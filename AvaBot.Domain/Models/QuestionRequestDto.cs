using System.Text.Json.Serialization;

namespace AvaBot.Domain.Models;

public sealed class QuestionRequestDto
{
    [JsonPropertyName("questionNumber")]
    public int QuestionNumber { get; init; }

    [JsonPropertyName("statement")]
    public string Statement { get; init; } = string.Empty;

    [JsonPropertyName("alternatives")]
    public IReadOnlyList<AlternativeRequestDto> Alternatives { get; init; } = [];

    [JsonPropertyName("formulas")]
    public IReadOnlyList<string> Formulas { get; init; } = [];

    [JsonPropertyName("tables")]
    public IReadOnlyList<TableRequestDto> Tables { get; init; } = [];

    [JsonPropertyName("visualDescriptions")]
    public IReadOnlyList<string> VisualDescriptions { get; init; } = [];

    [JsonPropertyName("screenshotPresent")]
    public bool ScreenshotPresent => !string.IsNullOrWhiteSpace(ScreenshotDataUrl);

    [JsonPropertyName("screenshotBytes")]
    public int ScreenshotBytes => EstimateDataUrlBytes(ScreenshotDataUrl);

    [JsonIgnore]
    public string ScreenshotDataUrl { get; init; } = string.Empty;

    private static int EstimateDataUrlBytes(string dataUrl)
    {
        var separatorIndex = dataUrl.IndexOf(',');
        if (separatorIndex < 0 || separatorIndex == dataUrl.Length - 1)
        {
            return 0;
        }

        var base64Length = dataUrl.Length - separatorIndex - 1;
        return base64Length * 3 / 4;
    }
}

public sealed record TableRequestDto(
    [property: JsonPropertyName("headers")] IReadOnlyList<string> Headers,
    [property: JsonPropertyName("rows")] IReadOnlyList<IReadOnlyList<string>> Rows);
