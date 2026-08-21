using System.Text.Json;
using AvaBot.Application.Services;
using AvaBot.Domain.Models;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Question = AvaBot.Domain.Models.Questions.Question;

namespace AvaBot.Tests;

public sealed class InferenceConsoleFlowTests
{
    [Fact]
    public async Task ExecuteFromFileAsync_PrintsRequestAndResponseWithoutPlaywright()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"avabot-questions-{Guid.NewGuid():N}.json");
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        var engine = new MockInferenceEngine(
        [
            new InferenceResultDto(1, InferenceStatuses.Answered, "A", 0.9, "Resposta simulada")
        ]);
        var flow = new InferenceConsoleFlow(
            engine,
            Options.Create(new OpenAiOptions
            {
                EnableConsoleInference = true,
                PrimaryModel = "modelo-economico-de-teste",
                FallbackModel = "modelo-forte-de-teste"
            }),
            NullLogger<InferenceConsoleFlow>.Instance);

        try
        {
            var quiz = new Quiz(
                "Quiz de teste",
                [
                    new Question(
                        1,
                        "Enunciado",
                        [
                            new Alternative("A", "Alternativa A"),
                            new Alternative("B", "Alternativa B")
                        ],
                        [],
                        [],
                        [])
                ]);
            await File.WriteAllTextAsync(
                filePath,
                JsonSerializer.Serialize(quiz, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Console.SetOut(output);

            var results = await flow.ExecuteFromFileAsync(
                filePath,
                visualMode: false);

            Assert.Single(results);
            Assert.Equal(1, engine.ExecutionCount);
            Assert.Contains("JSON ENVIADO À RESPONSES API", output.ToString());
            Assert.Contains("JSON RECEBIDO DA RESPONSES API", output.ToString());
            Assert.Contains("Resposta simulada", output.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            File.Delete(filePath);
        }
    }
}
