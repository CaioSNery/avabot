namespace AvaBot.Domain.Interfaces;

public interface IAvaLoginService
{
    Task LoginAsync(
        IAutomationPage avaPage,
        string login,
        string senha,
        CancellationToken cancellationToken = default);
}
