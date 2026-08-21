using AvaBot.Domain.Models;
using AvaBot.Infrastructure.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Tests;

public sealed class QuestionExtractorServiceTests
{
    [Fact]
    public async Task ExtractAsync_PreservesMathMlSuperscriptsTablesAndScreenshot()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <main>
              <h1>Teste matemático</h1>
              <div class="que" data-question-number="1">
                <div class="qtext">
                  Calcule
                  <math aria-label="x ao quadrado">
                    <semantics>
                      <msup><mi>x</mi><mn>2</mn></msup>
                      <annotation encoding="application/x-tex">x^2</annotation>
                    </semantics>
                  </math>
                  e 0,9<sup>10</sup>.
                  <svg aria-label="gráfico crescente"><title>Curva crescente</title></svg>
                  <table><thead><tr><th>n</th></tr></thead><tbody><tr><td>1</td></tr></tbody></table>
                </div>
                <div class="answer">
                  <label><input type="radio" value="A"> x<sub>1</sub> = 10</label>
                  <label><input type="radio" value="B"> x<sub>2</sub> = 20</label>
                </div>
              </div>
            </main>
            """);
        var extractor = new QuestionExtractorService(
            Options.Create(new QuestionExtractionOptions()),
            NullLogger<QuestionExtractorService>.Instance);

        var quiz = await extractor.ExtractAsync(page, captureScreenshots: true);

        var question = Assert.Single(quiz.Questions);
        Assert.Contains("x^2", question.Formulas!);
        Assert.Contains("0,9^(10)", question.Statement);
        Assert.Contains("[gráfico: gráfico crescente]", question.Statement);
        Assert.Equal("x_(1) = 10", question.Alternatives[0].Text);
        Assert.Equal("1", question.Tables[0].Rows[0][0]);
        Assert.StartsWith("data:image/png;base64,", question.ScreenshotDataUrl);
        Assert.True(question.ScreenshotDataUrl.Length > 100);
    }

    [Fact]
    public async Task ExtractAsync_MapsMultipleQuestionsIndependentlyWithoutScreenshots()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <main>
              <div class="que" id="question-attempt-1">
                <div class="qno">Questão 1</div>
                <div class="qtext">Enunciado da primeira questão.</div>
                <div class="answer">
                  <label>a. Primeira A</label><label>b. Primeira B</label>
                </div>
              </div>
              <div class="que" id="question-attempt-2">
                <div class="qno">Questão 2</div>
                <div class="qtext">Enunciado da segunda questão.</div>
                <div class="answer">
                  <label>a. Segunda A</label><label>b. Segunda B</label>
                </div>
              </div>
            </main>
            """);
        var extractor = new QuestionExtractorService(
            Options.Create(new QuestionExtractionOptions()),
            NullLogger<QuestionExtractorService>.Instance);

        var quiz = await extractor.ExtractAsync(page, captureScreenshots: false);

        Assert.Equal(2, quiz.Questions.Count);
        Assert.Equal("Questão 1", quiz.Questions[0].NumberText);
        Assert.Equal("Enunciado da primeira questão.", quiz.Questions[0].Statement);
        Assert.Contains("Primeira B", quiz.Questions[0].Alternatives[1].Text);
        Assert.Equal("Questão 2", quiz.Questions[1].NumberText);
        Assert.Equal("Enunciado da segunda questão.", quiz.Questions[1].Statement);
        Assert.Contains("Segunda A", quiz.Questions[1].Alternatives[0].Text);
        Assert.All(quiz.Questions, question =>
            Assert.Equal(string.Empty, question.ScreenshotDataUrl));
    }

    [Fact]
    public async Task ExtractAsync_CapturesOnlyQuestionThatContainsImageOutsideVisualMode()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <main>
              <div class="que" data-question-number="1">
                <div class="qtext">
                  Interprete a imagem.
                  <img alt="Teste visual" src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAEElEQVR42mNk+M/wHwAEAQH/Ur73WQAAAABJRU5ErkJggg==">
                </div>
                <div class="answer"><label><input type="radio" value="A"> A</label></div>
              </div>
              <div class="que" data-question-number="2">
                <div class="qtext">Questão exclusivamente textual.</div>
                <div class="answer"><label><input type="radio" value="A"> A</label></div>
              </div>
            </main>
            """);
        var extractor = new QuestionExtractorService(
            Options.Create(new QuestionExtractionOptions()),
            NullLogger<QuestionExtractorService>.Instance);

        var quiz = await extractor.ExtractAsync(page, captureScreenshots: false);

        Assert.Equal(2, quiz.Questions.Count);
        Assert.Single(quiz.Questions[0].Images);
        Assert.StartsWith(
            "data:image/png;base64,",
            quiz.Questions[0].ScreenshotDataUrl);
        Assert.Empty(quiz.Questions[1].Images);
        Assert.Equal(string.Empty, quiz.Questions[1].ScreenshotDataUrl);
    }
}
