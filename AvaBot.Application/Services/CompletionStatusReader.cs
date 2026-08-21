using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;

namespace AvaBot.Application.Services;

internal static class CompletionStatusReader
{
    public static async Task<(ActivityCompletionStatus Status, string Evidence)> ReadAsync(
        IAutomationLocator indicator,
        CancellationToken cancellationToken)
    {
        if (await indicator.CountAsync(cancellationToken) == 0)
        {
            return (ActivityCompletionStatus.Unknown, "indicador ausente");
        }

        var completedIcon = indicator.GetLocator(
            ".format_list_topics_complete, .icon-check_circle");

        for (var index = 0;
             index < await completedIcon.CountAsync(cancellationToken);
             index++)
        {
            if (await completedIcon.Nth(index).IsVisibleAsync(cancellationToken))
            {
                return (
                    ActivityCompletionStatus.Completed,
                    "ícone visual de conclusão exibido pelo AVA");
            }
        }

        var pendingIcon = indicator.GetLocator(
            ".format_list_topics_required, .icon-new_releases");

        for (var index = 0;
             index < await pendingIcon.CountAsync(cancellationToken);
             index++)
        {
            if (await pendingIcon.Nth(index).IsVisibleAsync(cancellationToken))
            {
                return (
                    ActivityCompletionStatus.Pending,
                    "ícone visual de pendência exibido pelo AVA");
            }
        }

        var currentValue = await indicator.GetAttributeAsync(
            "completion",
            cancellationToken);
        var completedValue = await indicator.GetAttributeAsync(
            "completion_rules",
            cancellationToken);

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return (ActivityCompletionStatus.Unknown, "atributo completion vazio");
        }

        var completed =
            !string.IsNullOrWhiteSpace(completedValue) &&
            string.Equals(
                currentValue.Trim(),
                completedValue.Trim(),
                StringComparison.OrdinalIgnoreCase);

        return completed
            ? (ActivityCompletionStatus.Completed,
                $"completion={currentValue}; completion_rules={completedValue}")
            : (ActivityCompletionStatus.Pending,
                $"completion={currentValue}; completion_rules={completedValue}");
    }
}
