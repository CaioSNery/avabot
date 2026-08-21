using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IDisciplinaNavigationService
{
    Task ExecuteAsync(
        IAutomationPage avaPage,
        Disciplina disciplina,
        CancellationToken cancellationToken = default);
}
