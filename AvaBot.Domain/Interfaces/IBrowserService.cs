namespace AvaBot.Domain.Interfaces;

public interface IBrowserService
{
    Task<TResult> ExecuteAsync<TResult>(
        string url,
        Func<IAutomationPage, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
