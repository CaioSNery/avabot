using AvaBot.Domain.Models;

namespace AvaBot.Tests;

public sealed class MockInferenceEngineTests
{
    [Fact]
    public async Task ExecuteAsync_UsesOnlyInferenceDtos()
    {
        var expected = new[]
        {
            new InferenceResultDto(1, InferenceStatuses.Answered, "A", 1, "Resultado simulado")
        };
        var engine = new MockInferenceEngine(expected);

        var result = await engine.ExecuteAsync(
            new InferenceRequestDto([TestQuestionFactory.Create(1)]));

        Assert.Equal(expected, result);
        Assert.Equal(1, engine.ExecutionCount);
    }
}
