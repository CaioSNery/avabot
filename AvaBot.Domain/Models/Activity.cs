using AvaBot.Domain.Interfaces;

namespace AvaBot.Domain.Models;

public sealed class Activity
{
    public string Categoria { get; init; } = string.Empty;

    public string Nome { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public ActivityType Tipo { get; init; }

    public ActivityCompletionStatus CompletionStatus { get; init; }

    public string CompletionEvidence { get; init; } = string.Empty;

    public required IAutomationLocator Locator { get; init; }
}
