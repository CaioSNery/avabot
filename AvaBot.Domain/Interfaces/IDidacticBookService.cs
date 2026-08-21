namespace AvaBot.Domain.Interfaces;

public interface IDidacticBookService
{
    Task<int> VisitPendingAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken = default);
}
