using System.Text.Json;
using AvaBot.Application.Services;
using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Models;

namespace AvaBot.Tests;

public sealed class MathematicalInferenceContractTests
{
    private readonly InferenceValidator _validator = new();

    [Theory]
    [InlineData("15000, 11250, 8437,50", "25%", "A depreciação é de 25%.")]
    [InlineData("a_n = 5000 + (n - 1) · 150", "150", "O juro mensal é 150.")]
    [InlineData("a_n = 200 + 15n", "200 + 15n", "A razão da progressão é 15.")]
    [InlineData("350000 · 0,9^10; 0,9^10 ≈ 0,35", "R$ 122.500,00", "O produto resulta em R$ 122.500,00.")]
    [InlineData("a_1 = 850; a_(n+1) = 1,2 · a_n", "20%", "Cada termo cresce 20%.")]
    [InlineData("a_1 = 2; a_n = a_(n-1) + 2n", "40", "Os termos 2, 6, 12 e 20 somam 40.")]
    public void ValidateAndDeserialize_AcceptsCompleteMathematicalAnswers(
        string formula,
        string calculatedAnswer,
        string reasoning)
    {
        var request = CreateRequest(formula);
        var response = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new
                {
                    questionNumber = 1,
                    status = InferenceStatuses.Answered,
                    selectedAlternative = "A",
                    confidence = 0.98,
                    calculatedAnswer,
                    reasoning,
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

        var results = _validator.ValidateAndDeserialize(response, request);

        Assert.Single(results);
        Assert.Equal(InferenceStatuses.Answered, results[0].Status);
        Assert.Equal("A", results[0].SelectedAlternative);
    }

    [Fact]
    public void ValidateAndDeserialize_AcceptsActuallyIncompleteQuestionWithoutGuessing()
    {
        var request = CreateRequest(string.Empty);
        const string response =
            """
            {"results":[{"questionNumber":1,"status":"insufficient_data","selectedAlternative":null,"confidence":0,"calculatedAnswer":null,"reasoning":"Não é possível calcular sem a expressão que define x.","missingData":"Expressão matemática que define x.","warning":null,"validation":{"hasSufficientData":false,"alternativeMatchesReasoning":false}}]}
            """;

        var result = _validator.ValidateAndDeserialize(response, request);

        Assert.Null(result[0].SelectedAlternative);
        Assert.Equal(InferenceStatuses.InsufficientData, result[0].Status);
    }

    [Fact]
    public void ValidateAndDeserialize_RejectsInsufficientDataWithoutMissingDataDescription()
    {
        var request = CreateRequest(string.Empty);
        const string response =
            """
            {"results":[{"questionNumber":1,"status":"insufficient_data","selectedAlternative":null,"confidence":0,"calculatedAnswer":null,"reasoning":"Dados ausentes.","missingData":null,"warning":null,"validation":{"hasSufficientData":false,"alternativeMatchesReasoning":false}}]}
            """;

        Assert.Throws<InferenceValidationException>(() =>
            _validator.ValidateAndDeserialize(response, request));
    }

    [Fact]
    public void QuestionBatchBuilder_PreservesFormulaTableAndScreenshot()
    {
        var request = CreateRequest("a_n = 200 + 15n");

        var batch = new QuestionBatchBuilder().Build(request);

        Assert.Equal("a_n = 200 + 15n", batch.Questions[0].Formulas[0]);
        Assert.Single(batch.Questions[0].Tables);
        Assert.True(batch.Questions[0].ScreenshotPresent);
    }

    private static InferenceRequestDto CreateRequest(string formula) =>
        new(
        [
            new QuestionRequestDto
            {
                QuestionNumber = 1,
                Statement = "Resolva a questão matemática.",
                Alternatives =
                [
                    new AlternativeRequestDto("A", "Resultado calculado"),
                    new AlternativeRequestDto("B", "Outro resultado")
                ],
                Formulas = string.IsNullOrWhiteSpace(formula) ? [] : [formula],
                Tables = [new TableRequestDto(["n"], [["1"]])],
                ScreenshotDataUrl = "data:image/png;base64,AQID"
            }
        ]);
}
