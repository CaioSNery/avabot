using System.Text.Json.Serialization;

namespace AvaBot.Domain.Models;

public sealed record AlternativeRequestDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text);
