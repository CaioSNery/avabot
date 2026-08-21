using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class ModalService(ILogger<ModalService> logger) : IModalService
{
    private const string NextAnnouncementSelector =
        "button:has-text('Próximo comunicado'), " +
        "[role='button']:has-text('Próximo comunicado'), " +
        "a:has-text('Próximo comunicado')";

    private const string CloseButtonSelector =
        "button:has(img[alt='close']), " +
        "[role='dialog'] button[aria-label*='fechar' i], " +
        "[role='dialog'] button[aria-label*='close' i], " +
        "[role='dialog'] button[title*='fechar' i], " +
        "[role='dialog'] button[title*='close' i], " +
        "[role='dialog'] button.close, " +
        "[role='dialog'] button[mat-dialog-close], " +
        ".modal.show button.close";

    private const int MaximumActions = 10;
    private const int MaximumIdlePolls = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AfterClickDelay = TimeSpan.FromMilliseconds(500);

    public async Task CloseAllModalsAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        logger.LogInformation("Iniciando tratamento dos modais na URL final: {CurrentUrl}", page.Url);

        var actionCount = 0;
        var nextAnnouncementCount = 0;
        var closeCount = 0;
        var idlePollCount = 0;

        while (actionCount < MaximumActions && idlePollCount < MaximumIdlePolls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextAnnouncementButton = await FindFirstVisibleAsync(
                page.GetLocator(NextAnnouncementSelector),
                cancellationToken);

            if (nextAnnouncementButton is not null)
            {
                logger.LogInformation(
                    "Comunicado encontrado; clicando em Próximo comunicado no canto superior");
                await nextAnnouncementButton.ClickAsync(cancellationToken);
                nextAnnouncementCount++;
                actionCount++;
                idlePollCount = 0;
                await page.WaitForClientRenderAsync(AfterClickDelay, cancellationToken);
                continue;
            }

            var closeButton = await FindFirstVisibleAsync(
                page.GetLocator(CloseButtonSelector),
                cancellationToken);

            if (closeButton is not null)
            {
                logger.LogInformation(
                    "Último comunicado encontrado; clicando no X e encerrando o tratamento");
                await closeButton.ClickAsync(cancellationToken);
                closeCount++;
                actionCount++;
                await page.WaitForClientRenderAsync(AfterClickDelay, cancellationToken);
                break;
            }

            idlePollCount++;
            await page.WaitForClientRenderAsync(PollInterval, cancellationToken);
        }

        if (actionCount == MaximumActions)
        {
            logger.LogWarning(
                "O limite de {MaximumActions} ações em modais foi atingido; interrompendo para evitar um ciclo infinito",
                MaximumActions);
        }

        logger.LogInformation(
            "Tratamento de modais concluído: {NextCount} avanço(s) em Próximo comunicado e {CloseCount} clique(s) no X",
            nextAnnouncementCount,
            closeCount);
    }

    private static async Task<IAutomationLocator?> FindFirstVisibleAsync(
        IAutomationLocator candidates,
        CancellationToken cancellationToken)
    {
        var count = await candidates.CountAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            var candidate = candidates.Nth(index);

            if (await candidate.IsVisibleAsync(cancellationToken) &&
                await candidate.IsEnabledAsync(cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }
}
