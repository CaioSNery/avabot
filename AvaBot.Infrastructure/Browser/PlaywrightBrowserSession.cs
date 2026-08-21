using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Browser;

internal sealed class PlaywrightBrowserSession(
    IPlaywright playwright,
    IBrowser browser,
    IBrowserContext context,
    IAutomationPage page,
    ILogger<PlaywrightBrowserSession> logger) : IBrowserSession
{
    private bool _disposed;

    public IAutomationPage Page { get; } = page;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await TryCloseAsync(() => context.CloseAsync(), "contexto do navegador");
        await TryCloseAsync(() => browser.CloseAsync(), "navegador");
        playwright.Dispose();

        logger.LogInformation("Recursos do Playwright liberados");
    }

    private async Task TryCloseAsync(Func<Task> closeAsync, string resourceName)
    {
        try
        {
            await closeAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Falha ao fechar {ResourceName}", resourceName);
        }
    }
}
