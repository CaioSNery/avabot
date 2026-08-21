namespace AvaBot.Domain.Models;

public sealed class DidacticBookOptions
{
    public const string SectionName = "DidacticBook";

    public bool Enabled { get; init; } = true;

    public int CompletionRenderDelay { get; init; } = 5000;

    public int VisitDuration { get; init; } = 3000;
}
