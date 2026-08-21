using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface ISectionExplorerService
{
    Task<List<Section>> ExecuteAsync(
        IAutomationPage disciplinePage,
        UnidadeEnsino expandedUnit,
        CancellationToken cancellationToken = default);
}
