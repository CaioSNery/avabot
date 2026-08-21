using System.Text.Json.Serialization;

namespace AvaBot.Domain.Models;

public sealed record InferenceRequestDto(
    [property: JsonPropertyName("questions")]
    IReadOnlyList<QuestionRequestDto> Questions)
{
    [JsonIgnore]
    public bool VisualMode { get; init; }

    [JsonPropertyName("responseToReview")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResponseToReview { get; init; }
}
