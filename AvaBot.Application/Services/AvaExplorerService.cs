using System.Text.RegularExpressions;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class AvaExplorerService(
    IAvaPageInspectionService inspectionService,
    ILogger<AvaExplorerService> logger) : IAvaExplorerService
{
    private const string DisciplinesSectionSelector =
        "#region-loggedin-home-enrolled-courses-box";

    private const string DisciplineCardSelector =
        ".card-container > .card-wrap > .card-item";

    private const string AccessButtonSelector =
        ".card-action a:has-text('Acessar a Disciplina')";

    private const string NameSelector = ".card-content > .card-content-title";

    public async Task<List<Disciplina>> ExecuteAsync(
        IAutomationPage avaPage,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("AvaExplorerService iniciado em modo somente leitura");
        await avaPage.WaitForDomContentLoadedAsync(cancellationToken);
        await avaPage.WaitForNetworkIdleAsync(cancellationToken);

        logger.LogInformation(
            "Página inicial do AVA carregada. URL: {AvaUrl}; título: {PageTitle}",
            avaPage.Url,
            await avaPage.GetTitleAsync(cancellationToken));

        var disciplinesSection = avaPage.GetLocator(DisciplinesSectionSelector);
        var sectionCount = await disciplinesSection.CountAsync(cancellationToken);
        logger.LogInformation(
            "Quantidade de seções 'Minhas Disciplinas' encontradas: {SectionCount}",
            sectionCount);

        var cards = disciplinesSection.GetLocator(DisciplineCardSelector);
        var cardCount = await cards.CountAsync(cancellationToken);
        logger.LogInformation("Quantidade de cards encontrados: {CardCount}", cardCount);

        var disciplines = new List<Disciplina>(cardCount);

        for (var index = 0; index < cardCount; index++)
        {
            var card = cards.Nth(index);
            var discipline = await MapCardAsync(card, index, cancellationToken);
            disciplines.Add(discipline);

            logger.LogInformation(
                "Card {CardNumber}: disciplina {DisciplineName}; botões encontrados dentro do card: {ButtonCount}",
                index + 1,
                discipline.Nome,
                discipline.QuantidadeBotoes);
        }

        logger.LogInformation(
            "Mapeamento concluído: {DisciplineCount} disciplina(s) obtida(s) de {CardCount} card(s)",
            disciplines.Count,
            cardCount);

        await inspectionService.CaptureHomeAsync(
            avaPage,
            cards,
            disciplines,
            cancellationToken);

        return disciplines;
    }

    private static async Task<Disciplina> MapCardAsync(
        IAutomationLocator card,
        int index,
        CancellationToken cancellationToken)
    {
        var text = NormalizeText(await card.InnerTextAsync(cancellationToken));
        var buttons = card.GetLocator(AccessButtonSelector);
        var buttonCount = await buttons.CountAsync(cancellationToken);
        var name = await ExtractNameAsync(card, text, cancellationToken);

        return new Disciplina
        {
            Nome = string.IsNullOrWhiteSpace(name)
                ? $"Disciplina sem nome identificado #{index + 1}"
                : name,
            Texto = text,
            CardLocator = card,
            AccessButton = buttonCount > 0 ? buttons.Nth(0) : card.GetLocator(AccessButtonSelector),
            QuantidadeBotoes = buttonCount,
            PossuiPendencias = HasPendingIndicator(text),
            QuantidadeAtividades = ExtractActivityCount(text)
        };
    }

    private static async Task<string> ExtractNameAsync(
        IAutomationLocator card,
        string cardText,
        CancellationToken cancellationToken)
    {
        var name = card.GetLocator(NameSelector);
        if (await name.CountAsync(cancellationToken) > 0)
        {
            var candidate = NormalizeText(await name.Nth(0).InnerTextAsync(cancellationToken));

            if (IsValidName(candidate))
            {
                return candidate;
            }
        }

        return cardText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(IsValidName) ?? string.Empty;
    }

    private static bool IsValidName(string text) =>
        !string.IsNullOrWhiteSpace(text) &&
        !text.Contains("Acessar disciplina", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("Acessar a disciplina", StringComparison.OrdinalIgnoreCase) &&
        !HasPendingIndicator(text);

    private static bool HasPendingIndicator(string text) =>
        Regex.IsMatch(
            text,
            @"pend[eê]ncia|pendente|a\s+realizar|não\s+realizada|não\s+concluída",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static int ExtractActivityCount(string text)
    {
        var matches = Regex.Matches(
            text,
            @"(?:(?<count>\d+)\s*atividades?|atividades?\s*[:\-]?\s*(?<count>\d+))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return matches
            .Select(match => int.TryParse(match.Groups["count"].Value, out var count) ? count : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string NormalizeText(string text) =>
        string.Join(
            '\n',
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
