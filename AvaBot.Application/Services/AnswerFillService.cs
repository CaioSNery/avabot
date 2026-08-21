using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class AnswerFillService(
    IOptions<AnswerFillOptions> options,
    ILogger<AnswerFillService> logger) : IAnswerFillService
{
    private const string CheckedChoiceSelector =
        "input[type='radio']:checked, input[type='checkbox']:checked";
    private readonly AnswerFillOptions _options = options.Value;

    public async Task<bool> WaitForManualAnswerAsync(
        IAutomationPage page,
        string questionId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(questionId);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "O tempo de espera pela resposta humana deve ser maior que zero.");
        }

        var normalizedQuestionId = questionId.Trim();
        var question = await FindQuestionAsync(
            page,
            normalizedQuestionId,
            cancellationToken);

        if (question is null && await TryNavigateToQuestionAsync(
                page,
                normalizedQuestionId,
                cancellationToken))
        {
            question = await FindQuestionAsync(
                page,
                normalizedQuestionId,
                cancellationToken);
        }

        if (question is null)
        {
            logger.LogWarning(
                "Não foi possível posicionar a página na questão {QuestionId} para aguardar a resposta humana",
                normalizedQuestionId);
            return false;
        }

        logger.LogInformation(
            "Questão {QuestionId} aguardando ação humana por até {TimeoutSeconds} segundos",
            normalizedQuestionId,
            timeout.TotalSeconds);

        var answered = await question
            .GetLocator(CheckedChoiceSelector)
            .TryWaitForVisibleAsync(timeout, cancellationToken);

        logger.LogInformation(
            answered
                ? "Ação humana detectada na questão {QuestionId}; seguindo para a próxima questão"
                : "Nenhuma ação humana detectada na questão {QuestionId} dentro do prazo",
            normalizedQuestionId);

        return answered;
    }

    public async Task<AnswerFillResult> FillAnswerAsync(
        IAutomationPage page,
        string questionId,
        string selectedOption,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (string.IsNullOrWhiteSpace(questionId))
        {
            return AnswerFillResult.Fail("O identificador da questão não foi informado.");
        }

        if (string.IsNullOrWhiteSpace(selectedOption))
        {
            return AnswerFillResult.Fail(
                $"A questão {questionId} não possui alternativa selecionada.");
        }

        try
        {
            ValidateOptions();
            var normalizedQuestionId = questionId.Trim();
            var normalizedOption = selectedOption.Trim();

            var question = await FindQuestionAsync(
                page,
                normalizedQuestionId,
                cancellationToken);

            if (question is null)
            {
                var navigated = await TryNavigateToQuestionAsync(
                    page,
                    normalizedQuestionId,
                    cancellationToken);

                if (navigated)
                {
                    question = await FindQuestionAsync(
                        page,
                        normalizedQuestionId,
                        cancellationToken);
                }
            }

            if (question is null)
            {
                logger.LogWarning(
                    "Mapeamento da questão {QuestionId}: container encontrado: NÃO; clique permitido: NÃO",
                    normalizedQuestionId);
                return AnswerFillResult.Fail(
                    $"A questão {normalizedQuestionId} não foi encontrada na tentativa.");
            }

            logger.LogInformation(
                "Mapeamento da questão {QuestionId}: container encontrado: SIM",
                normalizedQuestionId);

            var alternative = await FindAlternativeAsync(
                question,
                normalizedOption,
                cancellationToken);

            if (alternative is null)
            {
                logger.LogWarning(
                    "Mapeamento da questão {QuestionId} para alternativa {SelectedOption}: elemento encontrado: NÃO; clique permitido: NÃO",
                    normalizedQuestionId,
                    normalizedOption);
                return AnswerFillResult.Fail(
                    $"A alternativa {normalizedOption} não foi encontrada na questão {normalizedQuestionId}.");
            }

            var visible = await alternative.TryWaitForVisibleAsync(
                TimeSpan.FromMilliseconds(_options.ClickTimeout),
                cancellationToken);

            if (!visible)
            {
                return AnswerFillResult.Fail(
                    $"A alternativa {normalizedOption} da questão {normalizedQuestionId} não ficou visível.");
            }

            if (!await alternative.IsEnabledAsync(cancellationToken))
            {
                return AnswerFillResult.Fail(
                    $"A alternativa {normalizedOption} da questão {normalizedQuestionId} está desabilitada.");
            }

            logger.LogInformation(
                "Mapeamento da questão {QuestionId} para alternativa {SelectedOption}: elemento encontrado: SIM; clique permitido: SIM; dry-run: {DryRun}",
                normalizedQuestionId,
                normalizedOption,
                _options.DryRun);

            if (_options.DryRun)
            {
                return AnswerFillResult.Ok(
                    $"Mapeamento validado em dry-run para a alternativa {normalizedOption} da questão {normalizedQuestionId}.");
            }

            await alternative.ScrollIntoViewAsync(cancellationToken);
            await alternative.ClickAsync(cancellationToken);

            logger.LogInformation(
                "Alternativa {SelectedOption} marcada para a questão {QuestionId}",
                normalizedOption,
                normalizedQuestionId);

            return AnswerFillResult.Ok(
                $"Alternativa {normalizedOption} marcada na questão {normalizedQuestionId}.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Preenchimento da questão {QuestionId} cancelado",
                questionId);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Erro ao marcar a alternativa {SelectedOption} na questão {QuestionId}",
                selectedOption,
                questionId);
            return AnswerFillResult.Fail(
                $"Não foi possível marcar a alternativa {selectedOption} na questão {questionId}: {exception.Message}");
        }
    }

    private async Task<IAutomationLocator?> FindQuestionAsync(
        IAutomationPage page,
        string questionId,
        CancellationToken cancellationToken)
    {
        var questions = await SelectQuestionContainersAsync(page, cancellationToken);
        var count = await questions.CountAsync(cancellationToken);

        logger.LogInformation(
            "Questão solicitada: {QuestionId}; containers de questão detectados na página: {QuestionCount}",
            questionId,
            count);

        for (var index = 0; index < count; index++)
        {
            var candidate = questions.Nth(index);
            var visibleNumber = await ReadFirstTextAsync(
                candidate.GetLocator(_options.QuestionNumberSelector),
                cancellationToken);
            var identifiers = new[]
            {
                visibleNumber,
                await candidate.GetAttributeAsync("data-question-number", cancellationToken),
                await candidate.GetAttributeAsync("data-question-id", cancellationToken),
                await candidate.GetAttributeAsync("id", cancellationToken)
            };

            if (identifiers.Any(identifier => IdentifierMatches(identifier, questionId)))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task<IAutomationLocator?> FindAlternativeAsync(
        IAutomationLocator question,
        string selectedOption,
        CancellationToken cancellationToken)
    {
        var alternatives = question.GetLocator(_options.AlternativeSelector);
        var count = await alternatives.CountAsync(cancellationToken);

        logger.LogInformation(
            "Alternativas detectadas dentro do container da questão: {AlternativeCount}",
            count);

        for (var index = 0; index < count; index++)
        {
            var alternative = alternatives.Nth(index);
            var input = alternative.GetLocator(
                "input[type='radio'], input[type='checkbox']");
            var inputValue = await input.CountAsync(cancellationToken) > 0
                ? await input.Nth(0).GetAttributeAsync("value", cancellationToken)
                : null;
            var identifiers = new[]
            {
                await alternative.GetAttributeAsync("data-alternative-id", cancellationToken),
                await alternative.GetAttributeAsync("data-option-id", cancellationToken),
                await alternative.GetAttributeAsync("data-option", cancellationToken),
                inputValue
            };

            if (identifiers.Any(identifier =>
                    string.Equals(
                        identifier?.Trim(),
                        selectedOption,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return alternative;
            }

            var visibleText = await alternative.InnerTextAsync(cancellationToken);

            if (LeadingAlternativeIdentifierMatches(visibleText, selectedOption))
            {
                return alternative;
            }
        }

        return null;
    }

    private async Task<bool> TryNavigateToQuestionAsync(
        IAutomationPage page,
        string questionId,
        CancellationToken cancellationToken)
    {
        if (IsOutsideActiveAttempt(page.Url))
        {
            logger.LogWarning(
                "Navegação para a questão {QuestionId} recusada porque a página atual não é uma tentativa ativa. URL: {CurrentUrl}",
                questionId,
                page.Url);
            return false;
        }

        var visibleQuestions = await SelectQuestionContainersAsync(
            page,
            cancellationToken);
        var visibleCount = await visibleQuestions.CountAsync(cancellationToken);
        var hasTargetNumber = TryParseQuestionNumber(
            questionId,
            out var targetQuestionNumber);
        var currentQuestionNumber = visibleCount == 1
            ? await ReadQuestionNumberAsync(
                visibleQuestions.Nth(0),
                cancellationToken)
            : null;

        if (visibleCount == 1 &&
            hasTargetNumber &&
            currentQuestionNumber is not null &&
            targetQuestionNumber > currentQuestionNumber.Value)
        {
            var nextPageButton = await FindFirstInteractableAsync(
                page.GetLocator(_options.NextPageSelector),
                cancellationToken);

            if (nextPageButton is not null)
            {
                logger.LogInformation(
                    "Questionário paginado detectado; avançando da questão {CurrentQuestion} para alcançar a questão {QuestionId}",
                    currentQuestionNumber,
                    questionId);
                var previousUrl = page.Url;
                await nextPageButton.ClickAsync(cancellationToken);
                await page.WaitForUrlChangeAsync(previousUrl, cancellationToken);
                await page.WaitForDomContentLoadedAsync(cancellationToken);

                if (IsOutsideActiveAttempt(page.Url))
                {
                    logger.LogWarning(
                        "A navegação deixou a tentativa ativa inesperadamente ao procurar a questão {QuestionId}. URL: {CurrentUrl}",
                        questionId,
                        page.Url);
                    return false;
                }

                logger.LogInformation(
                    "Próxima página carregada para localizar a questão {QuestionId}. URL: {QuestionUrl}",
                    questionId,
                    page.Url);
                return true;
            }
        }

        var navigationItems = page.GetLocator(_options.QuestionNavigationSelector);
        var count = await navigationItems.CountAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            var navigationItem = navigationItems.Nth(index);
            var text = (await navigationItem.InnerTextAsync(cancellationToken)).Trim();
            var identifiers = new[]
            {
                text,
                await navigationItem.GetAttributeAsync("title", cancellationToken),
                await navigationItem.GetAttributeAsync("aria-label", cancellationToken),
                await navigationItem.GetAttributeAsync("data-question-number", cancellationToken)
            };

            if (!identifiers.Any(identifier => IdentifierMatches(identifier, questionId)))
            {
                continue;
            }

            var href = await navigationItem.GetAttributeAsync("href", cancellationToken);

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!TryResolveActiveAttemptUrl(page.Url, href, out var targetUrl))
            {
                logger.LogWarning(
                    "Destino do índice da questão {QuestionId} ignorado por não representar uma tentativa ativa: {TargetUrl}",
                    questionId,
                    href);
                continue;
            }

            logger.LogInformation(
                "Questão {QuestionId} não está na página atual; navegando diretamente pelo índice do questionário para {TargetUrl}",
                questionId,
                targetUrl);
            await page.NavigateAsync(targetUrl, cancellationToken);
            await page.WaitForDomContentLoadedAsync(cancellationToken);
            return true;
        }

        if (visibleCount == 1 &&
            hasTargetNumber &&
            currentQuestionNumber is not null &&
            targetQuestionNumber <= currentQuestionNumber.Value)
        {
            logger.LogWarning(
                "Próxima página não será usada para localizar a questão {QuestionId}; questão visível: {CurrentQuestion}. Isso evita finalizar ou avançar incorretamente a tentativa",
                questionId,
                currentQuestionNumber);
        }

        return false;
    }

    private async Task<int?> ReadQuestionNumberAsync(
        IAutomationLocator question,
        CancellationToken cancellationToken)
    {
        var identifiers = new[]
        {
            await ReadFirstTextAsync(
                question.GetLocator(_options.QuestionNumberSelector),
                cancellationToken),
            await question.GetAttributeAsync("data-question-number", cancellationToken),
            await question.GetAttributeAsync("data-question-id", cancellationToken),
            await question.GetAttributeAsync("id", cancellationToken)
        };

        foreach (var identifier in identifiers)
        {
            var digits = new string((identifier ?? string.Empty)
                .Where(char.IsDigit)
                .ToArray());

            if (int.TryParse(digits, out var number))
            {
                return number;
            }
        }

        return null;
    }

    private static bool TryParseQuestionNumber(string questionId, out int number) =>
        int.TryParse(
            new string(questionId.Where(char.IsDigit).ToArray()),
            out number);

    private static bool TryResolveActiveAttemptUrl(
        string currentUrl,
        string href,
        out string targetUrl)
    {
        targetUrl = string.Empty;

        if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var baseUri) ||
            !Uri.TryCreate(baseUri, href, out var targetUri) ||
            !targetUri.AbsolutePath.EndsWith(
                "/mod/quiz/attempt.php",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targetUrl = targetUri.ToString();
        return true;
    }

    private static bool IsOutsideActiveAttempt(string url) =>
        !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
        !uri.AbsolutePath.EndsWith(
            "/mod/quiz/attempt.php",
            StringComparison.OrdinalIgnoreCase);

    private static async Task<IAutomationLocator?> FindFirstInteractableAsync(
        IAutomationLocator candidates,
        CancellationToken cancellationToken)
    {
        var count = await candidates.CountAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            var candidate = candidates.Nth(index);

            if (await candidate.IsVisibleAsync(cancellationToken) &&
                await candidate.IsEnabledAsync(cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IdentifierMatches(string? identifier, string questionId)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        if (string.Equals(
                identifier.Trim(),
                questionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var numericIdentifier = new string(identifier.Where(char.IsDigit).ToArray());

        if (string.Equals(
                numericIdentifier,
                questionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var trailingDigits = new string(identifier
            .Reverse()
            .TakeWhile(char.IsDigit)
            .Reverse()
            .ToArray());
        return trailingDigits.Length > 0 &&
            string.Equals(trailingDigits, questionId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IAutomationLocator> SelectQuestionContainersAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        foreach (var selector in _options.QuestionSelector.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidates = page.GetLocator(selector);

            if (await candidates.CountAsync(cancellationToken) > 0)
            {
                return candidates;
            }
        }

        return page.GetLocator(_options.QuestionSelector);
    }

    private static async Task<string> ReadFirstTextAsync(
        IAutomationLocator locator,
        CancellationToken cancellationToken) =>
        await locator.CountAsync(cancellationToken) > 0
            ? await locator.Nth(0).InnerTextAsync(cancellationToken)
            : string.Empty;

    private static bool LeadingAlternativeIdentifierMatches(
        string text,
        string selectedOption)
    {
        if (selectedOption.Length != 1 || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.TrimStart();

        if (normalized.Length == 0 ||
            char.ToUpperInvariant(normalized[0]) !=
            char.ToUpperInvariant(selectedOption[0]))
        {
            return false;
        }

        return normalized.Length == 1 ||
            normalized[1] is '.' or ')' or ':' or '-' ||
            char.IsWhiteSpace(normalized[1]);
    }

    private void ValidateOptions()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.QuestionSelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.QuestionNumberSelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.AlternativeSelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.QuestionNavigationSelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.NextPageSelector);

        if (_options.ClickTimeout <= 0)
        {
            throw new InvalidOperationException(
                "AnswerFill:ClickTimeout deve ser maior que zero.");
        }

        if (_options.ManualAnswerTimeout <= 0)
        {
            throw new InvalidOperationException(
                "AnswerFill:ManualAnswerTimeout deve ser maior que zero.");
        }

        if (_options.MinimumConfidence is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "AnswerFill:MinimumConfidence deve estar entre zero e um.");
        }
    }
}
