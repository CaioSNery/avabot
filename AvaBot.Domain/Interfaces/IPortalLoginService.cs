namespace AvaBot.Domain.Interfaces;

public interface IPortalLoginService
{
    Task AuthenticateAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
