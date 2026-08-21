namespace AvaBot.Domain.Interfaces;

public interface IDashboardService
{
    Task WaitUntilReadyAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
