using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class PipelinePauseService(
    ILogger<PipelinePauseService> logger) : IPipelinePauseService
{
    public async Task WaitAsync(
        IAutomationPage page,
        string completedStage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(completedStage);

        if (!cancellationToken.CanBeCanceled)
        {
            throw new InvalidOperationException(
                "A sessão do navegador precisa de um CancellationToken cancelável.");
        }

        logger.LogInformation(
            "Etapa {CompletedStage} concluída em {CurrentUrl}. " +
            "Pressione Ctrl+C para encerrar o navegador",
            completedStage,
            page.Url);

        var completionSource = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = cancellationToken.Register(
            () => completionSource.TrySetResult());

        await completionSource.Task;

        logger.LogInformation(
            "Encerramento solicitado. O BrowserService poderá liberar os recursos do navegador");
    }
}
