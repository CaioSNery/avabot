using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IInferenceConsoleFlow
{
    Task<IReadOnlyList<InferenceResultDto>> ExecuteFromFileAsync(
        string questionsJsonPath,
        bool visualMode,
        CancellationToken cancellationToken = default);
}
