using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class PageReadyService(ILogger<PageReadyService> logger) : IPageReadyService
{
    private const string FinalPortalUrlFragment = "pda_unime";
    private static readonly TimeSpan AngularRenderTime = TimeSpan.FromSeconds(1);

    public async Task WaitForPortalAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Aguardando URL final do portal contendo {UrlFragment}",
            FinalPortalUrlFragment);
        await page.WaitForUrlContainingAsync(FinalPortalUrlFragment, cancellationToken);
        logger.LogInformation("URL final do portal alcançada: {CurrentUrl}", page.Url);

        logger.LogInformation("Aguardando DOMContentLoaded no portal");
        await page.WaitForDomContentLoadedAsync(cancellationToken);
        logger.LogInformation("DOMContentLoaded concluído");

        logger.LogInformation(
            "NetworkIdle não será exigido no Portal porque a aplicação mantém requisições em segundo plano");

        logger.LogInformation(
            "Aguardando {RenderMilliseconds} ms para renderização do Angular",
            AngularRenderTime.TotalMilliseconds);
        await page.WaitForClientRenderAsync(AngularRenderTime, cancellationToken);
        logger.LogInformation("Renderização inicial do Angular concluída");
    }
}
