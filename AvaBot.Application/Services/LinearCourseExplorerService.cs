using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class LinearCourseExplorerService(
    IOptions<ActivityDiscoveryOptions> options,
    ILogger<LinearCourseExplorerService> logger) : ILinearCourseExplorerService
{
    private const string TopicContainerSelector =
        "#ctmenu li:has(> .timeline-item:not(.group) > a[href*='course/view.php'][href*='topic=']:not([href$='#'])), " +
        "#ctmenu li:has(> .timeline-item.group):has(> .timeline-menu a[href*='course/view.php'][href*='topic=']:not([href$='#']))";
    private const string TopicLinkSelector =
        ":scope > .timeline-item:not(.group) > a[href*='course/view.php'][href*='topic=']:not([href$='#']), " +
        ":scope > .timeline-menu a[href*='course/view.php'][href*='topic=']:not([href$='#'])";
    private const string CompletionIndicatorSelector =
        ":scope > .timeline-status i.material-icons";
    private readonly ActivityDiscoveryOptions _options = options.Value;

    public async Task<List<Section>> ExecuteAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disciplinePage);

        logger.LogInformation("Mapeando curso no formato de tópicos lineares");
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);
        await disciplinePage.WaitForVisibleAsync("#ctmenu", cancellationToken);

        if (_options.CompletionRenderDelay < 0)
        {
            throw new InvalidOperationException(
                "ActivityDiscovery:CompletionRenderDelay não pode ser negativo.");
        }

        logger.LogInformation(
            "Aguardando {RenderDelay} ms para o AVA atualizar a conclusão dos tópicos lineares",
            _options.CompletionRenderDelay);
        await disciplinePage.WaitForClientRenderAsync(
            TimeSpan.FromMilliseconds(_options.CompletionRenderDelay),
            cancellationToken);

        var topicContainers = disciplinePage.GetLocator(TopicContainerSelector);
        var locatorCount = await topicContainers.CountAsync(cancellationToken);
        var topics = new List<Section>(locatorCount);
        var mappedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < locatorCount; index++)
        {
            var topicContainer = topicContainers.Nth(index);
            var completion = await ReadCompletionAsync(
                topicContainer.GetLocator(CompletionIndicatorSelector),
                cancellationToken);
            var topicLinks = topicContainer.GetLocator(TopicLinkSelector);
            var topicLinkCount = await topicLinks.CountAsync(cancellationToken);

            logger.LogInformation(
                "Item linear {ItemIndex} contém {ChildTopicCount} tópico(s) navegável(is)",
                index + 1,
                topicLinkCount);

            for (var linkIndex = 0; linkIndex < topicLinkCount; linkIndex++)
            {
                var topicLocator = topicLinks.Nth(linkIndex);
                var visible = await topicLocator.IsVisibleAsync(cancellationToken);
                var name = NormalizeWhitespace(
                    await topicLocator.InnerTextAsync(cancellationToken));
                var href = await topicLocator.GetAttributeAsync("href", cancellationToken);

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(href) ||
                    href.EndsWith('#'))
                {
                    continue;
                }

                var url = ResolveUrl(disciplinePage.Url, href);
                if (!mappedUrls.Add(url))
                {
                    continue;
                }

                var passiveOnly = !IsEvaluativeTopic(name);
                topics.Add(new Section
                {
                    Unidade = "Curso linear",
                    Nome = name,
                    Url = url,
                    Concluida = completion.Completed,
                    CompletionEvidence = completion.Evidence,
                    PassiveOnly = passiveOnly,
                    AccessLocator = topicLocator
                });

                logger.LogInformation(
                    "Tópico linear encontrado: {TopicName}; URL: {TopicUrl}; visível no menu: {IsVisible}; modo: {TopicMode}; conclusão: {CompletionState}; evidência: {CompletionEvidence}",
                    name,
                    url,
                    visible,
                    passiveOnly ? "Visualização passiva" : "Atividade avaliativa",
                    completion.Completed ? "Concluído" : "Pendente",
                    completion.Evidence);
            }
        }

        logger.LogInformation(
            "Curso linear mapeado com {TopicCount} tópico(s)",
            topics.Count);

        return topics;
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

    private static bool IsEvaluativeTopic(string topicName) =>
        topicName.Contains(
            "Atividade Avaliativa",
            StringComparison.OrdinalIgnoreCase) ||
        topicName.Contains(
            "Testando seu Conhecimento",
            StringComparison.OrdinalIgnoreCase);

    private static string ResolveUrl(string pageUrl, string href) =>
        Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri.ToString()
            : new Uri(new Uri(pageUrl), href).ToString();

    private static string NormalizeWhitespace(string value) =>
        string.Join(
            ' ',
            value.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
}
