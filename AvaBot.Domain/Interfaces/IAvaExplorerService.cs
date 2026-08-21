using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IAvaExplorerService
{
    Task<List<Disciplina>> ExecuteAsync(
        IAutomationPage avaPage,
        CancellationToken cancellationToken = default);
}
