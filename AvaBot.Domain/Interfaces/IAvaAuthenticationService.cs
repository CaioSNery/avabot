namespace AvaBot.Domain.Interfaces;

public interface IAvaAuthenticationService
{
    Task AuthenticateAsync(
        IAutomationPage avaPage,
        CancellationToken cancellationToken = default);
}
