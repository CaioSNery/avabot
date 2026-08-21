namespace AvaBot.Domain.Interfaces;

public interface IBrowserFactory
{
    Task<IBrowserSession> CreateAsync(CancellationToken cancellationToken = default);
}
