using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface ILinearCourseExplorerService
{
    Task<List<Section>> ExecuteAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken = default);
}
