namespace AvaBot.Domain.Interfaces;

public interface IQuestionConsoleFlow
{
    Task ExecuteAsync(
        IAutomationPage page,
        string disciplineName,
        bool visualMode,
        CancellationToken cancellationToken = default);
}
