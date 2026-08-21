using AvaBot.Domain.Models;

namespace AvaBot.Infrastructure.OpenAI;

public interface IOpenAiClient
{
    Task<OpenAiClientResult> ExecuteAsync(
        string model,
        string prompt,
        InferenceRequestDto request,
        CancellationToken cancellationToken = default);
}
