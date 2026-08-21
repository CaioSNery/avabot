using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;

namespace AvaBot.Tests;

internal sealed class MockInferenceEngine(
    IReadOnlyList<InferenceResultDto> results) : IInferenceEngine
{
    public int ExecutionCount { get; private set; }

    public Task<IReadOnlyList<InferenceResultDto>> ExecuteAsync(
        InferenceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ExecutionCount++;
        return Task.FromResult(results);
    }
}
