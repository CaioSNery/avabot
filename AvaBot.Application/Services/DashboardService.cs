using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class DashboardService(ILogger<DashboardService> logger) : IDashboardService
{
    private const string StudyLinkName = "Estudar";
    private static readonly TimeSpan StudyLinkTimeout = TimeSpan.FromSeconds(15);

    public async Task WaitUntilReadyAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Aguardando dashboard pelo link de navegação {LinkName}",
            StudyLinkName);

        var studyLink = page.GetLinkByRole(StudyLinkName);
        var isVisible = await studyLink.TryWaitForVisibleAsync(
            StudyLinkTimeout,
            cancellationToken);
        var elementCount = await studyLink.CountAsync(cancellationToken);

        logger.LogInformation(
            "DashboardService encontrou {ElementCount} elemento(s) para {LinkName}",
            elementCount,
            StudyLinkName);

        if (!isVisible || elementCount == 0)
        {
            throw new InvalidOperationException(
                "O dashboard foi carregado, mas o link 'Estudar' não ficou disponível.");
        }

        logger.LogInformation("Dashboard carregado; link {LinkName} disponível", StudyLinkName);
    }
}
