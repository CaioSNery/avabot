using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class PortalNavigationService(
    ILogger<PortalNavigationService> logger) : IPortalNavigationService
{
    private const string StudyLinkName = "Estudar";
    private const string AvaUrlFragment = "avaeduc.com.br";
    private static readonly TimeSpan StudyLinkTimeout = TimeSpan.FromSeconds(15);

    public async Task<IAutomationPage> NavigateToAvaAsync(
        IAutomationPage portalPage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Iniciando PortalNavigationService");
            logger.LogInformation("Procurando menu {LinkName}", StudyLinkName);

            var studyLink = portalPage.GetLinkByRole(StudyLinkName);

            logger.LogInformation(
                "Aguardando o menu {LinkName} ficar visível antes do clique",
                StudyLinkName);

            var isVisible = await studyLink.TryWaitForVisibleAsync(
                StudyLinkTimeout,
                cancellationToken);
            var elementCount = await studyLink.CountAsync(cancellationToken);
            var isEnabled = isVisible && elementCount > 0 &&
                await studyLink.IsEnabledAsync(cancellationToken);

            logger.LogInformation("Quantidade de elementos encontrados: {ElementCount}", elementCount);
            logger.LogInformation("Elemento visível: {IsVisible}", isVisible);
            logger.LogInformation("Elemento habilitado: {IsEnabled}", isEnabled);

            if (elementCount == 0 || !isVisible || !isEnabled)
            {
                throw new InvalidOperationException(
                    "O menu 'Estudar' não ficou visível e habilitado dentro do tempo esperado.");
            }

            logger.LogInformation("Menu {LinkName} pronto para clique", StudyLinkName);

            var avaPage = await portalPage.OpenLinkInNewPageAsync(
                StudyLinkName,
                cancellationToken);

            logger.LogInformation("Nova aba aberta; aguardando URL do AVA");
            await avaPage.WaitForUrlContainingAsync(AvaUrlFragment, cancellationToken);

            logger.LogInformation("Aguardando DOMContentLoaded na aba do AVA");
            await avaPage.WaitForDomContentLoadedAsync(cancellationToken);
            logger.LogInformation("DOMContentLoaded concluído na aba do AVA");

            logger.LogInformation("Aguardando NetworkIdle na aba do AVA");
            await avaPage.WaitForNetworkIdleAsync(cancellationToken);
            logger.LogInformation("NetworkIdle concluído na aba do AVA");

            var title = await avaPage.GetTitleAsync(cancellationToken);
            logger.LogInformation("URL da aba do AVA: {AvaUrl}", avaPage.Url);
            logger.LogInformation("Título da aba do AVA: {AvaTitle}", title);
            logger.LogInformation("Transição para o AVA concluída");

            return avaPage;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha na transição do Portal para o AVA. Exceção completa: {ExceptionDetails}",
                exception.ToString());
            throw;
        }
    }
}
