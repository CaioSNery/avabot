namespace AvaBot.Domain.Interfaces;

public interface IManualQuizNavigationService
{
    Task NavigateToSummaryAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
