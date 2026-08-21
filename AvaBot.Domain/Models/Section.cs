using AvaBot.Domain.Interfaces;

namespace AvaBot.Domain.Models;

public sealed class Section
{
    public string Unidade { get; init; } = string.Empty;

    public string Nome { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public bool Concluida { get; init; }

    public string CompletionEvidence { get; init; } = string.Empty;

    public bool PassiveOnly { get; init; }

    public required IAutomationLocator AccessLocator { get; init; }

    public List<Activity> Activities { get; } = [];
}
