using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IUnitExplorerService
{
    Task<List<UnidadeEnsino>> ExecuteAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken = default);
}
