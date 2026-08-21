namespace AvaBot.Domain.Models;

public sealed class BrowserOptions
{
    public const string SectionName = "Browser";

    public bool Headless { get; init; } = true;

    public int Timeout { get; init; } = 30_000;
}
