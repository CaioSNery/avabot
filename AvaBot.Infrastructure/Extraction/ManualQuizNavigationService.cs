using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using AvaBot.Infrastructure.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Extraction;

public sealed class ManualQuizNavigationService(
    IOptions<ManualQuizNavigationOptions> options,
    ILogger<ManualQuizNavigationService> logger) : IManualQuizNavigationService
{
    private const string AttemptUrlFragment = "/mod/quiz/attempt.php";
    private const string SummaryUrlFragment = "/mod/quiz/summary.php";
    private const string QuestionSelector =
        ".que:visible, [data-testid='question']:visible, [data-question-id]:visible";
    private const string ChoiceSelector =
        "input[type='radio']:not([disabled]), input[type='checkbox']:not([disabled])";
    private const string CheckedChoiceSelector =
        "input[type='radio']:checked, input[type='checkbox']:checked";
    private const string NavigationButtonSelector =
        "input.mod_quiz-next-nav[type='submit']:visible, " +
        "button.mod_quiz-next-nav:visible, " +
        "a.mod_quiz-next-nav:visible, " +
        "input[name='next'][type='submit']:visible, " +
        "button[name='next']:visible, " +
        "input[type='submit'][value*='Próxima' i]:visible, " +
        "input[type='submit'][value*='Finalizar tentativa' i]:visible, " +
        "button:has-text('Próxima página'):visible, " +
        "button:has-text('Finalizar tentativa'):visible";
    private const string FailureScreenshotPath =
        "logs/screenshots/manual-quiz-navigation-failure.png";
    private const int NavigationConfirmationTimeout = 30_000;

    private readonly ManualQuizNavigationOptions _options = options.Value;

    public async Task NavigateToSummaryAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Navegação manual assistida desabilitada em ManualQuizNavigation:Enabled");
            return;
        }

        ValidateOptions();

        if (page is not PlaywrightAutomationPage playwrightPage)
        {
            throw new InvalidOperationException(
                "A navegação manual assistida requer uma página Playwright do AvaBot.");
        }

        var nativePage = playwrightPage.NativePage;

        try
        {
            if (ContainsUrl(page.Url, SummaryUrlFragment))
            {
                logger.LogInformation("O questionário já está na página de resumo");
                return;
            }

            if (!ContainsUrl(page.Url, AttemptUrlFragment))
            {
                throw new InvalidOperationException(
                    $"A navegação assistida esperava uma tentativa aberta, mas encontrou {page.Url}.");
            }

            logger.LogInformation(
                "ManualQuizNavigationService iniciado. O bot não selecionará alternativas; aguardará as respostas humanas");

            for (var pageNumber = 1; pageNumber <= _options.MaxPages; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await nativePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded)
                    .WaitAsync(cancellationToken);

                var answeredQuestions = await WaitForHumanAnswersAsync(
                    nativePage,
                    pageNumber,
                    cancellationToken);
                var navigationButton = await FindNavigationButtonAsync(
                    nativePage,
                    cancellationToken);
                var buttonLabel = await ReadButtonLabelAsync(navigationButton);
                var isFinishButton = IsFinishButton(buttonLabel);

                logger.LogInformation(
                    "Página {PageNumber}: {AnsweredQuestionCount} questão(ões) respondida(s) manualmente. Controle encontrado: {ButtonLabel}",
                    pageNumber,
                    answeredQuestions,
                    buttonLabel);

                var previousUrl = nativePage.Url;
                var urlChange = WaitForUrlChangeAsync(
                    nativePage,
                    previousUrl,
                    buttonLabel,
                    cancellationToken);
                await navigationButton.EvaluateAsync(
                        "element => { setTimeout(() => element.click(), 0); }")
                    .WaitAsync(cancellationToken);
                logger.LogInformation(
                    "Clique realizado em {ButtonLabel}; nenhuma alternativa foi modificada pelo bot",
                    buttonLabel);
                await urlChange;
                await nativePage.WaitForLoadStateAsync(LoadState.DOMContentLoaded)
                    .WaitAsync(cancellationToken);

                if (ContainsUrl(nativePage.Url, SummaryUrlFragment))
                {
                    logger.LogInformation(
                        "Resumo da tentativa alcançado após {PageCount} página(s). URL: {SummaryUrl}",
                        pageNumber,
                        nativePage.Url);
                    return;
                }

                if (isFinishButton)
                {
                    throw new InvalidOperationException(
                        "O botão de finalizar foi acionado, mas a página de resumo não foi apresentada.");
                }
            }

            throw new InvalidOperationException(
                $"O questionário excedeu o limite configurado de {_options.MaxPages} páginas.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha na navegação manual assistida. URL atual: {CurrentUrl}",
                page.Url);
            await TrySaveScreenshotAsync(page, cancellationToken);
            throw;
        }
    }

    private async Task<int> WaitForHumanAnswersAsync(
        IPage page,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var questions = page.Locator(QuestionSelector);
        var questionCount = await questions.CountAsync().WaitAsync(cancellationToken);

        if (questionCount == 0)
        {
            throw new InvalidOperationException(
                $"Nenhuma questão foi encontrada na página {pageNumber} da tentativa.");
        }

        logger.LogInformation(
            "Página {PageNumber}: aguardando respostas humanas em {QuestionCount} questão(ões) visível(is)",
            pageNumber,
            questionCount);

        var answerableQuestionCount = 0;

        for (var index = 0; index < questionCount; index++)
        {
            var question = questions.Nth(index);

            if (await question.Locator(ChoiceSelector).CountAsync().WaitAsync(cancellationToken) == 0)
            {
                logger.LogWarning(
                    "Questão {QuestionIndex} não possui radio button ou checkbox; ela não será avançada automaticamente",
                    index + 1);
                continue;
            }

            answerableQuestionCount++;
            logger.LogInformation(
                "Aguardando seleção manual da questão {QuestionIndex} na página {PageNumber}",
                index + 1,
                pageNumber);
            await question.Locator(CheckedChoiceSelector).First.WaitForAsync(
                    new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = _options.AnswerTimeout
                    })
                .WaitAsync(cancellationToken);
            logger.LogInformation(
                "Seleção humana detectada na questão {QuestionIndex}; valor selecionado não será lido nem registrado",
                index + 1);
        }

        if (answerableQuestionCount == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma questão de múltipla escolha foi encontrada para navegação assistida.");
        }

        return answerableQuestionCount;
    }

    private async Task WaitForUrlChangeAsync(
        IPage page,
        string previousUrl,
        string buttonLabel,
        CancellationToken cancellationToken)
    {
        static bool HasChanged(string currentUrl, string previousUrl) =>
            !string.Equals(
                currentUrl,
                previousUrl,
                StringComparison.OrdinalIgnoreCase);

        try
        {
            await page.WaitForURLAsync(
                    url => HasChanged(url, previousUrl),
                    new PageWaitForURLOptions
                    {
                        Timeout = NavigationConfirmationTimeout
                    })
                .WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            logger.LogWarning(
                "A navegação após {ButtonLabel} não foi confirmada em {TimeoutMilliseconds} ms. O navegador permanecerá aberto aguardando intervenção humana ou Ctrl+C",
                buttonLabel,
                NavigationConfirmationTimeout);

            await page.WaitForURLAsync(
                    url => HasChanged(url, previousUrl),
                    new PageWaitForURLOptions
                    {
                        Timeout = 0
                    })
                .WaitAsync(cancellationToken);
        }
    }

    private static async Task<ILocator> FindNavigationButtonAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var candidates = page.Locator(NavigationButtonSelector);
        var count = await candidates.CountAsync().WaitAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            var candidate = candidates.Nth(index);

            if (await candidate.IsVisibleAsync().WaitAsync(cancellationToken) &&
                await candidate.IsEnabledAsync().WaitAsync(cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Nenhum botão visível de próxima página ou finalizar tentativa foi encontrado.");
    }

    private static async Task<string> ReadButtonLabelAsync(ILocator button)
    {
        var value = await button.GetAttributeAsync("value");

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        var text = await button.InnerTextAsync();
        return string.IsNullOrWhiteSpace(text)
            ? "Controle de navegação sem texto"
            : text.Trim();
    }

    private static bool IsFinishButton(string label) =>
        label.Contains("Finalizar tentativa", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsUrl(string url, string fragment) =>
        url.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private void ValidateOptions()
    {
        if (_options.AnswerTimeout < 0)
        {
            throw new InvalidOperationException(
                "ManualQuizNavigation:AnswerTimeout não pode ser negativo. Use zero para aguardar indefinidamente.");
        }

        if (_options.MaxPages <= 0)
        {
            throw new InvalidOperationException(
                "ManualQuizNavigation:MaxPages deve ser maior que zero.");
        }
    }

    private static async Task TrySaveScreenshotAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            await page.ScreenshotAsync(FailureScreenshotPath, cancellationToken);
        }
        catch
        {
        }
    }
}
