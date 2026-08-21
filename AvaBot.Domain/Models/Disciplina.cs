using AvaBot.Domain.Interfaces;

namespace AvaBot.Domain.Models;

public sealed class Disciplina
{
    public string Nome { get; init; } = string.Empty;

    public string Texto { get; init; } = string.Empty;

    public required IAutomationLocator CardLocator { get; init; }

    public required IAutomationLocator AccessButton { get; init; }

    public int QuantidadeBotoes { get; init; }

    public bool PossuiPendencias { get; set; }

    public int QuantidadeAtividades { get; set; }
}
