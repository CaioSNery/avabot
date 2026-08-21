using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Browser;

internal sealed class PlaywrightAutomationLocator(
    ILocator locator,
    ILogger logger) : IAutomationLocator
{
    public IAutomationLocator Nth(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new PlaywrightAutomationLocator(locator.Nth(index), logger);
    }

    public IAutomationLocator GetLocator(string selector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        return new PlaywrightAutomationLocator(locator.Locator(selector), logger);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await locator.CountAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Contagem do locator cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao contar elementos do locator");
            throw;
        }
    }

    public async Task<string> InnerTextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await locator.InnerTextAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Leitura do texto do locator cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao obter o texto do locator");
            throw;
        }
    }

    public async Task<string?> GetAttributeAsync(
        string attributeName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);

        try
        {
            return await locator.GetAttributeAsync(attributeName).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Leitura do atributo {AttributeName} cancelada", attributeName);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao obter o atributo {AttributeName}", attributeName);
            throw;
        }
    }

    public async Task HighlightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await locator.HighlightAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Destaque do locator cancelado");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao destacar o locator");
            throw;
        }
    }

    public async Task ScrollIntoViewAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await locator.ScrollIntoViewIfNeededAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Rolagem até o locator cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao rolar até o locator");
            throw;
        }
    }

    public async Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await locator.IsVisibleAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Verificação de visibilidade do locator cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao verificar visibilidade do locator");
            throw;
        }
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await locator.IsEnabledAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Verificação de habilitação do locator cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao verificar habilitação do locator");
            throw;
        }
    }

    public async Task<bool> TryWaitForVisibleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "O timeout deve ser maior que zero.");
        }

        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)timeout.TotalMilliseconds
            }).WaitAsync(cancellationToken);

            return true;
        }
        catch (PlaywrightException exception)
            when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera pelo locator visível cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao aguardar locator visível");
            throw;
        }
    }

    public async Task WaitForAttributeAsync(
        string attributeName,
        string expectedValue,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        ArgumentNullException.ThrowIfNull(expectedValue);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "O timeout deve ser maior que zero.");
        }

        try
        {
            await Assertions.Expect(locator).ToHaveAttributeAsync(
                    attributeName,
                    expectedValue,
                    new LocatorAssertionsToHaveAttributeOptions
                    {
                        Timeout = (float)timeout.TotalMilliseconds
                    })
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Espera pelo atributo {AttributeName}={ExpectedValue} cancelada",
                attributeName,
                expectedValue);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao aguardar o atributo {AttributeName}={ExpectedValue}",
                attributeName,
                expectedValue);
            throw;
        }
    }

    public async Task ClickAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await locator.ClickAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Clique no locator cancelado");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao clicar no locator");
            throw;
        }
    }

    public async Task WaitForHiddenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden
            }).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Espera pelo desaparecimento do locator cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao aguardar o desaparecimento do locator");
            throw;
        }
    }
}
