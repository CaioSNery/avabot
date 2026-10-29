using AvaBot.Domain.Models;
using AvaBot.Domain.Exceptions;
using AvaBot.Infrastructure.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Tests;

public sealed class QuizAutoStartTests
{
    [Fact]
    public async Task WaitForQuestionsAsync_UnavailableQuizStopsAfterTenSeconds()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<div>Ainda não foi inserida nenhuma pergunta</div><button>Voltar ao curso</button>");
        var flow = new QuestionConsoleFlow(
            null!, null!, null!, Options.Create(new QuestionExtractionOptions()),
            NullLogger<QuestionConsoleFlow>.Instance);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAsync<QuizUnavailableException>(() =>
            flow.WaitForQuestionsAsync(page, CancellationToken.None));

        Assert.InRange(stopwatch.Elapsed.TotalSeconds, 9, 20);
        Assert.Equal(0, await page.Locator(".que").CountAsync());
    }

    [Fact]
    public async Task WaitForQuestionsAsync_StartsAndConfirmsQuizOnlyOnce()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div class="quizstartbuttondiv">
              <button type="submit" onclick="
                window.startClicks = (window.startClicks || 0) + 1;
                this.parentElement.style.display = 'none';
                document.getElementById('confirm').style.display = 'block';
                return false;">Tentar responder o questionário agora</button>
            </div>
            <button id="confirm" style="display:none" onclick="
              window.confirmClicks = (window.confirmClicks || 0) + 1;
              this.style.display = 'none';
              document.body.insertAdjacentHTML('beforeend',
                '<div class=&quot;que&quot;><div class=&quot;qno&quot;>Questão 1</div></div>');
              return false;">Iniciar tentativa</button>
            """);
        var flow = new QuestionConsoleFlow(
            null!,
            null!,
            null!,
            Options.Create(new QuestionExtractionOptions
            {
                ManualStartTimeout = 2000
            }),
            NullLogger<QuestionConsoleFlow>.Instance);

        await flow.WaitForQuestionsAsync(page, CancellationToken.None);

        Assert.Equal(1, await page.EvaluateAsync<int>("window.startClicks"));
        Assert.Equal(1, await page.EvaluateAsync<int>("window.confirmClicks"));
        Assert.Equal(1, await page.Locator(".que").CountAsync());
    }

    [Fact]
    public async Task WaitForQuestionsAsync_ContinuesWhenOptionalConfirmationIsNotShown()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div class="quizstartbuttondiv">
              <button type="submit" onclick="
                window.startClicks = (window.startClicks || 0) + 1;
                this.parentElement.style.display = 'none';
                document.body.insertAdjacentHTML('beforeend',
                  '<div class=&quot;que&quot;><div class=&quot;qno&quot;>Questão 1</div></div>');
                return false;">Tentar responder o questionário agora</button>
            </div>
            """);
        var flow = new QuestionConsoleFlow(
            null!,
            null!,
            null!,
            Options.Create(new QuestionExtractionOptions
            {
                ManualStartTimeout = 2000
            }),
            NullLogger<QuestionConsoleFlow>.Instance);

        await flow.WaitForQuestionsAsync(page, CancellationToken.None);

        Assert.Equal(1, await page.EvaluateAsync<int>("window.startClicks"));
        Assert.Equal(1, await page.Locator(".que").CountAsync());
    }
}
