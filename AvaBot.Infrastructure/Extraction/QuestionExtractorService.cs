using System.Diagnostics;
using AvaBot.Domain.Models;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Extraction;

public sealed class QuestionExtractorService(
    IOptions<QuestionExtractionOptions> options,
    ILogger<QuestionExtractorService> logger) : IQuestionExtractor
{
    private const string RichTextExtractionScript =
        """
        element => {
          const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
          const formulaFrom = node => {
            const annotation = node.matches?.('annotation[encoding="application/x-tex"]')
              ? node
              : node.querySelector?.('annotation[encoding="application/x-tex"]');
            if (annotation) return normalize(annotation.textContent);
            if (node.matches?.('script[type*="math/tex"]')) return normalize(node.textContent);
            return normalize(node.getAttribute?.('data-tex') ||
              node.getAttribute?.('data-latex') ||
              node.getAttribute?.('aria-label'));
          };
          const visit = node => {
            if (node.nodeType === Node.TEXT_NODE) return node.nodeValue || '';
            if (node.nodeType !== Node.ELEMENT_NODE) return '';
            const tag = node.tagName.toLowerCase();
            if (['script', 'style', 'noscript'].includes(tag) &&
                !node.matches('script[type*="math/tex"]')) return '';
            if (node.matches('math, mjx-container, .katex, script[type*="math/tex"]')) {
              const formula = formulaFrom(node) || normalize(node.textContent);
              return formula ? ` \\(${formula}\\) ` : '';
            }
            if (tag === 'img') {
              const description = normalize(node.alt || node.title || node.getAttribute('aria-label'));
              return description ? ` [imagem: ${description}] ` : ' [conteúdo visual sem descrição] ';
            }
            if (tag === 'svg' || tag === 'canvas') {
              const description = normalize(node.getAttribute('aria-label') ||
                node.querySelector?.('title')?.textContent);
              return description ? ` [gráfico: ${description}] ` : ' [conteúdo visual sem descrição] ';
            }
            if (tag === 'sup') return `^(${Array.from(node.childNodes).map(visit).join('')})`;
            if (tag === 'sub') return `_(${Array.from(node.childNodes).map(visit).join('')})`;
            const content = Array.from(node.childNodes).map(visit).join('');
            return ['p','div','li','tr','br','h1','h2','h3','h4'].includes(tag)
              ? `${content}\n`
              : content;
          };
          return visit(element)
            .replace(/[ \t]+\n/g, '\n')
            .replace(/\n{3,}/g, '\n\n')
            .replace(/[ \t]{2,}/g, ' ')
            .trim();
        }
        """;

    private const string FormulaExtractionScript =
        """
        element => {
          const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
          const results = [];
          const seen = new Set();
          const add = value => {
            const normalized = normalize(value);
            if (normalized && !seen.has(normalized)) {
              seen.add(normalized);
              results.push(normalized);
            }
          };
          const candidates = element.querySelectorAll(
            'annotation[encoding="application/x-tex"], script[type*="math/tex"], math, mjx-container, .katex, [data-tex], [data-latex]');
          for (const node of candidates) {
            if (node.matches('annotation[encoding="application/x-tex"], script[type*="math/tex"]')) {
              add(node.textContent);
              continue;
            }
            const annotation = node.querySelector?.('annotation[encoding="application/x-tex"]');
            add(annotation?.textContent || node.getAttribute('data-tex') ||
              node.getAttribute('data-latex') || node.getAttribute('aria-label') ||
              node.textContent);
          }
          return results;
        }
        """;

    private readonly QuestionExtractionOptions _options = options.Value;

    public async Task<ExtractedQuizData> ExtractAsync(
        IPage page,
        bool captureScreenshots,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            var title = await ReadFirstTextAsync(
                page.Locator(_options.QuizTitleSelector),
                cancellationToken);
            var originalUrl = page.Url;
            var questionLocators = await SelectQuestionContainersAsync(
                page,
                cancellationToken);
            var questionCount = await questionLocators.CountAsync();
            var questions = await ExtractVisibleQuestionsAsync(
                questionLocators,
                captureScreenshots,
                cancellationToken);

            logger.LogInformation(
                "Iniciando extração somente leitura; objetos de questão encontrados: {QuestionCount}; modo visual: {VisualMode}",
                questionCount,
                captureScreenshots);

            if (questionCount <= 1)
            {
                var navigationUrls = await GetQuestionNavigationUrlsAsync(
                    page,
                    cancellationToken);

                logger.LogInformation(
                    "Questionário paginado: {PageCount} página(s) de questão identificada(s)",
                    navigationUrls.Count);

                foreach (var navigationUrl in navigationUrls.Where(url =>
                             !string.Equals(url, originalUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    logger.LogDebug("Lendo página de questão {QuestionPageUrl}", navigationUrl);
                    await page.GotoAsync(navigationUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    });

                    questions.AddRange(await ExtractVisibleQuestionsAsync(
                        await SelectQuestionContainersAsync(page, cancellationToken),
                        captureScreenshots,
                        cancellationToken));
                }

                if (!string.Equals(page.Url, originalUrl, StringComparison.OrdinalIgnoreCase))
                {
                    await page.GotoAsync(originalUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    });
                }
            }

            questions = questions
                .GroupBy(question => new
                {
                    Number = question.NumberText.Trim(),
                    Statement = question.Statement.Trim()
                })
                .Select(group => group.First())
                .ToList();

            stopwatch.Stop();
            logger.LogInformation(
                "Extração concluída em {ElapsedMilliseconds} ms; questões: {QuestionCount}; alternativas: {AlternativeCount}",
                stopwatch.ElapsedMilliseconds,
                questions.Count,
                questions.Sum(question => question.Alternatives.Count));

            return new ExtractedQuizData(title, questions);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha durante a extração somente leitura das questões");
            throw;
        }
    }

    private async Task<List<ExtractedQuestionData>> ExtractVisibleQuestionsAsync(
        ILocator questionLocators,
        bool captureScreenshots,
        CancellationToken cancellationToken)
    {
        var count = await questionLocators.CountAsync();
        var questions = new List<ExtractedQuestionData>(count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            questions.Add(await ExtractQuestionAsync(
                questionLocators.Nth(index),
                index + 1,
                captureScreenshots,
                cancellationToken));
        }

        return questions;
    }

    private async Task<IReadOnlyList<string>> GetQuestionNavigationUrlsAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        var locators = page.Locator(_options.QuestionNavigationSelector);
        var count = await locators.CountAsync();
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var href = await locators.Nth(index).GetAttributeAsync("href");

            if (string.IsNullOrWhiteSpace(href) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(new Uri(page.Url), href, out var absoluteUrl))
            {
                continue;
            }

            urls.Add(absoluteUrl.ToString());
        }

        return urls.ToArray();
    }

    private async Task<ExtractedQuestionData> ExtractQuestionAsync(
        ILocator question,
        int fallbackNumber,
        bool captureScreenshot,
        CancellationToken cancellationToken)
    {
        var number = await ReadFirstTextAsync(
            question.Locator(_options.QuestionNumberSelector),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(number))
        {
            number = await question.GetAttributeAsync("data-question-number")
                ?? await question.GetAttributeAsync("data-question-id");
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            number = fallbackNumber.ToString();
        }
        var statement = await ReadRichTextAsync(
            question.Locator(_options.StatementSelector),
            cancellationToken);

        var alternatives = await ExtractAlternativesAsync(question, cancellationToken);
        var images = await ExtractImagesAsync(question, cancellationToken);
        var tables = await ExtractTablesAsync(question, cancellationToken);
        var codeBlocks = await ExtractCodeBlocksAsync(question, cancellationToken);
        var formulas = await ExtractFormulasAsync(question, cancellationToken);
        var requiresScreenshot = captureScreenshot || images.Count > 0;
        var screenshotDataUrl = requiresScreenshot
            ? await CaptureQuestionScreenshotAsync(question, cancellationToken)
            : string.Empty;

        if (!captureScreenshot && images.Count > 0)
        {
            logger.LogInformation(
                "Conteúdo visual detectado na questão {QuestionNumber}; captura seletiva do container realizada",
                number);
        }

        logger.LogDebug(
            "Questão extraída: {QuestionNumber}; texto: {StatementLength} caracteres; alternativas: {AlternativeCount}; fórmulas: {FormulaCount}; imagens: {ImageCount}; tabelas: {TableCount}; blocos de código: {CodeBlockCount}; screenshot: {ScreenshotBytes} bytes",
            number,
            statement.Length,
            alternatives.Count,
            formulas.Count,
            images.Count,
            tables.Count,
            codeBlocks.Count,
            EstimateDataUrlBytes(screenshotDataUrl));

        return new ExtractedQuestionData(
            number,
            statement,
            alternatives,
            images,
            tables,
            codeBlocks,
            formulas,
            screenshotDataUrl);
    }

    private async Task<ILocator> SelectQuestionContainersAsync(
        IPage page,
        CancellationToken cancellationToken)
    {
        foreach (var selector in _options.QuestionSelector.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = page.Locator(selector);

            if (await candidates.CountAsync() > 0)
            {
                logger.LogDebug(
                    "Seletor de container de questão selecionado: {QuestionSelector}",
                    selector);
                return candidates;
            }
        }

        return page.Locator(_options.QuestionSelector);
    }

    private async Task<IReadOnlyList<ExtractedAlternativeData>> ExtractAlternativesAsync(
        ILocator question,
        CancellationToken cancellationToken)
    {
        var locators = question.Locator(_options.AlternativeSelector);
        var count = await locators.CountAsync();
        var alternatives = new List<ExtractedAlternativeData>(count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var alternative = locators.Nth(index);
            var id = await alternative.GetAttributeAsync("data-alternative-id")
                ?? await alternative.GetAttributeAsync("data-option-id")
                ?? await ReadInputValueAsync(alternative);
            alternatives.Add(new ExtractedAlternativeData(
                id ?? string.Empty,
                await ExtractRichTextAsync(alternative, cancellationToken)));
        }

        return alternatives;
    }

    private static async Task<IReadOnlyList<ExtractedImageData>> ExtractImagesAsync(
        ILocator question,
        CancellationToken cancellationToken)
    {
        var locators = question.Locator("img");
        var count = await locators.CountAsync();
        var images = new List<ExtractedImageData>(count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = locators.Nth(index);
            images.Add(new ExtractedImageData(
                await image.GetAttributeAsync("alt") ?? string.Empty,
                await image.GetAttributeAsync("src") ?? string.Empty));
        }

        return images;
    }

    private static async Task<IReadOnlyList<ExtractedTableData>> ExtractTablesAsync(
        ILocator question,
        CancellationToken cancellationToken)
    {
        var locators = question.Locator("table");
        var count = await locators.CountAsync();
        var tables = new List<ExtractedTableData>(count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var table = locators.Nth(index);
            var headers = await table.Locator("thead th").AllInnerTextsAsync();
            var rowLocators = table.Locator("tbody tr");
            var rowCount = await rowLocators.CountAsync();
            var rows = new List<IReadOnlyList<string>>(rowCount);

            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(await rowLocators.Nth(rowIndex).Locator("th, td").AllInnerTextsAsync());
            }

            tables.Add(new ExtractedTableData(headers, rows));
        }

        return tables;
    }

    private static async Task<IReadOnlyList<ExtractedCodeBlockData>> ExtractCodeBlocksAsync(
        ILocator question,
        CancellationToken cancellationToken)
    {
        var locators = question.Locator("pre code, pre:not(:has(code))");
        var count = await locators.CountAsync();
        var codeBlocks = new List<ExtractedCodeBlockData>(count);

        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var code = locators.Nth(index);
            var language = await code.GetAttributeAsync("data-language")
                ?? await code.GetAttributeAsync("class")
                ?? string.Empty;
            codeBlocks.Add(new ExtractedCodeBlockData(
                language,
                await code.InnerTextAsync()));
        }

        return codeBlocks;
    }

    private static async Task<string?> ReadInputValueAsync(ILocator locator)
    {
        var input = locator.Locator("input").First;
        return await input.CountAsync() > 0
            ? await input.GetAttributeAsync("value")
            : null;
    }

    private static async Task<string> ReadFirstTextAsync(
        ILocator locator,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await locator.CountAsync() > 0
            ? await locator.First.InnerTextAsync()
            : string.Empty;
    }

    private static async Task<string> ReadRichTextAsync(
        ILocator locator,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await locator.CountAsync() > 0
            ? await ExtractRichTextAsync(locator.First, cancellationToken)
            : string.Empty;
    }

    private static async Task<string> ExtractRichTextAsync(
        ILocator locator,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await locator.EvaluateAsync<string>(RichTextExtractionScript);
    }

    private static async Task<IReadOnlyList<string>> ExtractFormulasAsync(
        ILocator question,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await question.EvaluateAsync<string[]>(FormulaExtractionScript);
    }

    private static async Task<string> CaptureQuestionScreenshotAsync(
        ILocator question,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = await question.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Type = ScreenshotType.Png,
            Animations = ScreenshotAnimations.Disabled
        });
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static int EstimateDataUrlBytes(string dataUrl)
    {
        var separatorIndex = dataUrl.IndexOf(',');
        return separatorIndex < 0
            ? 0
            : (dataUrl.Length - separatorIndex - 1) * 3 / 4;
    }
}
