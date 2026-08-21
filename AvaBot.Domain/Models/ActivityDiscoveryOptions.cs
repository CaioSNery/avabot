namespace AvaBot.Domain.Models;

public sealed class ActivityDiscoveryOptions
{
    public const string SectionName = "ActivityDiscovery";

    public int CompletionRenderDelay { get; init; } = 5000;
}
