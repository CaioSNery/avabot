namespace AvaBot.Domain.Models;

public sealed class AvaOptions
{
    public const string SectionName = "Ava";

    public string Login { get; init; } = string.Empty;

    public string Senha { get; init; } = string.Empty;
}
