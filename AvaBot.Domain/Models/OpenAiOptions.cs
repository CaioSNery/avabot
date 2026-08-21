namespace AvaBot.Domain.Models;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;

    public string PrimaryModel { get; init; } = string.Empty;

    public string FallbackModel { get; init; } = string.Empty;

    public double FallbackConfidenceThreshold { get; init; } = 0.75;

    public bool EnableConsoleInference { get; init; }

    public double Temperature { get; init; }

    public int MaxOutputTokens { get; init; } = 4096;

    public string ReasoningEffort { get; init; } = "minimal";

    public int RequestTimeoutSeconds { get; init; } = 300;

    public int RetryCount { get; init; } = 2;
}
