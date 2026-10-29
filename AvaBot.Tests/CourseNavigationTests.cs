using System.Reflection;
using AvaBot.Infrastructure.Browser;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace AvaBot.Tests;

public sealed class CourseNavigationTests
{
    private const string CourseUrl = "https://example.test/course/view.php?id=1&topic=2";

    [Fact]
    public async Task Course_NetworkTimeoutIsOptionalAfterDomLoaded()
    {
        var (service, fake) = Create(new TimeoutException());
        await service.NavigateAsync(CourseUrl);
        Assert.Equal(WaitUntilState.DOMContentLoaded, fake.WaitUntil);
        Assert.Equal(5000f, fake.NetworkTimeout);
    }

    [Fact]
    public async Task OtherPages_PreserveMandatoryNetworkIdle()
    {
        var (service, fake) = Create(new TimeoutException());
        await Assert.ThrowsAsync<TimeoutException>(() => service.NavigateAsync("https://example.test/login"));
        Assert.Equal(WaitUntilState.Load, fake.WaitUntil);
        Assert.Null(fake.NetworkTimeout);
    }

    [Fact]
    public async Task Course_NavigationFailureIsNotIgnored()
    {
        var (service, fake) = Create(new TimeoutException());
        fake.NavigationFailure = new TimeoutException("DOM did not load");
        await Assert.ThrowsAsync<TimeoutException>(() => service.NavigateAsync(CourseUrl));
        Assert.False(fake.NetworkWaitStarted);
    }

    [Fact]
    public async Task Course_ClosedPageIsNotIgnored()
    {
        var (service, _) = Create(new PlaywrightException("Target page has been closed"));
        await Assert.ThrowsAsync<PlaywrightException>(() => service.NavigateAsync(CourseUrl));
    }

    [Fact]
    public async Task Course_CancellationIsNotIgnored()
    {
        var (service, _) = Create(new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.NavigateAsync(CourseUrl));
    }

    private static (PlaywrightAutomationPage, NavigationPage) Create(Exception failure)
    {
        var page = DispatchProxy.Create<IPage, NavigationPage>();
        var fake = (NavigationPage)(object)page;
        fake.NetworkFailure = failure;
        return (new PlaywrightAutomationPage(page, NullLogger<PlaywrightAutomationPage>.Instance), fake);
    }

    public class NavigationPage : DispatchProxy
    {
        public Exception NetworkFailure { get; set; } = new TimeoutException();
        public Exception? NavigationFailure { get; set; }
        public WaitUntilState? WaitUntil { get; private set; }
        public float? NetworkTimeout { get; private set; }
        public bool NetworkWaitStarted { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Url": return CourseUrl;
                case nameof(IPage.GotoAsync):
                    WaitUntil = ((PageGotoOptions)args![1]!).WaitUntil;
                    return NavigationFailure is null
                        ? Task.FromResult<IResponse?>(null)
                        : Task.FromException<IResponse?>(NavigationFailure);
                case nameof(IPage.WaitForLoadStateAsync):
                    NetworkWaitStarted = true;
                    NetworkTimeout = (args![1] as PageWaitForLoadStateOptions)?.Timeout;
                    return Task.FromException(NetworkFailure);
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
