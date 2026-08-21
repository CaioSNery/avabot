using System.Text.Json.Serialization;

namespace AvaBot.Domain.Models;

public sealed record InferenceResultDto(
    [property: JsonPropertyName("questionNumber")] int QuestionNumber,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("selectedAlternative")] string? SelectedAlternative,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("reasoning")] string Reasoning,
    [property: JsonPropertyName("calculatedAnswer")] string? CalculatedAnswer = null,
    [property: JsonPropertyName("missingData")] string? MissingData = null,
    [property: JsonPropertyName("warning")] string? Warning = null,
    [property: JsonPropertyName("validation")] InferenceValidationDto? Validation = null);

public sealed record InferenceValidationDto(
    [property: JsonPropertyName("hasSufficientData")] bool HasSufficientData,
    [property: JsonPropertyName("alternativeMatchesReasoning")] bool AlternativeMatchesReasoning);

public static class InferenceStatuses
{
    public const string Answered = "answered";
    public const string InsufficientData = "insufficient_data";
    public const string ManualReviewRequired = "manual_review_required";
}
