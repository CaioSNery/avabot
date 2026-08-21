using System.Text.Json;
using AvaBot.Application.Services;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvaBot.Tests;

public sealed class QuestionJsonExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesIndentedValidJson()
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"questions-{Guid.NewGuid():N}.json");
        var quiz = new Quiz(
            "Quiz",
            [
                new Question(
                    1,
                    "Enunciado",
                    [new Alternative("A", "Texto")],
                    [],
                    [],
                    [],
                    ["350000 · 0,9^10"],
                    "data:image/png;base64,AQID")
            ]);
        var exporter = new QuestionJsonExporter(
            NullLogger<QuestionJsonExporter>.Instance);

        try
        {
            await exporter.ExportAsync(quiz, outputPath);
            var json = await File.ReadAllTextAsync(outputPath);
            using var document = JsonDocument.Parse(json);

            Assert.Contains(Environment.NewLine, json);
            Assert.Equal(
                "Quiz",
                document.RootElement.GetProperty("title").GetString());
            var question = document.RootElement.GetProperty("questions")[0];
            Assert.Equal(
                "350000 · 0,9^10",
                question.GetProperty("formulas")[0].GetString());
            Assert.Equal(
                "data:image/png;base64,AQID",
                question.GetProperty("screenshotDataUrl").GetString());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
