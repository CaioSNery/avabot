using AvaBot.Domain.Models;
using AvaBot.Infrastructure.OpenAI;

namespace AvaBot.Tests;

public sealed class InferenceCacheTests
{
    [Fact]
    public void TryGet_ReturnsPreviouslyStoredResultForSameQuestionAndModel()
    {
        var cache = new InferenceCache();
        var question = TestQuestionFactory.Create(1);
        var expected = new InferenceResultDto(1, InferenceStatuses.Answered, "A", 0.8, "Motivo");

        cache.Set(question, "gpt-5", expected);

        var found = cache.TryGet(question, "gpt-5", out var actual);

        Assert.True(found);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGet_DoesNotShareEntriesBetweenModels()
    {
        var cache = new InferenceCache();
        var question = TestQuestionFactory.Create(1);
        cache.Set(question, "modelo-a", new InferenceResultDto(1, InferenceStatuses.Answered, "A", 1, "Motivo"));

        Assert.False(cache.TryGet(question, "modelo-b", out _));
    }
}
