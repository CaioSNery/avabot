using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IUnitNavigationService
{
    Task<UnidadeEnsino> ExecuteAsync(
        UnidadeEnsino unit,
        CancellationToken cancellationToken = default);
}
