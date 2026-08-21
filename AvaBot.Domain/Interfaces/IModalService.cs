namespace AvaBot.Domain.Interfaces;

public interface IModalService
{
    Task CloseAllModalsAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default);
}
