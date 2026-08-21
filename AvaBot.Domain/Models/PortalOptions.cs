namespace AvaBot.Domain.Models;

public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    public string Url { get; init; } = string.Empty;

    public string Login { get; init; } = string.Empty;

    public string Senha { get; init; } = string.Empty;
}
