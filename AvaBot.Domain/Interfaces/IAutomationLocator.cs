namespace AvaBot.Domain.Interfaces;

public interface IAutomationLocator
{
    IAutomationLocator Nth(int index);

    IAutomationLocator GetLocator(string selector);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<string> InnerTextAsync(CancellationToken cancellationToken = default);

    Task<string?> GetAttributeAsync(
        string attributeName,
        CancellationToken cancellationToken = default);

    Task HighlightAsync(CancellationToken cancellationToken = default);

    Task ScrollIntoViewAsync(CancellationToken cancellationToken = default);

    Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    Task<bool> TryWaitForVisibleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task WaitForAttributeAsync(
        string attributeName,
        string expectedValue,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task ClickAsync(CancellationToken cancellationToken = default);

    Task WaitForHiddenAsync(CancellationToken cancellationToken = default);
}
