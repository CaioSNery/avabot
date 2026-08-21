namespace AvaBot.Infrastructure.OpenAI;

public sealed record OpenAiClientResult(
    string OutputJson,
    int InputTokens,
    int OutputTokens,
    int TotalTokens);
