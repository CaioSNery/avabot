using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Browser;

internal sealed class PlaywrightAutomationPage(
    IPage page,
    ILogger<PlaywrightAutomationPage> logger) : IAutomationPage
{
    internal IPage NativePage => page;

    public string Url => page.Url;

    public IAutomationLocator GetLocator(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        return new PlaywrightAutomationLocator(page.Locator(selector), logger);
    }

    public IAutomationLocator GetFirstLocator(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        return new PlaywrightAutomationLocator(page.Locator(selector).First, logger);
    }

    public IAutomationLocator GetButtonByRole(string accessibleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);

        var locator = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            Name = accessibleName
        });

        return new PlaywrightAutomationLocator(locator, logger);
    }

    public IAutomationLocator GetLinkByRole(string accessibleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);

        var locator = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
        {
            Name = accessibleName,
            Exact = true
        });

        return new PlaywrightAutomationLocator(locator, logger);
    }

    public IAutomationLocator GetLinksByRole(string accessibleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);

        var locator = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
        {
            Name = accessibleName
        });

        return new PlaywrightAutomationLocator(locator, logger);
    }

    public async Task<IAutomationPage> OpenLinkInNewPageAsync(
        string accessibleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleName);

        try
        {
            var link = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
            {
                Name = accessibleName,
                Exact = true
            });

            logger.LogInformation("Aguardando nova aba antes do clique em {LinkName}", accessibleName);

            var newPage = await page.Context.RunAndWaitForPageAsync(
                async () =>
                {
                    logger.LogInformation("Clicando em {LinkName}", accessibleName);
                    await link.ClickAsync();
                    logger.LogInformation("Clique realizado em {LinkName}", accessibleName);
                }).WaitAsync(cancellationToken);

            logger.LogInformation("Nova aba encontrada após clicar em {LinkName}", accessibleName);
            return new PlaywrightAutomationPage(newPage, logger);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Captura da nova aba cancelada para o link {LinkName}", accessibleName);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao clicar no link {LinkName} e capturar a nova aba",
                accessibleName);
            throw;
        }
    }

    public async Task<string> GetTitleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await page.TitleAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Leitura do título da página cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao ler o título da página");
            throw;
        }
    }

    public async Task<string> GetContentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await page.ContentAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Leitura do HTML da página cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao obter o HTML da página");
            throw;
        }
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Navegando para {Url}", url);

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load
            }).WaitAsync(cancellationToken);

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle)
                .WaitAsync(cancellationToken);

            logger.LogInformation("Página carregada: {CurrentUrl}", page.Url);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Navegação cancelada para {Url}", url);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao navegar para {Url}", url);
            throw;
        }
    }

    public async Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Retornando para a página anterior");

            var response = await page.GoBackAsync(new PageGoBackOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).WaitAsync(cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException(
                    "O histórico do navegador não possui uma página anterior.");
            }

            logger.LogInformation("Retorno concluído para {CurrentUrl}", page.Url);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Retorno para a página anterior cancelado");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao retornar para a página anterior");
            throw;
        }
    }

    public async Task WaitForUrlContainingAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        try
        {
            await page.WaitForURLAsync(url =>
                    url.Contains(value, StringComparison.OrdinalIgnoreCase))
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera pela URL contendo {UrlValue} cancelada", value);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao aguardar URL contendo {UrlValue}", value);
            throw;
        }
    }

    public async Task<bool> TryWaitForUrlContainingAsync(
        string value,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "O timeout deve ser maior que zero.");
        }

        try
        {
            await page.WaitForURLAsync(
                    url => url.Contains(value, StringComparison.OrdinalIgnoreCase),
                    new PageWaitForURLOptions
                    {
                        Timeout = (float)timeout.TotalMilliseconds
                    })
                .WaitAsync(cancellationToken);

            return true;
        }
        catch (PlaywrightException exception)
            when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera opcional pela URL contendo {UrlValue} cancelada", value);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao aguardar opcionalmente URL contendo {UrlValue}",
                value);
            throw;
        }
    }

    public async Task WaitForUrlChangeAsync(
        string previousUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(previousUrl);

        try
        {
            await page.WaitForURLAsync(url =>
                    !url.Equals(previousUrl, StringComparison.OrdinalIgnoreCase))
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera pela mudança da URL cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao aguardar mudança da URL anterior {PreviousUrl}",
                previousUrl);
            throw;
        }
    }

    public Task WaitForDomContentLoadedAsync(CancellationToken cancellationToken = default) =>
        WaitForLoadStateAsync(
            LoadState.DOMContentLoaded,
            "DOMContentLoaded",
            cancellationToken);

    public Task WaitForNetworkIdleAsync(CancellationToken cancellationToken = default) =>
        WaitForLoadStateAsync(
            LoadState.NetworkIdle,
            "NetworkIdle",
            cancellationToken);

    public async Task WaitForClientRenderAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "A duração deve ser maior que zero.");
        }

        try
        {
            await page.WaitForTimeoutAsync((float)duration.TotalMilliseconds)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera pela renderização do cliente cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao aguardar renderização do cliente");
            throw;
        }
    }

    public Task FillAsync(
        string selector,
        string value,
        CancellationToken cancellationToken = default) =>
        ExecuteLocatorOperationAsync(
            selector,
            "preencher campo",
            async locator =>
            {
                await locator.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible
                }).WaitAsync(cancellationToken);

                await locator.FillAsync(value).WaitAsync(cancellationToken);
            });

    public Task ClickAsync(string selector, CancellationToken cancellationToken = default) =>
        ExecuteLocatorOperationAsync(
            selector,
            "clicar",
            async locator =>
            {
                await locator.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible
                }).WaitAsync(cancellationToken);

                await locator.ClickAsync().WaitAsync(cancellationToken);
            });

    public Task PressAsync(
        string selector,
        string key,
        CancellationToken cancellationToken = default) =>
        ExecuteLocatorOperationAsync(
            selector,
            $"pressionar a tecla {key}",
            locator => locator.PressAsync(key).WaitAsync(cancellationToken));

    public Task WaitForVisibleAsync(
        string selector,
        CancellationToken cancellationToken = default) =>
        ExecuteLocatorOperationAsync(
            selector,
            "aguardar visibilidade",
            locator => locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible
            }).WaitAsync(cancellationToken));

    public Task<bool> IsVisibleAsync(
        string selector,
        CancellationToken cancellationToken = default) =>
        ExecuteLocatorQueryAsync(
            selector,
            "verificar visibilidade",
            locator => locator.IsVisibleAsync().WaitAsync(cancellationToken));

    public Task<bool> IsEnabledAsync(
        string selector,
        CancellationToken cancellationToken = default) =>
        ExecuteLocatorQueryAsync(
            selector,
            "verificar habilitação",
            locator => locator.IsEnabledAsync().WaitAsync(cancellationToken));

    public Task<bool> IsDisabledAsync(
        string selector,
        CancellationToken cancellationToken = default) =>
        ExecuteLocatorQueryAsync(
            selector,
            "verificar desabilitação",
            locator => locator.IsDisabledAsync().WaitAsync(cancellationToken));

    public async Task ScreenshotAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = fullPath,
                FullPage = true
            }).WaitAsync(cancellationToken);

            logger.LogInformation("Screenshot salva em {ScreenshotPath}", fullPath);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Captura de screenshot cancelada para {ScreenshotPath}", path);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao salvar screenshot em {ScreenshotPath}", path);
            throw;
        }
    }

    public async Task WaitForLoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.Load)
                .WaitAsync(cancellationToken);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera pelo carregamento da página cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao aguardar o carregamento completo da página");
            throw;
        }
    }

    private async Task ExecuteLocatorOperationAsync(
        string selector,
        string operationName,
        Func<ILocator, Task> operation)
    {
        try
        {
            var locator = page.Locator(selector);
            await operation(locator);
            logger.LogDebug("Operação {OperationName} concluída para {Selector}", operationName, selector);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operação {OperationName} cancelada para {Selector}", operationName, selector);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao {OperationName} usando {Selector}",
                operationName,
                selector);
            throw;
        }
    }

    private async Task<TResult> ExecuteLocatorQueryAsync<TResult>(
        string selector,
        string operationName,
        Func<ILocator, Task<TResult>> operation)
    {
        try
        {
            var locator = page.Locator(selector);
            return await operation(locator);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operação {OperationName} cancelada para {Selector}", operationName, selector);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao {OperationName} usando {Selector}",
                operationName,
                selector);
            throw;
        }
    }

    private async Task WaitForLoadStateAsync(
        LoadState state,
        string stateName,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(state).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera por {LoadState} cancelada", stateName);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao aguardar {LoadState}", stateName);
            throw;
        }
    }
}
