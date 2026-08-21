using System.Text;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class UnitExplorerService(
    IOptions<ActivityDiscoveryOptions> options,
    ILogger<UnitExplorerService> logger) : IUnitExplorerService
{
    private const string SideMenuSelector = "#ctmenu";
    private const string UnitSelector =
        "li:has(> .timeline-item.group[data-ct-groupname^='Unidade de Ensino'])";
    private const string UnitGroupSelector =
        ":scope > .timeline-item.group[data-ct-groupname^='Unidade de Ensino']";
    private const string CompletionIndicatorSelector =
        ":scope > .timeline-status i.material-icons";
    private const string ToggleSelector = "a[aria-expanded]";
    private const string UnitNameAttribute = "data-ct-groupname";
    private const string ExpandedAttribute = "aria-expanded";

    private static readonly string InspectionDirectory = Path.Combine("logs", "inspection");
    private readonly ActivityDiscoveryOptions _options = options.Value;

    public async Task<List<UnidadeEnsino>> ExecuteAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disciplinePage);

        logger.LogInformation("UnitExplorerService iniciado em modo somente leitura");
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);

        if (_options.CompletionRenderDelay < 0)
        {
            throw new InvalidOperationException(
                "ActivityDiscovery:CompletionRenderDelay não pode ser negativo.");
        }

        logger.LogInformation(
            "Aguardando {RenderDelay} ms para o AVA atualizar a conclusão das unidades",
            _options.CompletionRenderDelay);
        await disciplinePage.WaitForClientRenderAsync(
            TimeSpan.FromMilliseconds(_options.CompletionRenderDelay),
            cancellationToken);

        var sideMenu = disciplinePage.GetLocator(SideMenuSelector);
        var menuCount = await sideMenu.CountAsync(cancellationToken);
        logger.LogInformation("Quantidade de menus laterais encontrados: {MenuCount}", menuCount);

        var unitLocators = sideMenu.GetLocator(UnitSelector);
        var unitCount = await unitLocators.CountAsync(cancellationToken);
        var units = new List<UnidadeEnsino>(unitCount);

        logger.LogInformation("Quantidade de unidades encontradas: {UnitCount}", unitCount);

        for (var index = 0; index < unitCount; index++)
        {
            var unitContainer = unitLocators.Nth(index);
            var unitLocator = unitContainer.GetLocator(UnitGroupSelector);
            var toggle = unitLocator.GetLocator(ToggleSelector);
            var name = await ExtractNameAsync(unitLocator, index, cancellationToken);
            var expanded = await IsExpandedAsync(toggle, cancellationToken);
            var completion = await ReadCompletionAsync(
                unitContainer.GetLocator(CompletionIndicatorSelector),
                cancellationToken);

            units.Add(new UnidadeEnsino
            {
                Nome = name,
                Toggle = toggle,
                Expandida = expanded,
                Concluida = completion.Completed,
                CompletionEvidence = completion.Evidence
            });

            logger.LogInformation(
                "Unidade encontrada: {UnitName}; expansão: {UnitState}; conclusão: {CompletionState}; evidência: {CompletionEvidence}",
                name,
                expanded ? "Expandida" : "Recolhida",
                completion.Completed ? "Concluída" : "Pendente",
                completion.Evidence);
        }

        await SaveInspectionAsync(disciplinePage, units, cancellationToken);
        return units;
    }

    private static async Task<string> ExtractNameAsync(
        IAutomationLocator unitLocator,
        int index,
        CancellationToken cancellationToken)
    {
        var attributeName = await unitLocator.GetAttributeAsync(
            UnitNameAttribute,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            return attributeName.Trim();
        }

        var text = await unitLocator.InnerTextAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? $"Unidade de Ensino {index + 1}"
            : text.Trim();
    }

    private static async Task<bool> IsExpandedAsync(
        IAutomationLocator toggle,
        CancellationToken cancellationToken)
    {
        if (await toggle.CountAsync(cancellationToken) == 0)
        {
            return false;
        }

        var expandedValue = await toggle.Nth(0).GetAttributeAsync(
            ExpandedAttribute,
            cancellationToken);

        return bool.TryParse(expandedValue, out var expanded) && expanded;
    }

    private static async Task<(bool Completed, string Evidence)> ReadCompletionAsync(
        IAutomationLocator indicator,
        CancellationToken cancellationToken)
    {
        if (await indicator.CountAsync(cancellationToken) == 0)
        {
            return (false, "indicador de conclusão ausente");
        }

        var classValue = await indicator.Nth(0).GetAttributeAsync(
            "class",
            cancellationToken) ?? string.Empty;
        var classes = classValue.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var completed = classes.Contains("complete", StringComparer.OrdinalIgnoreCase);

        return (completed, $"class={classValue}");
    }

    private async Task SaveInspectionAsync(
        IAutomationPage disciplinePage,
        IReadOnlyCollection<UnidadeEnsino> units,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(InspectionDirectory);

        var treePath = Path.Combine(InspectionDirectory, "unit-tree.txt");
        var htmlPath = Path.Combine(InspectionDirectory, "unit-home.html");
        var screenshotPath = Path.Combine(InspectionDirectory, "unit-home.png");

        await File.WriteAllTextAsync(
            treePath,
            BuildTree(units),
            Encoding.UTF8,
            cancellationToken);

        await File.WriteAllTextAsync(
            htmlPath,
            await disciplinePage.GetContentAsync(cancellationToken),
            Encoding.UTF8,
            cancellationToken);

        await disciplinePage.ScreenshotAsync(screenshotPath, cancellationToken);

        logger.LogInformation(
            "Inspeção das unidades salva em {TreePath}, {HtmlPath} e {ScreenshotPath}",
            Path.GetFullPath(treePath),
            Path.GetFullPath(htmlPath),
            Path.GetFullPath(screenshotPath));
    }

    private static string BuildTree(IEnumerable<UnidadeEnsino> units)
    {
        var unitList = units.ToList();
        var builder = new StringBuilder();
        builder.AppendLine($"UNIDADES: {unitList.Count}");
        builder.AppendLine();

        foreach (var unit in unitList)
        {
            builder.AppendLine($"UNIDADE: {unit.Nome}");
            builder.AppendLine($"ESTADO: {(unit.Expandida ? "Expandida" : "Recolhida")}");
            builder.AppendLine($"CONCLUSÃO: {(unit.Concluida ? "Concluída" : "Pendente")}");
            builder.AppendLine($"EVIDÊNCIA: {unit.CompletionEvidence}");
            builder.AppendLine();
        }

        if (unitList.Count == 0)
        {
            builder.AppendLine("Nenhuma Unidade de Ensino encontrada.");
        }

        return builder.ToString();
    }
}
