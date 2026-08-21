using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class QuizSubmissionService(
    IOptions<QuizSubmissionOptions> options,
    ILogger<QuizSubmissionService> logger) : IQuizSubmissionService
{
    private const string SummaryUrlFragment = "/mod/quiz/summary.php";
    private const string ReviewUrlFragment = "/mod/quiz/review.php";
    private const string SubmitButtonName = "Enviar tudo e terminar";
    private const string VisibleDialogSelector =
        "[role='dialog']:visible, " +
        ".confirmation-dialogue:visible, " +
        ".modal-dialog:visible";
    private const string FinishReviewSelector =
        "a:has-text('Terminar revisão'), " +
        "button:has-text('Terminar revisão'), " +
        "input[type='submit'][value*='TERMINAR' i]";
    private const string FailureScreenshotPath =
        "logs/screenshots/quiz-submission-failure.png";

    private readonly QuizSubmissionOptions _options = options.Value;

    public async Task<bool> CompleteAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Finalização automática desabilitada em QuizSubmission:Enabled");
            return false;
        }

        if (_options.ManualCompletionTimeout <= 0)
        {
            throw new InvalidOperationException(
                "QuizSubmission:ManualCompletionTimeout deve ser maior que zero.");
        }

        try
        {
            logger.LogInformation(
                "Aguardando finalização manual das respostas e acesso ao resumo da tentativa");

            var summaryPresented = await page.TryWaitForUrlContainingAsync(
                SummaryUrlFragment,
                TimeSpan.FromMilliseconds(_options.ManualCompletionTimeout),
                cancellationToken);

            if (!summaryPresented)
            {
                throw new TimeoutException(
                    "A página de resumo da tentativa não apareceu dentro do tempo configurado.");
            }

            await page.WaitForDomContentLoadedAsync(cancellationToken);
            logger.LogInformation("Resumo da tentativa encontrado em {CurrentUrl}", page.Url);

            var submitButton = await FindLastVisibleAsync(
                page.GetButtonByRole(SubmitButtonName),
                SubmitButtonName,
                cancellationToken);
            await ClickEnabledAsync(
                submitButton,
                "Enviar tudo e terminar no resumo",
                cancellationToken);

            logger.LogInformation("Aguardando diálogo de confirmação");
            var dialog = page.GetLocator(VisibleDialogSelector).Nth(0);
            var dialogVisible = await dialog.TryWaitForVisibleAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            IAutomationLocator confirmationButton;

            if (dialogVisible)
            {
                logger.LogInformation("Diálogo de confirmação visível encontrado");
                confirmationButton = await FindButtonByTextAsync(
                    dialog.GetLocator("button, input[type='submit'], input[type='button']"),
                    SubmitButtonName,
                    cancellationToken);
            }
            else
            {
                logger.LogInformation(
                    "Nenhum contêiner de diálogo visível foi identificado; procurando o botão de confirmação diretamente na página");
                confirmationButton = await FindLastVisibleAsync(
                    page.GetButtonByRole(SubmitButtonName),
                    $"confirmação {SubmitButtonName}",
                    cancellationToken);
            }

            await ClickEnabledAsync(
                confirmationButton,
                "Confirmar envio definitivo",
                cancellationToken);
            logger.LogInformation("Clique de confirmação realizado");

            logger.LogInformation("Envio confirmado; aguardando página de revisão");
            await page.WaitForUrlContainingAsync(ReviewUrlFragment, cancellationToken);
            await page.WaitForDomContentLoadedAsync(cancellationToken);

            var finishReviewButton = await FindLastVisibleAsync(
                page.GetLocator(FinishReviewSelector),
                "Terminar revisão",
                cancellationToken);
            await finishReviewButton.ScrollIntoViewAsync(cancellationToken);

            var reviewUrl = page.Url;
            await ClickEnabledAsync(
                finishReviewButton,
                "Terminar revisão",
                cancellationToken);
            await page.WaitForUrlChangeAsync(reviewUrl, cancellationToken);

            logger.LogInformation(
                "Revisão terminada com sucesso. URL final: {FinalUrl}; título: {FinalTitle}",
                page.Url,
                await page.GetTitleAsync(cancellationToken));

            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha na finalização da tentativa. URL atual: {CurrentUrl}; título: {CurrentTitle}",
                page.Url,
                await TryGetTitleAsync(page, cancellationToken));

            await TrySaveFailureScreenshotAsync(page, cancellationToken);
            throw;
        }
    }

    private static async Task<IAutomationLocator> FindLastVisibleAsync(
        IAutomationLocator candidates,
        string description,
        CancellationToken cancellationToken)
    {
        var count = await candidates.CountAsync(cancellationToken);

        for (var index = count - 1; index >= 0; index--)
        {
            var candidate = candidates.Nth(index);

            if (await candidate.IsVisibleAsync(cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Nenhum elemento visível foi encontrado para '{description}'.");
    }

    private static async Task<IAutomationLocator> FindButtonByTextAsync(
        IAutomationLocator candidates,
        string expectedText,
        CancellationToken cancellationToken)
    {
        var count = await candidates.CountAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            var candidate = candidates.Nth(index);
            var text = await candidate.InnerTextAsync(cancellationToken);
            var value = await candidate.GetAttributeAsync("value", cancellationToken);
            var label = string.IsNullOrWhiteSpace(text) ? value : text;

            if (label?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true &&
                await candidate.IsVisibleAsync(cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"O botão '{expectedText}' não foi encontrado no diálogo de confirmação.");
    }

    private static async Task ClickEnabledAsync(
        IAutomationLocator locator,
        string step,
        CancellationToken cancellationToken)
    {
        if (!await locator.IsEnabledAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"O botão da etapa '{step}' está desabilitado.");
        }

        await locator.ClickAsync(cancellationToken);
    }

    private static async Task<string> TryGetTitleAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        try
        {
            return await page.GetTitleAsync(cancellationToken);
        }
        catch
        {
            return "Título indisponível";
        }
    }

    private static async Task TrySaveFailureScreenshotAsync(
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
