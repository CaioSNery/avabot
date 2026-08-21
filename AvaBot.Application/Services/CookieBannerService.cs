using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class CookieBannerService(
    ILogger<CookieBannerService> logger) : ICookieBannerService
{
    private const string AcceptButtonSelector = ".cookie-police-bar.accept-button";

    public async Task CloseIfVisibleAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var acceptButton = page.GetLocator(AcceptButtonSelector);
        var buttonCount = await acceptButton.CountAsync(cancellationToken);

        if (buttonCount == 0 || !await acceptButton.Nth(0).IsVisibleAsync(cancellationToken))
        {
            logger.LogInformation("Banner de cookies não apresentado");
            return;
        }

        logger.LogInformation("Banner de cookies encontrado; clicando em OK");
        await acceptButton.Nth(0).ClickAsync(cancellationToken);
        await acceptButton.Nth(0).WaitForHiddenAsync(cancellationToken);
        logger.LogInformation("Banner de cookies fechado");
    }
}
