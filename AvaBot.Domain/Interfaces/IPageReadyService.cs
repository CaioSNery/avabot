namespace AvaBot.Domain.Interfaces;

public interface IPageReadyService
{
    Task WaitForPortalAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
