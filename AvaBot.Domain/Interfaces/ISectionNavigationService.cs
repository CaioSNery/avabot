using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface ISectionNavigationService
{
    Task<Section> ExecuteAsync(
        IAutomationPage disciplinePage,
        Section section,
        CancellationToken cancellationToken = default);
}
