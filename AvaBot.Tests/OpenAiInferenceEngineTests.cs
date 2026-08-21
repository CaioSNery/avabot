using System.Text.Json;
using AvaBot.Application.Services;
using AvaBot.Domain.Models;
using AvaBot.Infrastructure.OpenAI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvaBot.Tests;

public sealed class OpenAiInferenceEngineTests
{
    private const string PrimaryModel = "modelo-economico";
    private const string FallbackModel = "modelo-forte";

    [Fact]
    public async Task ExecuteAsync_AcceptsReliablePrimaryWithoutFallback()
    {
        var client = new MockOpenAiClient(Result(Answered(0.93, "B")));

        var results = await ExecuteAsync(client);

        Assert.Equal("B", Assert.Single(results).SelectedAlternative);
        Assert.Equal(1, client.CallCount);
        Assert.Equal([PrimaryModel], client.Models);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFallbackOnceForLowConfidencePrimary()
    {
        var client = new MockOpenAiClient(
            Result(Answered(0.52, "A")),
            Result(Answered(0.93, "B")));

        var results = await ExecuteAsync(client);

        Assert.Equal("B", Assert.Single(results).SelectedAlternative);
        Assert.Equal(2, client.CallCount);
        Assert.Equal([PrimaryModel, FallbackModel], client.Models);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFallbackForInsufficientDataPrimary()
    {
        var client = new MockOpenAiClient(
            Result(InsufficientData()),
            Result(Answered(0.91, "A")));

        var results = await ExecuteAsync(client);

        Assert.Equal("A", Assert.Single(results).SelectedAlternative);
        Assert.Equal([PrimaryModel, FallbackModel], client.Models);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFallbackForInvalidPrimaryJson()
    {
        var client = new MockOpenAiClient(
            new OpenAiClientResult("{json-invalido", 4, 2, 6),
            Result(Answered(0.94, "B")));

        var results = await ExecuteAsync(client);

        Assert.Equal("B", Assert.Single(results).SelectedAlternative);
        Assert.Equal(2, client.CallCount);
        Assert.Equal([PrimaryModel, FallbackModel], client.Models);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFallbackForAlternativeOutsideQuestion()
    {
        var client = new MockOpenAiClient(
            Result(Answered(0.98, "G")),
            Result(Answered(0.90, "A")));

        var results = await ExecuteAsync(client);

        Assert.Equal("A", Assert.Single(results).SelectedAlternative);
        Assert.Equal(2, client.CallCount);
        Assert.Equal([PrimaryModel, FallbackModel], client.Models);
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsPrimaryAtExactThreshold()
    {
        var client = new MockOpenAiClient(Result(Answered(0.75, "A")));

        var results = await ExecuteAsync(client);

        Assert.Equal("A", Assert.Single(results).SelectedAlternative);
        Assert.Equal(1, client.CallCount);
        Assert.Equal([PrimaryModel], client.Models);
    }

    [Fact]
    public async Task ExecuteAsync_SendsOnlyOneQuestionInEachPrimaryAndFallbackRequest()
    {
        var client = new MockOpenAiClient(
            Result(Answered(0.51, "A")),
            Result(Answered(0.90, "B")),
            Result(Answered(0.88, "A", 2)));
        var request = new InferenceRequestDto(
        [
            TestQuestionFactory.Create(1),
            TestQuestionFactory.Create(2, "Segundo enunciado")
        ]);

        var results = await ExecuteAsync(client, request);

        Assert.Equal(2, results.Count);
        Assert.Equal(3, client.CallCount);
        Assert.All(client.Requests, item => Assert.Single(item.Questions));
        Assert.All(client.Requests, item => Assert.Null(item.ResponseToReview));
        Assert.Equal([PrimaryModel, FallbackModel, PrimaryModel], client.Models);
    }

    private static async Task<IReadOnlyList<InferenceResultDto>> ExecuteAsync(
        MockOpenAiClient client,
        InferenceRequestDto? request = null)
    {
        var promptDirectory = Path.Combine(
            Path.GetTempPath(),
            $"avabot-fallback-prompts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(promptDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(promptDirectory, "QuestionAnswerPrompt.txt"),
                "Responda com JSON e justificativa curta.");
            var engine = new OpenAiInferenceEngine(
                client,
                new QuestionBatchBuilder(),
                new InferenceValidator(),
                new InferenceCache(),
                new PromptRepository(new PromptLoader(promptDirectory)),
                Options.Create(new OpenAiOptions
                {
                    ApiKey = "test-key",
                    PrimaryModel = PrimaryModel,
                    FallbackModel = FallbackModel,
                    FallbackConfidenceThreshold = 0.75,
                    MaxOutputTokens = 100
                }),
                NullLogger<OpenAiInferenceEngine>.Instance);

            return await engine.ExecuteAsync(
                request ?? new InferenceRequestDto([TestQuestionFactory.Create(1)]));
        }
        finally
        {
            Directory.Delete(promptDirectory, recursive: true);
        }
    }

    private static OpenAiClientResult Result(string outputJson) =>
        new(outputJson, 10, 5, 15);

    private static string Answered(
        double confidence,
        string alternative,
        int questionNumber = 1) =>
        JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    questionNumber,
                    status = InferenceStatuses.Answered,
                    selectedAlternative = alternative,
                    confidence,
                    calculatedAnswer = (string?)null,
                    reasoning = "Aplicação direta.",
                    missingData = (string?)null,
                    warning = (string?)null,
                    validation = new
                    {
                        hasSufficientData = true,
                        alternativeMatchesReasoning = true
                    }
                }
            }
        });

    private static string InsufficientData() =>
        """
        {"results":[{"questionNumber":1,"status":"insufficient_data","selectedAlternative":null,"confidence":0.20,"calculatedAnswer":null,"reasoning":"Falta dado essencial.","missingData":"Imagem necessária.","warning":null,"validation":{"hasSufficientData":false,"alternativeMatchesReasoning":false}}]}
        """;
}
