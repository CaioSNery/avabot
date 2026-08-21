using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Browser;

public sealed class BrowserFactory(
    IOptions<BrowserOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<BrowserFactory> logger) : IBrowserFactory
{
    private readonly BrowserOptions _options = options.Value;

    public async Task<IBrowserSession> CreateAsync(CancellationToken cancellationToken = default)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        IBrowserContext? context = null;

        try
        {
            ValidateOptions();
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "Iniciando Chromium. Headless: {Headless}, TimeoutMs: {TimeoutMs}",
                _options.Headless,
                _options.Timeout);

            playwright = await Microsoft.Playwright.Playwright.CreateAsync()
                .WaitAsync(cancellationToken);

            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = _options.Headless
            }).WaitAsync(cancellationToken);

            context = await browser.NewContextAsync().WaitAsync(cancellationToken);
            var page = await context.NewPageAsync().WaitAsync(cancellationToken);

            page.SetDefaultTimeout(_options.Timeout);
            page.SetDefaultNavigationTimeout(_options.Timeout);

            logger.LogInformation("Chromium iniciado e nova página criada");

            var automationPage = new PlaywrightAutomationPage(
                page,
                loggerFactory.CreateLogger<PlaywrightAutomationPage>());

            return new PlaywrightBrowserSession(
                playwright,
                browser,
                context,
                automationPage,
                loggerFactory.CreateLogger<PlaywrightBrowserSession>());
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Inicialização do Chromium cancelada");
            await DisposePartiallyCreatedResourcesAsync(context, browser, playwright);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao iniciar o Playwright e o Chromium");
            await DisposePartiallyCreatedResourcesAsync(context, browser, playwright);
            throw;
        }
    }

    private void ValidateOptions()
    {
        if (_options.Timeout <= 0)
        {
            throw new InvalidOperationException("Browser:Timeout deve ser maior que zero.");
        }
    }

    private async Task DisposePartiallyCreatedResourcesAsync(
        IBrowserContext? context,
        IBrowser? browser,
        IPlaywright? playwright)
    {
        if (context is not null)
        {
            await TryCloseAsync(() => context.CloseAsync(), "contexto");
        }

        if (browser is not null)
        {
            await TryCloseAsync(() => browser.CloseAsync(), "navegador");
        }

        playwright?.Dispose();
    }

    private async Task TryCloseAsync(Func<Task> closeAsync, string resourceName)
    {
        try
        {
            await closeAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Falha ao liberar {ResourceName} após erro de inicialização", resourceName);
        }
    }
}
