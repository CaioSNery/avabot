namespace AvaBot.Domain.Interfaces;

public interface IPortalNavigationService
{
    Task<IAutomationPage> NavigateToAvaAsync(
        IAutomationPage portalPage,
        CancellationToken cancellationToken = default);
}
