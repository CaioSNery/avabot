using System.Text;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using DomainActivity = AvaBot.Domain.Models.Activity;

namespace AvaBot.Application.Services;

public sealed class ActivityNavigationService(
    IActivityProgressStore progressStore,
    ILogger<ActivityNavigationService> logger) : IActivityNavigationService
{
    private const string SectionContentSelector = "#ct-cards-list";
    private static readonly TimeSpan PassiveContentVisitDuration =
        TimeSpan.FromSeconds(3);
    private static readonly string[] CompletedQuizSelectors =
    [
        "table:has-text('Finalizada')",
        ".quizattemptsummary:has-text('Finalizada')",
        "button:has-text('FAZER UMA OUTRA TENTATIVA')",
        "a:has-text('FAZER UMA OUTRA TENTATIVA')",
        "input[value*='FAZER UMA OUTRA TENTATIVA' i]"
    ];
    private static readonly string InspectionDirectory = Path.Combine("logs", "inspection");

    public async Task<ActivityNavigationResult> ExecuteAsync(
        IAutomationPage sectionPage,
        List<DomainActivity> activities,
        ActivityExecutionFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sectionPage);
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(filter);

        logger.LogInformation(
            "ActivityNavigationService iniciado no modo {ExecutionMode}",
            filter.Mode);

        var navigationCount = 0;
        var completedActivities = activities
            .Where(activity =>
                activity.CompletionStatus == ActivityCompletionStatus.Completed)
            .ToList();

        foreach (var completedActivity in completedActivities)
        {
            if (completedActivity.Tipo is ActivityType.ConteudoProfessor or
                ActivityType.MaterialInformativo or
                ActivityType.AulaPratica)
            {
                logger.LogInformation(
                    "Conteúdo já visualizado; avançando para o próximo: {ActivityName}; tipo: {ActivityType}",
                    completedActivity.Nome,
                    completedActivity.Tipo);
            }
            else
            {
                logger.LogInformation(
                    "Ignorando atividade já concluída: {ActivityName}; tipo: {ActivityType}",
                    completedActivity.Nome,
                    completedActivity.Tipo);
            }
        }

        var pendingActivities = activities.Where(filter.ShouldNavigate).ToList();
        logger.LogInformation(
            "Atividades elegíveis e ainda não concluídas: {PendingActivityCount}",
            pendingActivities.Count);

        try
        {
            foreach (var activity in pendingActivities)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (activity.CompletionStatus == ActivityCompletionStatus.Unknown &&
                    !ActivityExecutionFilter.IsEvaluative(activity.Tipo) &&
                    await progressStore.IsCompletedAsync(
                        activity.Identifier,
                        cancellationToken))
                {
                    logger.LogInformation(
                        "Progresso local atualizado durante a execução: {ActivityName} já foi visualizado; avançando para o próximo recurso",
                        activity.Nome);
                    continue;
                }

                logger.LogInformation("Atividade: {ActivityName}", activity.Nome);
                logger.LogInformation("Tipo: {ActivityType}", activity.Tipo);

                switch (activity.Tipo)
                {
                    case ActivityType.WebAula:
                    case ActivityType.VideoAulaResumo:
                    case ActivityType.ConteudoProfessor:
                    case ActivityType.MaterialInformativo:
                    case ActivityType.AulaPratica:
                        await OpenActivityAsync(sectionPage, activity, cancellationToken);
                        navigationCount++;

                        if (activity.Tipo is ActivityType.ConteudoProfessor or
                            ActivityType.MaterialInformativo or
                            ActivityType.AulaPratica)
                        {
                            logger.LogInformation(
                                "Aguardando {VisitSeconds} segundos no conteúdo {ActivityName}",
                                PassiveContentVisitDuration.TotalSeconds,
                                activity.Nome);
                            await sectionPage.WaitForClientRenderAsync(
                                PassiveContentVisitDuration,
                                cancellationToken);
                        }

                        logger.LogInformation(
                            "Conteúdo {ActivityType} aberto; retornando para a seção",
                            activity.Tipo);
                        await sectionPage.GoBackAsync(cancellationToken);
                        await sectionPage.WaitForDomContentLoadedAsync(cancellationToken);
                        logger.LogInformation("DOMContentLoaded concluído após o retorno");
                        await WaitForNetworkIdleAsync(
                            sectionPage,
                            activity,
                            "após o retorno à seção",
                            cancellationToken);
                        await sectionPage.WaitForVisibleAsync(
                            SectionContentSelector,
                            cancellationToken);
                        logger.LogInformation(
                            "Retorno para a seção confirmado. URL: {SectionUrl}",
                            sectionPage.Url);

                        await progressStore.MarkCompletedAsync(
                            activity.Identifier,
                            cancellationToken);
                        logger.LogInformation(
                            "Conteúdo {ActivityType} marcado como concluído no progresso local: {ActivityName}",
                            activity.Tipo,
                            activity.Nome);
                        logger.LogInformation(
                            "Visualização concluída; avançando para o próximo recurso da seção");

                        if (filter.StopAfterFirstNavigation)
                        {
                            return new ActivityNavigationResult
                            {
                                NavigationCount = navigationCount
                            };
                        }

                        break;

                    case ActivityType.AtividadeDiagnostica:
                    case ActivityType.AtividadeAprendizagem:
                    case ActivityType.AvaliacaoUnidade:
                    case ActivityType.Questionario:
                        await OpenActivityAsync(sectionPage, activity, cancellationToken);
                        navigationCount++;
                        await SaveInspectionAsync(sectionPage, cancellationToken);

                        if (await IsQuizAlreadyCompletedAsync(
                                sectionPage,
                                cancellationToken))
                        {
                            logger.LogInformation(
                                "Atividade já finalizada no AVA: {ActivityName}. Nenhuma nova tentativa será iniciada",
                                activity.Nome);
                            await progressStore.MarkCompletedAsync(
                                activity.Identifier,
                                cancellationToken);
                            await ReturnToSectionAsync(sectionPage, cancellationToken);
                            continue;
                        }

                        logger.LogInformation(
                            "Atividade pendente confirmada; página preparada para o QuestionExplorerService");

                        return new ActivityNavigationResult
                        {
                            OpenedActivity = activity,
                            PreparedForQuestions = true,
                            NavigationCount = navigationCount
                        };
                }
            }

            logger.LogInformation(
                "Nenhuma atividade avaliativa permaneceu aberta no modo {ExecutionMode}",
                filter.Mode);

            return new ActivityNavigationResult
            {
                NavigationCount = navigationCount
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Navegação de atividades cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha no ActivityNavigationService. Exceção completa: {ExceptionDetails}",
                exception.ToString());
            throw;
        }
        finally
        {
            logger.LogInformation(
                "ActivityNavigationService finalizado após {NavigationCount} navegação(ões)",
                navigationCount);
        }
    }

    private async Task<bool> IsQuizAlreadyCompletedAsync(
        IAutomationPage activityPage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Verificando se a atividade já possui tentativa finalizada");

        foreach (var selector in CompletedQuizSelectors)
        {
            var candidates = activityPage.GetLocator(selector);
            var count = await candidates.CountAsync(cancellationToken);

            for (var index = 0; index < count; index++)
            {
                if (await candidates.Nth(index).IsVisibleAsync(cancellationToken))
                {
                    logger.LogInformation(
                        "Tentativa finalizada detectada pelo indicador {CompletionSelector}",
                        selector);
                    return true;
                }
            }
        }

        logger.LogInformation("Nenhuma tentativa finalizada foi detectada");
        return false;
    }

    private async Task ReturnToSectionAsync(
        IAutomationPage activityPage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Retornando à seção para procurar a próxima atividade pendente");
        await activityPage.GoBackAsync(cancellationToken);
        await activityPage.WaitForDomContentLoadedAsync(cancellationToken);
        await activityPage.WaitForNetworkIdleAsync(cancellationToken);
        await activityPage.WaitForVisibleAsync(
            SectionContentSelector,
            cancellationToken);
        logger.LogInformation(
            "Retorno à seção concluído. URL: {SectionUrl}",
            activityPage.Url);
    }

    private async Task OpenActivityAsync(
        IAutomationPage sectionPage,
        DomainActivity activity,
        CancellationToken cancellationToken)
    {
        var previousUrl = sectionPage.Url;

        logger.LogInformation("Abrindo atividade {ActivityName}", activity.Nome);
        await activity.Locator.ClickAsync(cancellationToken);
        await sectionPage.WaitForUrlChangeAsync(previousUrl, cancellationToken);
        logger.LogInformation(
            "Mudança de URL confirmou a abertura de {ActivityName}: {PreviousUrl} -> {CurrentUrl}",
            activity.Nome,
            previousUrl,
            sectionPage.Url);

        await sectionPage.WaitForDomContentLoadedAsync(cancellationToken);
        logger.LogInformation("DOMContentLoaded concluído");

        await WaitForNetworkIdleAsync(
            sectionPage,
            activity,
            "durante a abertura",
            cancellationToken);

        logger.LogInformation("URL: {ActivityUrl}", sectionPage.Url);
        logger.LogInformation(
            "Título: {ActivityTitle}",
            await sectionPage.GetTitleAsync(cancellationToken));
        logger.LogInformation("Abertura da atividade confirmada");
    }

    private async Task WaitForNetworkIdleAsync(
        IAutomationPage page,
        DomainActivity activity,
        string stage,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForNetworkIdleAsync(cancellationToken);
            logger.LogInformation("NetworkIdle concluído {Stage}", stage);
        }
        catch (TimeoutException exception)
            when (!cancellationToken.IsCancellationRequested &&
                  activity.Tipo is (ActivityType.WebAula or
                      ActivityType.VideoAulaResumo or
                      ActivityType.ConteudoProfessor or
                      ActivityType.MaterialInformativo or
                      ActivityType.AulaPratica))
        {
            logger.LogWarning(
                exception,
                "NetworkIdle não foi alcançado {Stage} do conteúdo {ActivityName}; " +
                "DOMContentLoaded foi concluído; o fluxo continuará com a confirmação da página",
                stage,
                activity.Nome);
        }
    }

    private async Task SaveInspectionAsync(
        IAutomationPage activityPage,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(InspectionDirectory);

        var htmlPath = Path.Combine(InspectionDirectory, "activity-navigation.html");
        var screenshotPath = Path.Combine(InspectionDirectory, "activity-navigation.png");

        await File.WriteAllTextAsync(
            htmlPath,
            await activityPage.GetContentAsync(cancellationToken),
            Encoding.UTF8,
            cancellationToken);

        await activityPage.ScreenshotAsync(screenshotPath, cancellationToken);

        logger.LogInformation(
            "Inspeção da navegação salva em {HtmlPath} e {ScreenshotPath}",
            Path.GetFullPath(htmlPath),
            Path.GetFullPath(screenshotPath));
    }
}
