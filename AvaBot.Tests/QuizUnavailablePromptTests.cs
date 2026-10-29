using AvaBot.Application.Services;

namespace AvaBot.Tests;

public sealed class QuizUnavailablePromptTests
{
    [Theory]
    [InlineData("s", true)]
    [InlineData("Sim", true)]
    [InlineData("n", false)]
    [InlineData("Não", false)]
    public async Task ConfirmReturnAsync_RespectsUserChoice(string answer, bool expected)
    {
        using var input = new StringReader(answer);
        using var output = new StringWriter();

        var result = await QuizUnavailablePrompt.ConfirmReturnAsync(
            input, output, CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Contains("[S/N]", output.ToString());
    }

    [Fact]
    public async Task ConfirmReturnAsync_DoesNotAcceptInvalidAnswer()
    {
        using var input = new StringReader("talvez\nS");
        using var output = new StringWriter();

        Assert.True(await QuizUnavailablePrompt.ConfirmReturnAsync(
            input, output, CancellationToken.None));
        Assert.Equal(2, output.ToString().Split("[S/N]").Length - 1);
    }

    [Fact]
    public async Task ConfirmReturnAsync_AllowsCancellation()
    {
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await QuizUnavailablePrompt.ConfirmReturnAsync(input, output, cancellation.Token));
    }
}
