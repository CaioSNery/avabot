namespace AvaBot.Domain.Interfaces;

public interface IBotRunner
{
    Task<int> RunAsync(CancellationToken cancellationToken = default);
}
