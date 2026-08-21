using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed partial class UnitAvailabilityService(
    ISectionNavigationService sectionNavigationService,
    ILogger<UnitAvailabilityService> logger) : IUnitAvailabilityService
{
    private const string AvailabilitySelector =
        "#ct-cards-list .availabilityinfo, .availabilityinfo";
    private const string ResourceSelector =
        "#ct-cards-list .ct-resource-list .ct-list .ct-list-title h3 a";

    private static readonly IReadOnlyDictionary<string, int> MonthNumbers =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["janeiro"] = 1,
            ["fevereiro"] = 2,
            ["marco"] = 3,
            ["abril"] = 4,
            ["maio"] = 5,
            ["junho"] = 6,
            ["julho"] = 7,
            ["agosto"] = 8,
            ["setembro"] = 9,
            ["outubro"] = 10,
            ["novembro"] = 11,
            ["dezembro"] = 12
        };

    public async Task<bool> IsAvailableAsync(
        IAutomationPage disciplinePage,
        UnidadeEnsino unit,
        IReadOnlyList<Section> sections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disciplinePage);
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(sections);

        if (sections.Count == 0)
        {
            logger.LogInformation(
                "Unidade {UnitName} indisponível: nenhuma seção foi encontrada",
                unit.Nome);
            return false;
        }

        logger.LogInformation(
            "Verificando disponibilidade de {UnitName} pela primeira seção {SectionName}",
            unit.Nome,
            sections[0].Nome);
        await sectionNavigationService.ExecuteAsync(
            disciplinePage,
            sections[0],
            cancellationToken);

        var availabilityMessages = disciplinePage.GetLocator(AvailabilitySelector);
        var messageCount = await availabilityMessages.CountAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Now);

        for (var index = 0; index < messageCount; index++)
        {
            var message = NormalizeWhitespace(
                await availabilityMessages.Nth(index).InnerTextAsync(cancellationToken));

            if (TryExtractAvailabilityDate(message, out var availableFrom))
            {
                logger.LogInformation(
                    "Data de disponibilidade encontrada em {UnitName}: {AvailableFrom:yyyy-MM-dd}; data atual: {Today:yyyy-MM-dd}",
                    unit.Nome,
                    availableFrom,
                    today);

                if (availableFrom > today)
                {
                    logger.LogInformation(
                        "Unidade {UnitName} ainda não disponível: {AvailabilityMessage}",
                        unit.Nome,
                        message);
                    return false;
                }
            }
            else if (IsBlockingMessage(message))
            {
                logger.LogInformation(
                    "Unidade {UnitName} indisponível: {AvailabilityMessage}",
                    unit.Nome,
                    message);
                return false;
            }
        }

        var resourceCount = await disciplinePage.GetLocator(ResourceSelector)
            .CountAsync(cancellationToken);
        var available = resourceCount > 0;

        logger.LogInformation(
            "Disponibilidade de {UnitName}: {Available}; recursos encontrados na primeira seção: {ResourceCount}",
            unit.Nome,
            available,
            resourceCount);

        return available;
    }

    private static bool TryExtractAvailabilityDate(
        string message,
        out DateOnly date)
    {
        date = default;
        var normalized = NormalizeForComparison(message);
        var match = AvailabilityDateRegex().Match(normalized);

        if (!match.Success ||
            !int.TryParse(match.Groups["day"].Value, out var day) ||
            !int.TryParse(match.Groups["year"].Value, out var year) ||
            !MonthNumbers.TryGetValue(match.Groups["month"].Value, out var month))
        {
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool IsBlockingMessage(string message)
    {
        var normalized = NormalizeForComparison(message);
        return normalized.Contains("nao disponivel", StringComparison.Ordinal) ||
               normalized.Contains("indisponivel", StringComparison.Ordinal) ||
               normalized.Contains("acesso restrito", StringComparison.Ordinal) ||
               normalized.Contains("bloquead", StringComparison.Ordinal);
    }

    private static string NormalizeForComparison(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return NormalizeWhitespace(builder.ToString().Normalize(NormalizationForm.FormC));
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            ' ',
            value.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    [GeneratedRegex(
        @"(?<day>\d{1,2})\s+(?:de\s+)?(?<month>janeiro|fevereiro|marco|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro)\s+(?:de\s+)?(?<year>\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AvailabilityDateRegex();
}
