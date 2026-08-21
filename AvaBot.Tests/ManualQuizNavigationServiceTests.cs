using AvaBot.Domain.Models;
using AvaBot.Infrastructure.Browser;
using AvaBot.Infrastructure.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace AvaBot.Tests;

public sealed class ManualQuizNavigationServiceTests
{
    [Fact]
    public async Task NavigateToSummaryAsync_FinalizesLastPageWithoutClickNavigationTimeout()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var nativePage = await browser.NewPageAsync();

        await nativePage.RouteAsync("**/*", async route =>
        {
            var content = route.Request.Url.Contains(
                "/mod/quiz/summary.php",
                StringComparison.OrdinalIgnoreCase)
                    ? "<main><h1>Resumo da tentativa</h1></main>"
                    :
                    """
                    <main>
                      <form method="get" action="/mod/quiz/summary.php">
                        <div class="que">
                          <label><input type="radio" name="q1" value="A" checked> A</label>
                        </div>
                        <input class="mod_quiz-next-nav btn btn-primary"
                               name="next"
                               type="submit"
                               value="Finalizar tentativa ...">
                      </form>
                    </main>
                    """;

            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "text/html",
                Body = content
            });
        });
        await nativePage.GotoAsync(
            "https://ava.test/mod/quiz/attempt.php?attempt=1&page=9");

        var page = new PlaywrightAutomationPage(
            nativePage,
            NullLogger<PlaywrightAutomationPage>.Instance);
        var service = new ManualQuizNavigationService(
            Options.Create(new ManualQuizNavigationOptions
            {
                Enabled = true,
                AnswerTimeout = 0,
                MaxPages = 10
            }),
            NullLogger<ManualQuizNavigationService>.Instance);

        await service.NavigateToSummaryAsync(page);

        Assert.Contains("/mod/quiz/summary.php", nativePage.Url);
    }
}
