namespace AvaBot.Domain.Interfaces;

public interface IActivityProgressStore
{
    Task<bool> IsCompletedAsync(
        string activityIdentifier,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        string activityIdentifier,
        CancellationToken cancellationToken = default);
}
