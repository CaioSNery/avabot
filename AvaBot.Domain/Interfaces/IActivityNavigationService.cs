using AvaBot.Domain.Models;
using DomainActivity = AvaBot.Domain.Models.Activity;

namespace AvaBot.Domain.Interfaces;

public interface IActivityNavigationService
{
    Task<ActivityNavigationResult> ExecuteAsync(
        IAutomationPage sectionPage,
        List<DomainActivity> activities,
        ActivityExecutionFilter filter,
        CancellationToken cancellationToken = default);
}
