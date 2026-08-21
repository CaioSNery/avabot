namespace AvaBot.Domain.Interfaces;

public interface ICookieBannerService
{
    Task CloseIfVisibleAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
