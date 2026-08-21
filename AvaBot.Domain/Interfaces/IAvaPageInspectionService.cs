using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IAvaPageInspectionService
{
    Task CaptureHomeAsync(
        IAutomationPage avaPage,
        IAutomationLocator cards,
        IReadOnlyCollection<Disciplina> disciplines,
        CancellationToken cancellationToken = default);
}
