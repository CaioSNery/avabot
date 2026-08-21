namespace AvaBot.Domain.Interfaces;

public interface IPipelinePauseService
{
    Task WaitAsync(
        IAutomationPage page,
        string completedStage,
        CancellationToken cancellationToken = default);
}
