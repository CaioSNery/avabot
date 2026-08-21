using AvaBot.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvaBot.Tests;

public sealed class ActivityProgressStoreTests
{
    [Fact]
    public void BuildScopedStatePath_SeparatesAccountsWithoutExposingLogin()
    {
        const string firstLogin = "11122233344";
        const string secondLogin = "55566677788";

        var firstPath = ActivityProgressStore.BuildScopedStatePath(firstLogin);
        var secondPath = ActivityProgressStore.BuildScopedStatePath(secondLogin);

        Assert.NotEqual(firstPath, secondPath);
        Assert.DoesNotContain(firstLogin, firstPath);
        Assert.DoesNotContain(secondLogin, secondPath);
        Assert.EndsWith("activity-progress.json", firstPath);
    }

    [Fact]
    public async Task MarkCompletedAsync_PersistsCompletionAcrossInstances()
    {
        var statePath = Path.Combine(
            Path.GetTempPath(),
            $"activity-progress-{Guid.NewGuid():N}.json");

        try
        {
            var firstStore = new ActivityProgressStore(
                statePath,
                NullLogger<ActivityProgressStore>.Instance);
            await firstStore.MarkCompletedAsync("https://ava/activity/1");

            var secondStore = new ActivityProgressStore(
                statePath,
                NullLogger<ActivityProgressStore>.Instance);

            Assert.True(await secondStore.IsCompletedAsync("https://ava/activity/1"));
            Assert.False(await secondStore.IsCompletedAsync("https://ava/activity/2"));
        }
        finally
        {
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
    }
}
