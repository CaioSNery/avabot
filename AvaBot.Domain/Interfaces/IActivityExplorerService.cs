using AvaBot.Domain.Models;
using DomainActivity = AvaBot.Domain.Models.Activity;

namespace AvaBot.Domain.Interfaces;

public interface IActivityExplorerService
{
    Task<List<DomainActivity>> ExecuteAsync(
        IAutomationPage sectionPage,
        Section section,
        CancellationToken cancellationToken = default);
}
