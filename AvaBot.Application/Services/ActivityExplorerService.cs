using System.Globalization;
using System.Text;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ActivityModel = AvaBot.Domain.Models.Activity;

namespace AvaBot.Application.Services;

public sealed class ActivityExplorerService(
    IActivityProgressStore progressStore,
    IOptions<ActivityDiscoveryOptions> options,
    ILogger<ActivityExplorerService> logger) : IActivityExplorerService
{
    private const string ResourceListSelector =
        "#ct-cards-list .ct-resource-list:has(.ct-list)";
    private const string ContentItemSelector = ".ct-list";
    private const string ActivitySelector = ".ct-list-title h3 a";
    private const string CompletionIndicatorSelector = ".ct-list-title h3 i[completion]";

    private static readonly string InspectionDirectory = Path.Combine("logs", "inspection");
    private readonly ActivityDiscoveryOptions _options = options.Value;

    public async Task<List<ActivityModel>> ExecuteAsync(
        IAutomationPage sectionPage,
        Section section,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sectionPage);
        ArgumentNullException.ThrowIfNull(section);

        logger.LogInformation(
            "ActivityExplorerService iniciado para a seção {SectionName}",
            section.Nome);

        await sectionPage.WaitForDomContentLoadedAsync(cancellationToken);
        await sectionPage.WaitForVisibleAsync("#ct-cards-list", cancellationToken);

        if (_options.CompletionRenderDelay < 0)
        {
            throw new InvalidOperationException(
                "ActivityDiscovery:CompletionRenderDelay não pode ser negativo.");
        }

        logger.LogInformation(
            "Aguardando {RenderDelay} ms para o AVA atualizar os estados de conclusão",
            _options.CompletionRenderDelay);
        await sectionPage.WaitForClientRenderAsync(
            TimeSpan.FromMilliseconds(_options.CompletionRenderDelay),
            cancellationToken);
        logger.LogInformation("Estados de conclusão prontos para leitura");

        var resourceLists = sectionPage.GetLocator(ResourceListSelector);
        var resourceListCount = await resourceLists.CountAsync(cancellationToken);
        var activities = new List<ActivityModel>();

        for (var resourceIndex = 0; resourceIndex < resourceListCount; resourceIndex++)
        {
            var resourceList = resourceLists.Nth(resourceIndex);
            var currentCategory = TryExtractCategoryName(
                await resourceList.InnerTextAsync(cancellationToken)) ?? "Sem categoria";
            var contentItems = resourceList.GetLocator(ContentItemSelector);
            var contentItemCount = await contentItems.CountAsync(cancellationToken);

            for (var itemIndex = 0; itemIndex < contentItemCount; itemIndex++)
            {
                var contentItem = contentItems.Nth(itemIndex);
                var activityLocators = contentItem.GetLocator(ActivitySelector);
                var activityCount = await activityLocators.CountAsync(cancellationToken);

                if (activityCount == 0)
                {
                    var categoryMarker = TryExtractCategoryName(
                        await contentItem.InnerTextAsync(cancellationToken));

                    if (categoryMarker is not null)
                    {
                        currentCategory = categoryMarker;
                    }

                    continue;
                }

                for (var activityIndex = 0; activityIndex < activityCount; activityIndex++)
                {
                    var activityLocator = activityLocators.Nth(activityIndex);
                    var activityName = NormalizeWhitespace(
                        await activityLocator.InnerTextAsync(cancellationToken));
                    var activityHref = await activityLocator.GetAttributeAsync(
                        "href",
                        cancellationToken);
                    var detectedActivityType = IdentifyType(
                        activityName,
                        currentCategory,
                        activityHref);
                    var activityType = ApplySectionMode(
                        detectedActivityType,
                        section.PassiveOnly);
                    var activityIdentifier = BuildIdentifier(
                        activityHref,
                        currentCategory,
                        activityName);
                    var completionIndicator = contentItem.GetLocator(
                        CompletionIndicatorSelector).Nth(activityIndex);
                    var completionState = await CompletionStatusReader.ReadAsync(
                        completionIndicator,
                        cancellationToken);

                    var locallyCompleted = await progressStore.IsCompletedAsync(
                        activityIdentifier,
                        cancellationToken);

                    if (locallyCompleted &&
                        CanUseLocalCompletion(
                            activityType,
                            completionState.Status))
                    {
                        completionState = (
                            ActivityCompletionStatus.Completed,
                            "registro local de conclusão");
                    }
                    else if (locallyCompleted &&
                             completionState.Status == ActivityCompletionStatus.Pending)
                    {
                        logger.LogInformation(
                            "O AVA informa que {ActivityName} está pendente; o registro local não será usado para ignorar o recurso",
                            activityName);
                    }
                    else if (locallyCompleted &&
                             ActivityExecutionFilter.IsEvaluative(activityType))
                    {
                        logger.LogInformation(
                            "Registro local encontrado para a atividade avaliativa {ActivityName}; a conclusão será confirmada na página da tentativa",
                            activityName);
                    }
                    else if (completionState.Status == ActivityCompletionStatus.Completed)
                    {
                        await progressStore.MarkCompletedAsync(
                            activityIdentifier,
                            cancellationToken);
                    }

                    activities.Add(new ActivityModel
                    {
                        Categoria = currentCategory,
                        Nome = activityName,
                        Identifier = activityIdentifier,
                        Tipo = activityType,
                        CompletionStatus = completionState.Status,
                        CompletionEvidence = completionState.Evidence,
                        Locator = activityLocator
                    });
                }
            }
        }

        var categoryGroups = activities.GroupBy(activity => activity.Categoria).ToList();
        logger.LogInformation(
            "Quantidade de categorias encontradas: {CategoryCount}",
            categoryGroups.Count);

        foreach (var categoryGroup in categoryGroups)
        {
            logger.LogInformation("Categoria encontrada: {CategoryName}", categoryGroup.Key);
            logger.LogInformation(
                "Quantidade de atividades na categoria: {ActivityCount}",
                categoryGroup.Count());

            foreach (var activity in categoryGroup)
            {
                logger.LogInformation("Atividade encontrada: {ActivityName}", activity.Nome);
                logger.LogInformation("Tipo: {ActivityType}", activity.Tipo);
                logger.LogInformation(
                    "Estado: {CompletionStatus}; evidência: {CompletionEvidence}",
                    activity.CompletionStatus,
                    activity.CompletionEvidence);
            }
        }

        section.Activities.Clear();
        section.Activities.AddRange(activities);

        logger.LogInformation(
            "Total de atividades encontradas: {ActivityCount}",
            activities.Count);
        logger.LogInformation(
            "Atividades concluídas: {CompletedCount}; pendentes: {PendingCount}; estado desconhecido: {UnknownCount}",
            activities.Count(activity =>
                activity.CompletionStatus == ActivityCompletionStatus.Completed),
            activities.Count(activity =>
                activity.CompletionStatus == ActivityCompletionStatus.Pending),
            activities.Count(activity =>
                activity.CompletionStatus == ActivityCompletionStatus.Unknown));

        await SaveInspectionAsync(
            sectionPage,
            section,
            activities,
            cancellationToken);

        logger.LogInformation("ActivityExplorerService concluído");
        return activities;
    }

    private static string BuildIdentifier(
        string? href,
        string category,
        string activityName) =>
        !string.IsNullOrWhiteSpace(href)
            ? href.Trim()
            : $"{category.Trim()}::{activityName.Trim()}";

    private static string? TryExtractCategoryName(string text)
    {
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeWhitespace)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        foreach (var category in new[] { "Pré-Aula", "Pós-Aula", "Aula Prática", "Aula" })
        {
            var match = lines.FirstOrDefault(line =>
                NormalizeForComparison(line).Equals(
                    NormalizeForComparison(category),
                    StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    internal static ActivityType IdentifyType(
        string activityName,
        string category,
        string? href)
    {
        var normalized = NormalizeForComparison(activityName);
        var normalizedCategory = NormalizeForComparison(category);

        if (normalizedCategory.Contains("aula pratica", StringComparison.Ordinal))
        {
            return ActivityType.AulaPratica;
        }

        if (normalized.Contains("postado pelo professor", StringComparison.Ordinal))
        {
            return ActivityType.ConteudoProfessor;
        }

        if (normalized.Contains("webaula", StringComparison.Ordinal) ||
            normalized.Contains("web aula", StringComparison.Ordinal))
        {
            return ActivityType.WebAula;
        }

        if (normalized.Contains("video aula resumo", StringComparison.Ordinal) ||
            normalized.Contains("videoaula resumo", StringComparison.Ordinal))
        {
            return ActivityType.VideoAulaResumo;
        }

        if (normalized.Contains("atividade diagnostica", StringComparison.Ordinal))
        {
            return ActivityType.AtividadeDiagnostica;
        }

        if (normalized.Contains("atividade de aprendizagem", StringComparison.Ordinal) ||
            normalized.Contains("atividade aprendizagem", StringComparison.Ordinal))
        {
            return ActivityType.AtividadeAprendizagem;
        }

        if (normalized.Contains("avaliacao da unidade", StringComparison.Ordinal) ||
            normalized.Contains("avaliacao de unidade", StringComparison.Ordinal))
        {
            return ActivityType.AvaliacaoUnidade;
        }

        if (href?.Contains("/mod/quiz/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ActivityType.Questionario;
        }

        if (normalized.Contains("certificado", StringComparison.Ordinal))
        {
            return ActivityType.Outro;
        }

        if (normalized.Equals("manual", StringComparison.Ordinal) ||
            normalized.Contains("manual do", StringComparison.Ordinal) ||
            normalized.Contains("livro digital didatico", StringComparison.Ordinal))
        {
            return ActivityType.MaterialInformativo;
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? ActivityType.Unknown
            : ActivityType.MaterialInformativo;
    }

    internal static bool CanUseLocalCompletion(
        ActivityType activityType,
        ActivityCompletionStatus avaStatus) =>
        avaStatus == ActivityCompletionStatus.Unknown &&
        !ActivityExecutionFilter.IsEvaluative(activityType);

    internal static ActivityType ApplySectionMode(
        ActivityType detectedActivityType,
        bool passiveOnly) =>
        passiveOnly &&
        detectedActivityType != ActivityType.Unknown &&
        !ActivityExecutionFilter.IsEvaluative(detectedActivityType)
            ? ActivityType.MaterialInformativo
            : detectedActivityType;

    private async Task SaveInspectionAsync(
        IAutomationPage sectionPage,
        Section section,
        IReadOnlyCollection<ActivityModel> activities,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(InspectionDirectory);

        var htmlPath = Path.Combine(InspectionDirectory, "activity-home.html");
        var screenshotPath = Path.Combine(InspectionDirectory, "activity-home.png");
        var treePath = Path.Combine(InspectionDirectory, "activity-tree.txt");

        await File.WriteAllTextAsync(
            htmlPath,
            await sectionPage.GetContentAsync(cancellationToken),
            Encoding.UTF8,
            cancellationToken);

        await sectionPage.ScreenshotAsync(screenshotPath, cancellationToken);

        await File.WriteAllTextAsync(
            treePath,
            BuildTree(section, activities),
            Encoding.UTF8,
            cancellationToken);

        logger.LogInformation(
            "Inspeção das atividades salva em {HtmlPath}, {ScreenshotPath} e {TreePath}",
            Path.GetFullPath(htmlPath),
            Path.GetFullPath(screenshotPath),
            Path.GetFullPath(treePath));
    }

    private static string BuildTree(
        Section section,
        IEnumerable<ActivityModel> activities)
    {
        var activityList = activities.ToList();
        var builder = new StringBuilder();
        builder.AppendLine($"SEÇÃO: {section.Nome}");
        builder.AppendLine($"ATIVIDADES: {activityList.Count}");
        builder.AppendLine();

        foreach (var categoryGroup in activityList.GroupBy(activity => activity.Categoria))
        {
            builder.AppendLine($"CATEGORIA: {categoryGroup.Key}");

            foreach (var activity in categoryGroup)
            {
                builder.AppendLine($"  ATIVIDADE: {activity.Nome}");
                builder.AppendLine($"  TIPO: {activity.Tipo}");
                builder.AppendLine($"  ESTADO: {activity.CompletionStatus}");
                builder.AppendLine($"  EVIDÊNCIA: {activity.CompletionEvidence}");
            }

            builder.AppendLine();
        }

        if (activityList.Count == 0)
        {
            builder.AppendLine("Nenhuma atividade encontrada.");
        }

        return builder.ToString();
    }

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            ' ',
            value.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string NormalizeForComparison(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return NormalizeWhitespace(builder.ToString().Normalize(NormalizationForm.FormC));
    }
}
