namespace AvaBot.Domain.Interfaces;

public interface IQuizSubmissionService
{
    Task<bool> CompleteAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
