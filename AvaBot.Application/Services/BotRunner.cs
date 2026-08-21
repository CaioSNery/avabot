using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using AvaBot.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class BotRunner(
    IBrowserService browserService,
    IAuthenticationFlowService authenticationFlowService,
    IPageReadyService pageReadyService,
    IModalService modalService,
    IDashboardService dashboardService,
    IPortalNavigationService portalNavigationService,
    IAvaAuthenticationService avaAuthenticationService,
    IAvaExplorerService avaExplorerService,
    IDisciplinaNavigationService disciplinaNavigationService,
    ICookieBannerService cookieBannerService,
    IDidacticBookService didacticBookService,
    IUnitExplorerService unitExplorerService,
    IUnitNavigationService unitNavigationService,
    IUnitAvailabilityService unitAvailabilityService,
    ILinearCourseExplorerService linearCourseExplorerService,
    DisciplineExecutionPolicy disciplineExecutionPolicy,
    ISectionExplorerService sectionExplorerService,
    ISectionNavigationService sectionNavigationService,
    IActivityExplorerService activityExplorerService,
    IActivityNavigationService activityNavigationService,
    IQuestionConsoleFlow questionConsoleFlow,
    IInferenceConsoleFlow inferenceConsoleFlow,
    IAnswerFillService answerFillService,
    IManualQuizNavigationService manualQuizNavigationService,
    IQuizSubmissionService quizSubmissionService,
    IActivityProgressStore activityProgressStore,
    IPipelinePauseService pipelinePauseService,
    IOptions<PortalOptions> options,
    IOptions<ActivityNavigationOptions> activityNavigationOptions,
    IOptions<AnswerFillOptions> answerFillOptions,
    ILogger<BotRunner> logger) : IBotRunner
{
    private readonly PortalOptions _options = options.Value;
    private readonly ActivityNavigationOptions _activityNavigationOptions =
        activityNavigationOptions.Value;
    private readonly AnswerFillOptions _answerFillOptions = answerFillOptions.Value;

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!IsValidUrl(_options.Url))
        {
            logger.LogError("Portal:Url deve ser uma URL HTTP ou HTTPS absoluta.");
            return 1;
        }

        try
        {
            var result = await browserService.ExecuteAsync(
                _options.Url,
                ExecutePortalFlowAsync,
                cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("{ResultMessage}", result.Message);
                return 0;
            }

            logger.LogError("{ResultMessage}", result.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Execução do bot cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha durante a execução do bot. Exceção completa: {ExceptionDetails}",
                exception.ToString());
            return 1;
        }
    }

    private async Task<OperationResult> ExecutePortalFlowAsync(
        IAutomationPage portalPage,
        CancellationToken cancellationToken)
    {
        var authenticationResult = await authenticationFlowService.ExecuteAsync(
            portalPage,
            cancellationToken);

        if (!authenticationResult.IsSuccess)
        {
            return authenticationResult;
        }

        logger.LogInformation("Autenticação concluída; aguardando interface final do portal");
        await pageReadyService.WaitForPortalAsync(portalPage, cancellationToken);

        logger.LogInformation("Interface do portal pronta; iniciando ModalService");
        await modalService.CloseAllModalsAsync(portalPage, cancellationToken);

        logger.LogInformation("ModalService concluído; iniciando DashboardService");
        await dashboardService.WaitUntilReadyAsync(portalPage, cancellationToken);

        logger.LogInformation("DashboardService concluído; iniciando transição para o AVA");
        var avaPage = await portalNavigationService.NavigateToAvaAsync(
            portalPage,
            cancellationToken);

        logger.LogInformation(
            "BotRunner transferiu o fluxo para a aba do AVA em {AvaUrl}; a página do Portal não será mais utilizada",
            avaPage.Url);

        logger.LogInformation("Iniciando AvaAuthenticationService");
        await avaAuthenticationService.AuthenticateAsync(avaPage, cancellationToken);
        logger.LogInformation("AvaAuthenticationService concluído");

        logger.LogInformation("Iniciando AvaExplorerService");
        var disciplines = await avaExplorerService.ExecuteAsync(avaPage, cancellationToken);
        logger.LogInformation(
            "AvaExplorerService concluído com {DisciplineCount} disciplina(s)",
            disciplines.Count);

        var disciplinesPageUrl = avaPage.Url;
        var plannedDisciplineNames = disciplines
            .Select(discipline => discipline.Nome)
            .ToArray();
        var processedDisciplineCount = 0;
        var processedUnitCount = 0;
        var processedSectionCount = 0;
        var totalActivityCount = 0;
        var skippedDisciplineNames = new List<string>();

        foreach (var disciplineName in plannedDisciplineNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(
                    avaPage.Url,
                    disciplinesPageUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                await ReturnToDisciplinesAsync(
                    avaPage,
                    disciplinesPageUrl,
                    cancellationToken);
            }

            var currentDisciplines = await avaExplorerService.ExecuteAsync(
                avaPage,
                cancellationToken);
            var discipline = currentDisciplines.FirstOrDefault(item =>
                string.Equals(
                    item.Nome,
                    disciplineName,
                    StringComparison.OrdinalIgnoreCase));

            if (discipline is null)
            {
                logger.LogWarning(
                    "Disciplina {DisciplineName} não foi encontrada após remapear a página; seguindo para a próxima",
                    disciplineName);
                continue;
            }

            if (disciplineExecutionPolicy.ShouldSkipWithoutOpening(discipline.Nome))
            {
                skippedDisciplineNames.Add(discipline.Nome);
                logger.LogInformation(
                    "Disciplina excluída da automação e mantida fechada: {DisciplineName}. O fluxo continuará nas disciplinas seguintes",
                    discipline.Nome);
                continue;
            }

            logger.LogInformation(
                "Iniciando disciplina {DisciplineIndex}/{DisciplineCount}: {DisciplineName}",
                processedDisciplineCount + 1,
                plannedDisciplineNames.Length,
                discipline.Nome);
            await disciplinaNavigationService.ExecuteAsync(
                avaPage,
                discipline,
                cancellationToken);

            var disciplineResult = await ProcessDisciplineAsync(
                avaPage,
                discipline,
                cancellationToken);
            processedDisciplineCount++;
            processedUnitCount += disciplineResult.ProcessedUnits;
            processedSectionCount += disciplineResult.ProcessedSections;
            totalActivityCount += disciplineResult.ActivityCount;

            await ReturnToDisciplinesAsync(
                avaPage,
                disciplinesPageUrl,
                cancellationToken);

            if (_activityNavigationOptions.Mode == ActivityExecutionMode.Teste)
            {
                logger.LogInformation(
                    "Modo Teste ativo; encerrando após a primeira disciplina processada");
                break;
            }
        }

        if (skippedDisciplineNames.Count > 0)
        {
            logger.LogInformation(
                "Disciplinas excluídas e não abertas: {SkippedDisciplines}",
                string.Join(", ", skippedDisciplineNames));
        }

        await pipelinePauseService.WaitAsync(
            avaPage,
            nameof(ActivityNavigationService),
            cancellationToken);

        return OperationResult.Success(
            $"Fluxo concluído em {processedDisciplineCount} disciplina(s), " +
            $"{processedUnitCount} unidade(s), {processedSectionCount} seção(ões) e " +
            $"{totalActivityCount} atividade(s) encontradas. " +
            $"{skippedDisciplineNames.Count} Projeto(s) de Extensão permaneceram fechados.");
    }

    private async Task<DisciplineProcessingResult> ProcessDisciplineAsync(
        IAutomationPage avaPage,
        Disciplina discipline,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Iniciando CookieBannerService");
        await cookieBannerService.CloseIfVisibleAsync(avaPage, cancellationToken);

        var standardUnitCount = await avaPage
            .GetLocator(
                "#ctmenu .timeline-item.group[data-ct-groupname^='Unidade de Ensino']")
            .CountAsync(cancellationToken);

        if (standardUnitCount == 0)
        {
            logger.LogInformation(
                "Disciplina {DisciplineName} usa o formato alternativo de tópicos; o fluxo padrão de unidades permanecerá inalterado",
                discipline.Nome);
            return await ProcessLinearDisciplineAsync(
                avaPage,
                discipline,
                cancellationToken);
        }

        logger.LogInformation("Iniciando processamento dos livros didáticos");
        var visitedBookCount = await didacticBookService.VisitPendingAsync(
            avaPage,
            cancellationToken);
        logger.LogInformation(
            "Livros didáticos concluídos em {DisciplineName}; visitados nesta execução: {VisitedBookCount}",
            discipline.Nome,
            visitedBookCount);

        var units = await unitExplorerService.ExecuteAsync(
            avaPage,
            cancellationToken);
        logger.LogInformation(
            "Disciplina {DisciplineName}: {UnitCount} unidade(s) mapeada(s)",
            discipline.Nome,
            units.Count);

        var processedUnits = 0;
        var processedSections = 0;
        var activityCount = 0;

        foreach (var unit in units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unit.Concluida)
            {
                processedUnits++;
                logger.LogInformation(
                    "Unidade já concluída no AVA; todas as seções serão ignoradas: {UnitName}. Evidência: {CompletionEvidence}",
                    unit.Nome,
                    unit.CompletionEvidence);
                continue;
            }

            logger.LogInformation(
                "Verificando unidade {UnitIndex}/{UnitCount}: {UnitName}",
                processedUnits + 1,
                units.Count,
                unit.Nome);

            var expandedUnit = await unitNavigationService.ExecuteAsync(
                unit,
                cancellationToken);
            var sections = await sectionExplorerService.ExecuteAsync(
                avaPage,
                expandedUnit,
                cancellationToken);
            var unitAvailable = await unitAvailabilityService.IsAvailableAsync(
                avaPage,
                expandedUnit,
                sections,
                cancellationToken);

            if (!unitAvailable)
            {
                logger.LogInformation(
                    "Unidade {UnitName} ainda não disponível. Encerrando {DisciplineName} e retornando à página de disciplinas",
                    unit.Nome,
                    discipline.Nome);
                break;
            }

            processedUnits++;

            foreach (var section in sections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.LogInformation(
                    "Processando {SectionName} da unidade {UnitName}",
                    section.Nome,
                    unit.Nome);
                var openedSection = await sectionNavigationService.ExecuteAsync(
                    avaPage,
                    section,
                    cancellationToken);
                var sectionUrl = avaPage.Url;
                var sectionActivityResult = await ProcessSectionActivitiesAsync(
                    avaPage,
                    openedSection,
                    sectionUrl,
                    discipline.Nome,
                    cancellationToken);
                activityCount += sectionActivityResult.ActivityCount;
                processedSections++;

                logger.LogInformation(
                    "Seção {SectionName} concluída",
                    openedSection.Nome);

                if (_activityNavigationOptions.Mode == ActivityExecutionMode.Teste)
                {
                    return new DisciplineProcessingResult(
                        processedUnits,
                        processedSections,
                        activityCount);
                }
            }
        }

        return new DisciplineProcessingResult(
            processedUnits,
            processedSections,
            activityCount);
    }

    private async Task<DisciplineProcessingResult> ProcessLinearDisciplineAsync(
        IAutomationPage avaPage,
        Disciplina discipline,
        CancellationToken cancellationToken)
    {
        var topics = await linearCourseExplorerService.ExecuteAsync(
            avaPage,
            cancellationToken);

        if (topics.Count == 0)
        {
            logger.LogWarning(
                "Nenhum tópico foi encontrado na disciplina alternativa {DisciplineName}",
                discipline.Nome);
            return new DisciplineProcessingResult(0, 0, 0);
        }

        var requiredTopics = topics
            .Where(topic => !disciplineExecutionPolicy.IsExcludedLinearTopic(topic.Nome))
            .ToList();
        var locallyCompletedTopics = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var topic in requiredTopics)
        {
            var locallyCompleted = await activityProgressStore.IsCompletedAsync(
                topic.Url,
                cancellationToken);

            if (locallyCompleted &&
                disciplineExecutionPolicy.RequiresLiveCompletionForLinearTopic(
                    discipline.Nome,
                    topic.Nome))
            {
                logger.LogInformation(
                    "Registro local de {TopicName} será revalidado no AVA porque o tópico pode conter questionários avaliativos pendentes",
                    topic.Nome);
                continue;
            }

            if (locallyCompleted)
            {
                locallyCompletedTopics.Add(topic.Url);
            }
        }

        var pendingRequiredTopics = requiredTopics
            .Where(topic => !IsLinearTopicConfirmedCompleted(
                topic,
                locallyCompletedTopics))
            .ToList();

        logger.LogInformation(
            "Verificação de conclusão de {DisciplineName}: obrigatórios: {RequiredCount}; confirmados: {CompletedCount}; pendentes: {PendingCount}",
            discipline.Nome,
            requiredTopics.Count,
            requiredTopics.Count - pendingRequiredTopics.Count,
            pendingRequiredTopics.Count);

        if (pendingRequiredTopics.Count > 0)
        {
            logger.LogInformation(
                "Tópicos que ainda exigem verificação: {PendingTopics}",
                string.Join(", ", pendingRequiredTopics.Select(topic => topic.Nome)));
        }

        if (requiredTopics.Count > 0 && pendingRequiredTopics.Count == 0)
        {
            logger.LogInformation(
                "Todos os tópicos obrigatórios de {DisciplineName} estão concluídos; Certificado não participa da verificação. Avançando para a próxima disciplina",
                discipline.Nome);
            return new DisciplineProcessingResult(1, requiredTopics.Count, 0);
        }

        var processedTopics = 0;
        var activityCount = 0;

        foreach (var topic in topics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (disciplineExecutionPolicy.IsExcludedLinearTopic(topic.Nome))
            {
                logger.LogInformation(
                    "Tópico excluído da automação e deixado para abertura manual: {TopicName}",
                    topic.Nome);
                continue;
            }


            if (IsLinearTopicConfirmedCompleted(topic, locallyCompletedTopics))
            {
                processedTopics++;
                logger.LogInformation(
                    "Tópico obrigatório já concluído; avançando para o próximo: {TopicName}. Evidência: {CompletionEvidence}",
                    topic.Nome,
                    disciplineExecutionPolicy.AllowsLocalCompletionForLinearTopic(topic.Nome) &&
                    locallyCompletedTopics.Contains(topic.Url)
                        ? "registro local de tópico concluído sem considerar Certificado"
                        : topic.CompletionEvidence);
                continue;
            }

            logger.LogInformation(
                "Abrindo tópico linear {TopicIndex}/{TopicCount}: {TopicName}",
                processedTopics + 1,
                topics.Count,
                topic.Nome);

            var openedTopic = await OpenLinearTopicAsync(
                avaPage,
                topic,
                cancellationToken);
            var topicUrl = avaPage.Url;

            var topicActivityResult = await ProcessSectionActivitiesAsync(
                avaPage,
                openedTopic,
                topicUrl,
                discipline.Nome,
                cancellationToken);
            activityCount += topicActivityResult.ActivityCount;
            processedTopics++;

            if (topicActivityResult.Completed &&
                topicActivityResult.ActivityCount > 0 &&
                _activityNavigationOptions.Mode != ActivityExecutionMode.Teste)
            {
                await activityProgressStore.MarkCompletedAsync(
                    topic.Url,
                    cancellationToken);
                locallyCompletedTopics.Add(topic.Url);
                logger.LogInformation(
                    "Tópico linear confirmado e registrado localmente: {TopicName}; modo: {TopicMode}; recursos verificados: {ActivityCount}. Recursos opcionais como Certificado não impedem o avanço",
                    topic.Nome,
                    topic.PassiveOnly ? "passivo" : "avaliativo",
                    topicActivityResult.ActivityCount);
            }
            else if (topicActivityResult.Completed &&
                     topicActivityResult.ActivityCount == 0)
            {
                logger.LogWarning(
                    "Tópico {TopicName} não possui recursos detectados e não será registrado como concluído localmente",
                    topic.Nome);
            }

            if (_activityNavigationOptions.Mode == ActivityExecutionMode.Teste)
            {
                break;
            }
        }

        return new DisciplineProcessingResult(
            1,
            processedTopics,
            activityCount);
    }

    private async Task<Section> OpenLinearTopicAsync(
        IAutomationPage avaPage,
        Section topic,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Navegando para o tópico linear {TopicName}; URL: {TopicUrl}",
            topic.Nome,
            topic.Url);

        if (!string.Equals(
                avaPage.Url,
                topic.Url,
                StringComparison.OrdinalIgnoreCase))
        {
            await avaPage.NavigateAsync(topic.Url, cancellationToken);
        }

        await avaPage.WaitForDomContentLoadedAsync(cancellationToken);
        await avaPage.WaitForVisibleAsync("#ct-cards-list", cancellationToken);

        logger.LogInformation(
            "Tópico linear aberto: {TopicName}; URL atual: {CurrentUrl}",
            topic.Nome,
            avaPage.Url);
        return topic;
    }

    private async Task ReturnToDisciplinesAsync(
        IAutomationPage avaPage,
        string disciplinesPageUrl,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Retornando à página de disciplinas");
        await avaPage.NavigateAsync(disciplinesPageUrl, cancellationToken);
        logger.LogInformation(
            "Página de disciplinas restaurada. URL: {DisciplinesUrl}",
            avaPage.Url);
    }

    private async Task<SectionActivityProcessingResult> ProcessSectionActivitiesAsync(
        IAutomationPage avaPage,
        Section openedSection,
        string sectionUrl,
        string disciplineName,
        CancellationToken cancellationToken)
    {
        var lastActivityCount = 0;

        while (true)
        {
            logger.LogInformation("Iniciando ActivityExplorerService");
            var activities = await activityExplorerService.ExecuteAsync(
                avaPage,
                openedSection,
                cancellationToken);
            lastActivityCount = activities.Count;
            logger.LogInformation(
                "ActivityExplorerService concluído com {ActivityCount} atividade(s)",
                activities.Count);

            logger.LogInformation(
                "Iniciando ActivityNavigationService no modo {ExecutionMode}",
                _activityNavigationOptions.Mode);
            var navigationResult = await activityNavigationService.ExecuteAsync(
                avaPage,
                activities,
                new ActivityExecutionFilter
                {
                    Mode = _activityNavigationOptions.Mode
                },
                cancellationToken);

            logger.LogInformation(
                "ActivityNavigationService concluído. Navegações: {NavigationCount}; " +
                "página preparada para questões: {PreparedForQuestions}",
                navigationResult.NavigationCount,
                navigationResult.PreparedForQuestions);

            if (!navigationResult.PreparedForQuestions ||
                navigationResult.OpenedActivity is null)
            {
                var requiredActivitiesCompleted =
                    await AreRequiredActivitiesCompletedAsync(
                        activities,
                        cancellationToken);
                logger.LogInformation(
                    "Nenhuma atividade avaliativa pendente permaneceu na seção. Recursos obrigatórios concluídos: {RequiredActivitiesCompleted}",
                    requiredActivitiesCompleted);
                return new SectionActivityProcessingResult(
                    lastActivityCount,
                    Completed: requiredActivitiesCompleted);
            }

            logger.LogInformation(
                "Iniciando extração em lote para JSON no console; a camada OpenAI permanecerá inativa");
            var visualMode = IsMathematicsDiscipline(disciplineName);
            logger.LogInformation(
                "Disciplina: {DisciplineName}; modo visual: {VisualMode}",
                disciplineName,
                visualMode ? "SIM" : "NÃO");
            await questionConsoleFlow.ExecuteAsync(
                avaPage,
                disciplineName,
                visualMode,
                cancellationToken);
            logger.LogInformation("JSON das questões exposto no console");

            logger.LogInformation(
                "Iniciando demonstração independente questions.json -> Responses API -> console");
            var inferenceResults = await inferenceConsoleFlow.ExecuteFromFileAsync(
                "questions.json",
                visualMode,
                cancellationToken);
            logger.LogInformation(
                "Demonstração da Responses API concluída; resultados recebidos: {InferenceResultCount}",
                inferenceResults.Count);

            foreach (var inferenceResult in inferenceResults.OrderBy(
                         result => result.QuestionNumber))
            {
                var hasSuggestedAlternative =
                    string.Equals(
                        inferenceResult.Status,
                        InferenceStatuses.Answered,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(
                        inferenceResult.SelectedAlternative);
                var requiresHumanAction =
                    !hasSuggestedAlternative ||
                    inferenceResult.Confidence <
                    _answerFillOptions.MinimumConfidence;

                if (requiresHumanAction)
                {
                    logger.LogWarning(
                        "Questão {QuestionNumber} exige ação humana; status: {InferenceStatus}; confiança: {Confidence}; mínimo exigido: {MinimumConfidence}; motivo: {Warning}",
                        inferenceResult.QuestionNumber,
                        inferenceResult.Status,
                        inferenceResult.Confidence,
                        _answerFillOptions.MinimumConfidence,
                        inferenceResult.Warning ?? inferenceResult.MissingData);

                    var answeredByHuman = await answerFillService
                        .WaitForManualAnswerAsync(
                            avaPage,
                            inferenceResult.QuestionNumber.ToString(),
                            TimeSpan.FromMilliseconds(
                                _answerFillOptions.ManualAnswerTimeout),
                            cancellationToken);

                    if (answeredByHuman)
                    {
                        continue;
                    }

                    if (!hasSuggestedAlternative)
                    {
                        logger.LogWarning(
                            "A questão {QuestionNumber} não possui alternativa de fallback. O navegador continuará aguardando a resposta humana em ciclos de {TimeoutSeconds} segundos",
                            inferenceResult.QuestionNumber,
                            _answerFillOptions.ManualAnswerTimeout / 1000d);

                        do
                        {
                            answeredByHuman = await answerFillService
                                .WaitForManualAnswerAsync(
                                    avaPage,
                                    inferenceResult.QuestionNumber.ToString(),
                                    TimeSpan.FromMilliseconds(
                                        _answerFillOptions.ManualAnswerTimeout),
                                    cancellationToken);
                        }
                        while (!answeredByHuman);

                        continue;
                    }

                    logger.LogWarning(
                        "Prazo de ação humana encerrado na questão {QuestionNumber}; aplicando a alternativa sugerida {SelectedAlternative} e seguindo o fluxo",
                        inferenceResult.QuestionNumber,
                        inferenceResult.SelectedAlternative);
                }

                var fillResult = await answerFillService.FillAnswerAsync(
                        avaPage,
                        inferenceResult.QuestionNumber.ToString(),
                        inferenceResult.SelectedAlternative!,
                        cancellationToken);

                if (fillResult.Success)
                {
                    continue;
                }

                logger.LogWarning(
                    "Falha no preenchimento da questão {QuestionNumber}: {FailureMessage}. Aguardando ação humana antes de continuar",
                    inferenceResult.QuestionNumber,
                    fillResult.Message);

                while (!await answerFillService.WaitForManualAnswerAsync(
                           avaPage,
                           inferenceResult.QuestionNumber.ToString(),
                           TimeSpan.FromMilliseconds(
                               _answerFillOptions.ManualAnswerTimeout),
                           cancellationToken))
                {
                }
            }

            logger.LogInformation(
                "Etapa de aplicação por questão encerrada; nenhuma tentativa foi enviada nesta etapa");

            logger.LogInformation(
                "Iniciando navegação assistida para validar as seleções e alcançar o resumo");
            await manualQuizNavigationService.NavigateToSummaryAsync(
                avaPage,
                cancellationToken);
            logger.LogInformation(
                "Navegação manual assistida chegou ao resumo da tentativa");

            logger.LogInformation(
                "Aguardando o usuário concluir as respostas para iniciar QuizSubmissionService");
            var completed = await quizSubmissionService.CompleteAsync(
                avaPage,
                cancellationToken);
            logger.LogInformation(
                "QuizSubmissionService concluído. Atividade finalizada: {Completed}",
                completed);

            if (!completed)
            {
                logger.LogInformation(
                    "Finalização automática desabilitada; o pipeline de atividades será pausado");
                return new SectionActivityProcessingResult(
                    lastActivityCount,
                    Completed: false);
            }

            await activityProgressStore.MarkCompletedAsync(
                navigationResult.OpenedActivity.Identifier,
                cancellationToken);
            logger.LogInformation(
                "Conclusão persistida para {ActivityName}",
                navigationResult.OpenedActivity.Nome);

            if (_activityNavigationOptions.Mode == ActivityExecutionMode.Teste)
            {
                return new SectionActivityProcessingResult(
                    lastActivityCount,
                    Completed: true);
            }

            logger.LogInformation(
                "Retornando à seção para procurar a próxima atividade pendente");
            await avaPage.NavigateAsync(sectionUrl, cancellationToken);
            await avaPage.WaitForDomContentLoadedAsync(cancellationToken);
            await avaPage.WaitForNetworkIdleAsync(cancellationToken);
            await avaPage.WaitForVisibleAsync("#ct-cards-list", cancellationToken);
        }
    }

    private async Task<bool> AreRequiredActivitiesCompletedAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken)
    {
        foreach (var activity in activities)
        {
            if (activity.Nome.Contains(
                    "Certificado",
                    StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Recurso opcional ignorado na conclusão do tópico: {ActivityName}",
                    activity.Nome);
                continue;
            }

            if (activity.CompletionStatus == ActivityCompletionStatus.Completed ||
                await activityProgressStore.IsCompletedAsync(
                    activity.Identifier,
                    cancellationToken))
            {
                continue;
            }

            logger.LogInformation(
                "Recurso obrigatório ainda pendente; o tópico não será marcado como concluído: {ActivityName}",
                activity.Nome);
            return false;
        }

        return true;
    }

    private static bool IsMathematicsDiscipline(string disciplineName) =>
        disciplineName.Contains("Matemática", StringComparison.OrdinalIgnoreCase) ||
        disciplineName.Contains("Matematica", StringComparison.OrdinalIgnoreCase);

    private bool IsLinearTopicConfirmedCompleted(
        Section topic,
        IReadOnlySet<string> locallyCompletedTopics) =>
        topic.Concluida ||
        (disciplineExecutionPolicy.AllowsLocalCompletionForLinearTopic(topic.Nome) &&
         locallyCompletedTopics.Contains(topic.Url));

    private static bool IsValidUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private sealed record DisciplineProcessingResult(
        int ProcessedUnits,
        int ProcessedSections,
        int ActivityCount);

    private sealed record SectionActivityProcessingResult(
        int ActivityCount,
        bool Completed);
}
