using AvaBot.Shared;

namespace AvaBot.Domain.Interfaces;

public interface IAuthenticationFlowService
{
    Task<OperationResult> ExecuteAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
