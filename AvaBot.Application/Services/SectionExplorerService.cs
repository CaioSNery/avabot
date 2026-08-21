using System.Text;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class SectionExplorerService(
    ILogger<SectionExplorerService> logger) : ISectionExplorerService
{
    private const string UnitContainerSelector = "xpath=ancestor::li[1]";
    private const string SectionSelector = ".timeline-menu li > a";
    private static readonly string InspectionDirectory = Path.Combine("logs", "inspection");

    public async Task<List<Section>> ExecuteAsync(
        IAutomationPage disciplinePage,
        UnidadeEnsino expandedUnit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disciplinePage);
        ArgumentNullException.ThrowIfNull(expandedUnit);

        if (!expandedUnit.Expandida)
        {
            throw new InvalidOperationException(
                $"A unidade '{expandedUnit.Nome}' precisa estar expandida antes de mapear suas seções.");
        }

        logger.LogInformation(
            "SectionExplorerService iniciado para {UnitName}",
            expandedUnit.Nome);

        var sectionLocators = expandedUnit.Toggle
            .GetLocator(UnitContainerSelector)
            .GetLocator(SectionSelector);
        var sectionCount = await sectionLocators.CountAsync(cancellationToken);
        var sections = new List<Section>(sectionCount);

        logger.LogInformation(
            "Quantidade de seções encontradas em {UnitName}: {SectionCount}",
            expandedUnit.Nome,
            sectionCount);

        for (var index = 0; index < sectionCount; index++)
        {
            var accessLocator = sectionLocators.Nth(index);
            var name = NormalizeText(await accessLocator.InnerTextAsync(cancellationToken));
            var url = await accessLocator.GetAttributeAsync("href", cancellationToken)
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Seção {index + 1}";
            }

            var section = new Section
            {
                Unidade = expandedUnit.Nome,
                Nome = name,
                Url = url,
                AccessLocator = accessLocator
            };

            sections.Add(section);
            logger.LogInformation("Seção encontrada: {SectionName}", name);
        }

        expandedUnit.Sections.Clear();
        expandedUnit.Sections.AddRange(sections);

        await SaveInspectionAsync(
            disciplinePage,
            expandedUnit,
            cancellationToken);

        return sections;
    }

    private async Task SaveInspectionAsync(
        IAutomationPage disciplinePage,
        UnidadeEnsino unit,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(InspectionDirectory);

        var htmlPath = Path.Combine(InspectionDirectory, "section-home.html");
        var screenshotPath = Path.Combine(InspectionDirectory, "section-home.png");
        var treePath = Path.Combine(InspectionDirectory, "section-tree.txt");

        await File.WriteAllTextAsync(
            htmlPath,
            await disciplinePage.GetContentAsync(cancellationToken),
            Encoding.UTF8,
            cancellationToken);

        await disciplinePage.ScreenshotAsync(screenshotPath, cancellationToken);

        await File.WriteAllTextAsync(
            treePath,
            BuildTree(unit),
            Encoding.UTF8,
            cancellationToken);

        logger.LogInformation(
            "Inspeção das seções salva em {HtmlPath}, {ScreenshotPath} e {TreePath}",
            Path.GetFullPath(htmlPath),
            Path.GetFullPath(screenshotPath),
            Path.GetFullPath(treePath));
    }

    private static string BuildTree(UnidadeEnsino unit)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"UNIDADE: {unit.Nome}");
        builder.AppendLine($"ESTADO: {(unit.Expandida ? "Expandida" : "Recolhida")}");
        builder.AppendLine($"SEÇÕES: {unit.Sections.Count}");
        builder.AppendLine();

        foreach (var section in unit.Sections)
        {
            builder.AppendLine($"  SEÇÃO: {section.Nome}");
        }

        if (unit.Sections.Count == 0)
        {
            builder.AppendLine("  Nenhuma seção encontrada.");
        }

        return builder.ToString();
    }

    private static string NormalizeText(string text) =>
        string.Join(
            ' ',
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
