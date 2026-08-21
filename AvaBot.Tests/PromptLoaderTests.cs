using AvaBot.Infrastructure.OpenAI;

namespace AvaBot.Tests;

public sealed class PromptLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReturnsTrimmedPrompt()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"avabot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "prompt.txt"),
                "  Prompt de teste.  ");

            var loader = new PromptLoader(directory);
            var result = await loader.LoadAsync("prompt.txt");

            Assert.Equal("Prompt de teste.", result);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsPathTraversal()
    {
        var loader = new PromptLoader(Path.GetTempPath());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            loader.LoadAsync("..\\prompt.txt"));
    }

    [Fact]
    public async Task PromptRepository_SelectsGeneralOrMathematicsPromptByVisualMode()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"avabot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "QuestionAnswerPrompt.txt"),
                "Prompt geral");
            await File.WriteAllTextAsync(
                Path.Combine(directory, "MathematicsQuestionAnswerPrompt.txt"),
                "Prompt matemático");
            var repository = new PromptRepository(new PromptLoader(directory));

            var general = await repository.GetQuestionAnswerPromptAsync(false);
            var mathematics = await repository.GetQuestionAnswerPromptAsync(true);

            Assert.Equal("Prompt geral", general);
            Assert.Equal("Prompt matemático", mathematics);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
