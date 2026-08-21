using AvaBot.Domain.Models;
using AvaBot.Infrastructure.OpenAI;

namespace AvaBot.Tests;

internal sealed class MockOpenAiClient(params OpenAiClientResult[] results) : IOpenAiClient
{
    private readonly Queue<OpenAiClientResult> _results = new(results);

    public int CallCount { get; private set; }

    public List<InferenceRequestDto> Requests { get; } = [];

    public List<string> Models { get; } = [];

    public Task<OpenAiClientResult> ExecuteAsync(
        string model,
        string prompt,
        InferenceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        Models.Add(model);
        Requests.Add(request);

        if (_results.Count == 0)
        {
            throw new InvalidOperationException("Nenhum resultado simulado restante.");
        }

        return Task.FromResult(_results.Dequeue());
    }
}
