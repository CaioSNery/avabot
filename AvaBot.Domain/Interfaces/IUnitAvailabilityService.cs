using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IUnitAvailabilityService
{
    Task<bool> IsAvailableAsync(
        IAutomationPage disciplinePage,
        UnidadeEnsino unit,
        IReadOnlyList<Section> sections,
        CancellationToken cancellationToken = default);
}
