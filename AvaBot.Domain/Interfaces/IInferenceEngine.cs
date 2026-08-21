using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IInferenceEngine
{
    Task<IReadOnlyList<InferenceResultDto>> ExecuteAsync(
        InferenceRequestDto request,
        CancellationToken cancellationToken = default);
}
