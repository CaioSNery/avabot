namespace AvaBot.Domain.Interfaces;

public interface IAutomationPage
{
    string Url { get; }

    IAutomationLocator GetLocator(string selector);

    IAutomationLocator GetFirstLocator(string selector);

    IAutomationLocator GetButtonByRole(string accessibleName);

    IAutomationLocator GetLinkByRole(string accessibleName);

    IAutomationLocator GetLinksByRole(string accessibleName);

    Task<IAutomationPage> OpenLinkInNewPageAsync(
        string accessibleName,
        CancellationToken cancellationToken = default);

    Task<string> GetTitleAsync(CancellationToken cancellationToken = default);

    Task<string> GetContentAsync(CancellationToken cancellationToken = default);

    Task NavigateAsync(string url, CancellationToken cancellationToken = default);

    Task GoBackAsync(CancellationToken cancellationToken = default);

    Task WaitForUrlContainingAsync(
        string value,
        CancellationToken cancellationToken = default);

    Task<bool> TryWaitForUrlContainingAsync(
        string value,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task WaitForUrlChangeAsync(
        string previousUrl,
        CancellationToken cancellationToken = default);

    Task WaitForDomContentLoadedAsync(CancellationToken cancellationToken = default);

    Task WaitForNetworkIdleAsync(CancellationToken cancellationToken = default);

    Task WaitForClientRenderAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task FillAsync(string selector, string value, CancellationToken cancellationToken = default);

    Task PressAsync(
        string selector,
        string key,
        CancellationToken cancellationToken = default);

    Task ClickAsync(string selector, CancellationToken cancellationToken = default);

    Task WaitForVisibleAsync(string selector, CancellationToken cancellationToken = default);

    Task<bool> IsVisibleAsync(string selector, CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(string selector, CancellationToken cancellationToken = default);

    Task<bool> IsDisabledAsync(string selector, CancellationToken cancellationToken = default);

    Task ScreenshotAsync(string path, CancellationToken cancellationToken = default);

    Task WaitForLoadAsync(CancellationToken cancellationToken = default);
}
