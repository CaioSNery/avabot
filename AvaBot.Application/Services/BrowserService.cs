using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class BrowserService(
    IBrowserFactory browserFactory,
    ILogger<BrowserService> logger) : IBrowserService
{
    public async Task<TResult> ExecuteAsync<TResult>(
        string url,
        Func<IAutomationPage, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            logger.LogInformation("Iniciando sessão de navegação para {Url}", url);

            await using var session = await browserFactory.CreateAsync(cancellationToken);
            await session.Page.NavigateAsync(url, cancellationToken);

            var result = await operation(session.Page, cancellationToken);

            logger.LogInformation("Operação de navegação finalizada em {Url}", session.Page.Url);
            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operação de navegação cancelada para {Url}", url);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha durante a operação de navegação para {Url}", url);
            throw;
        }
    }
}
