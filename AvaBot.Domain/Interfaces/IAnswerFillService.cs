using AvaBot.Domain.Models;

namespace AvaBot.Domain.Interfaces;

public interface IAnswerFillService
{
    Task<bool> WaitForManualAnswerAsync(
        IAutomationPage page,
        string questionId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<AnswerFillResult> FillAnswerAsync(
        IAutomationPage page,
        string questionId,
        string selectedOption,
        CancellationToken cancellationToken = default);
}
