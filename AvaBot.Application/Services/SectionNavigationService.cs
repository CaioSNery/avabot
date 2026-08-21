using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class SectionNavigationService(
    ILogger<SectionNavigationService> logger) : ISectionNavigationService
{
    private const string SectionContentSelector = "#ct-cards-list";

    public async Task<Section> ExecuteAsync(
        IAutomationPage disciplinePage,
        Section section,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disciplinePage);
        ArgumentNullException.ThrowIfNull(section);

        var previousUrl = disciplinePage.Url;

        if (!string.IsNullOrWhiteSpace(section.Url) &&
            string.Equals(previousUrl, section.Url, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Seção {SectionName} já está aberta; novo clique não é necessário",
                section.Nome);
            await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);
            await disciplinePage.WaitForVisibleAsync(SectionContentSelector, cancellationToken);
            return section;
        }

        logger.LogInformation("Seção selecionada: {SectionName}", section.Nome);
        logger.LogInformation("URL antes de abrir a seção: {PreviousUrl}", previousUrl);

        await section.AccessLocator.ClickAsync(cancellationToken);
        logger.LogInformation("Clique na seção {SectionName} realizado", section.Nome);

        await disciplinePage.WaitForUrlChangeAsync(previousUrl, cancellationToken);
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);
        await disciplinePage.WaitForVisibleAsync(SectionContentSelector, cancellationToken);

        logger.LogInformation("Seção aberta: {SectionName}", section.Nome);
        logger.LogInformation("URL da seção: {SectionUrl}", disciplinePage.Url);
        logger.LogInformation(
            "Título da página da seção: {PageTitle}",
            await disciplinePage.GetTitleAsync(cancellationToken));

        return section;
    }
}
