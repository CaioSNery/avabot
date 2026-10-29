using System.Diagnostics;
using System.Text.Json;
using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using AvaBot.Infrastructure.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Extraction;

public sealed class QuestionConsoleFlow(
    IQuestionExtractor extractor,
    IQuestionParser parser,
    IQuestionJsonExporter jsonExporter,
    IOptions<QuestionExtractionOptions> options,
    ILogger<QuestionConsoleFlow> logger) : IQuestionConsoleFlow
{
    private const string AttemptUrlFragment = "/mod/quiz/attempt.php";
    private const string StartQuizSelector =
        ".quizstartbuttondiv button[type='submit']:visible, " +
        ".quizstartbuttondiv input[type='submit']:visible, " +
        "button:has-text('Tentar responder o questionário agora'):visible, " +
        "button:has-text('Continuar a última tentativa'):visible, " +
        "input[type='submit'][value*='Tentar responder' i]:visible, " +
        "input[type='submit'][value*='Continuar a última tentativa' i]:visible";
    private const string ConfirmStartSelector =
        "button:has-text('Iniciar tentativa'):visible, " +
        "input[type='submit'][value*='Iniciar tentativa' i]:visible";
    private static readonly JsonSerializerOptions ConsoleSerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
    private readonly QuestionExtractionOptions _options = options.Value;

    public async Task ExecuteAsync(
        IAutomationPage page,
        string disciplineName,
        bool visualMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(disciplineName);

        if (page is not PlaywrightAutomationPage playwrightPage)
        {
            throw new InvalidOperationException(
                "A extração requer uma página criada pelo adaptador Playwright do AvaBot.");
        }

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Disciplina: {DisciplineName}; modo visual: {VisualMode}",
            disciplineName,
            visualMode ? "SIM" : "NÃO");
        await WaitForQuestionsAsync(playwrightPage.NativePage, cancellationToken);

        var extractedQuiz = await extractor.ExtractAsync(
            playwrightPage.NativePage,
            visualMode,
            cancellationToken);
        var quiz = parser.Parse(extractedQuiz);
        var json = CreateSafeConsoleJson(quiz);

        await jsonExporter.ExportAsync(quiz, "questions.json", cancellationToken);

        Console.WriteLine();
        Console.WriteLine("===== QUESTIONS.JSON =====");
        Console.WriteLine(json);
        Console.WriteLine("===== FIM QUESTIONS.JSON =====");
        Console.WriteLine();

        stopwatch.Stop();
        logger.LogInformation(
            "Lote completo exposto no console em {ElapsedMilliseconds} ms; questões: {QuestionCount}; alternativas: {AlternativeCount}; fórmulas: {FormulaCount}; screenshots: {ScreenshotCount}. Dados base64 foram omitidos somente da exibição; questions.json permanece completo. Nenhum motor de inferência foi chamado",
            stopwatch.ElapsedMilliseconds,
            quiz.Questions.Count,
            quiz.Questions.Sum(question => question.Alternatives.Count),
            quiz.Questions.Sum(question => question.Formulas?.Count ?? 0),
            quiz.Questions.Count(question => !string.IsNullOrWhiteSpace(question.ScreenshotDataUrl)));
    }

    internal async Task WaitForQuestionsAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var questions = await SelectQuestionContainersAsync(page, cancellationToken);

        if (await questions.CountAsync() > 0)
        {
            logger.LogInformation("Questões já estão renderizadas; iniciando leitura em lote");
            return;
        }

        if (!page.Url.Contains(AttemptUrlFragment, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Aguardando botão de abertura ou questões por até 10 segundos");
            try
            {
                await page.Locator($"{StartQuizSelector}, {_options.QuestionSelector}")
                    .First.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 10000
                    }).WaitAsync(cancellationToken);
            }
            catch (TimeoutException exception)
            {
                throw new QuizUnavailableException(
                    "Nenhum botão de abertura ou questão apareceu em 10 segundos.",
                    exception);
            }

            questions = await SelectQuestionContainersAsync(page, cancellationToken);
            if (await questions.CountAsync() > 0 && await questions.First.IsVisibleAsync())
            {
                return;
            }

            var startButton = await FindFirstInteractableAsync(
                page.Locator(StartQuizSelector),
                cancellationToken);

            if (startButton is not null)
            {
                logger.LogInformation("Botão para abrir o questionário encontrado; iniciando tentativa");
                await startButton.ClickAsync().WaitAsync(cancellationToken);
                logger.LogInformation("Clique para abrir o questionário realizado");
                await TryConfirmStartAsync(page, cancellationToken);
            }
            else
            {
                throw new QuizUnavailableException(
                    "O botão de abertura do questionário não está disponível para interação.",
                    new InvalidOperationException("Botão ausente ou desabilitado."));
            }
        }
        else
        {
            logger.LogInformation(
                "Tentativa já iniciada detectada pela URL; nenhum clique de abertura será repetido");
        }

        logger.LogInformation(
            "Aguardando questões por até {TimeoutMilliseconds} ms",
            _options.ManualStartTimeout);

        try
        {
            var renderedQuestions = await SelectQuestionContainersAsync(
                page,
                cancellationToken);
            await renderedQuestions.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = _options.ManualStartTimeout
            }).WaitAsync(cancellationToken);
        }
        catch (PlaywrightException exception)
            when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException(
                "Nenhuma questão apareceu após a tentativa de abertura automática.",
                exception);
        }

        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded)
            .WaitAsync(cancellationToken);
        logger.LogInformation(
            "Questionário aberto e questões detectadas. URL: {QuestionUrl}",
            page.Url);
    }

    private async Task<ILocator> SelectQuestionContainersAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var selectors = _options.QuestionSelector.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var selector in selectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = page.Locator(selector);

            if (await candidates.CountAsync() > 0)
            {
                return candidates;
            }
        }

        return page.Locator(_options.QuestionSelector);
    }

    private static async Task<ILocator?> FindFirstInteractableAsync(
        ILocator candidates,
        CancellationToken cancellationToken)
    {
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

        return null;
    }

    private async Task TryConfirmStartAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var confirmation = page.Locator(ConfirmStartSelector).First;

        try
        {
            await confirmation.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            }).WaitAsync(cancellationToken);
        }
        catch (Exception exception)
            when (exception is TimeoutException ||
                  exception is PlaywrightException &&
                  exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug(
                "Confirmação adicional para iniciar tentativa não foi apresentada");
            return;
        }

        if (!await confirmation.IsEnabledAsync().WaitAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "A confirmação para iniciar a tentativa está desabilitada.");
        }

        await confirmation.ClickAsync().WaitAsync(cancellationToken);
        logger.LogInformation("Confirmação para iniciar tentativa realizada");
    }

    private static string CreateSafeConsoleJson(
        AvaBot.Domain.Models.Questions.Quiz quiz) =>
        JsonSerializer.Serialize(
            new
            {
                quiz.Title,
                Questions = quiz.Questions.Select(question => new
                {
                    question.Number,
                    question.Statement,
                    question.Alternatives,
                    question.Formulas,
                    Images = question.Images.Select(image => new
                    {
                        image.Description,
                        Source = DescribeBinarySource(image.Source)
                    }),
                    question.Tables,
                    question.CodeBlocks,
                    Screenshot = DescribeBinarySource(question.ScreenshotDataUrl)
                })
            },
            ConsoleSerializerOptions);

    private static string DescribeBinarySource(string source) =>
        source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
            ? $"<imagem base64 omitida do console; caracteres={source.Length}>"
            : source;
}
