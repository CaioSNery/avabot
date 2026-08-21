using AvaBot.Domain.Interfaces;

namespace AvaBot.Domain.Models;

public sealed class UnidadeEnsino
{
    public string Nome { get; init; } = string.Empty;

    public required IAutomationLocator Toggle { get; init; }

    public bool Expandida { get; set; }

    public bool Concluida { get; init; }

    public string CompletionEvidence { get; init; } = string.Empty;

    public List<Section> Sections { get; } = [];
}
