using AvaBot.Application.Services;
using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Models;

namespace AvaBot.Tests;

public sealed class InferenceValidatorTests
{
    private readonly InferenceValidator _validator = new();

    [Fact]
    public void ValidateAndDeserialize_ReturnsValidResult()
    {
        const string json =
            """
            {
              "results": [
                {
                  "questionNumber": 1,
                  "status": "answered",
                  "selectedAlternative": "A",
                  "confidence": 0.9,
                  "calculatedAnswer": "Resultado",
                  "reasoning": "Justificativa curta.",
                  "missingData": null,
                  "warning": null,
                  "validation": {"hasSufficientData":true,"alternativeMatchesReasoning":true}
                }
              ]
            }
            """;

        var result = _validator.ValidateAndDeserialize(
            json,
            new InferenceRequestDto([TestQuestionFactory.Create(1)]));

        Assert.Single(result);
        Assert.Equal("A", result[0].SelectedAlternative);
    }

    [Fact]
    public void ValidateAndDeserialize_RejectsInvalidJson()
    {
        Assert.Throws<InferenceValidationException>(() =>
            _validator.ValidateAndDeserialize(
                "{invalid",
                new InferenceRequestDto([TestQuestionFactory.Create(1)])));
    }

    [Fact]
    public void ValidateAndDeserialize_RejectsUnknownAlternative()
    {
        const string json =
            """
            {"results":[{"questionNumber":1,"status":"answered","selectedAlternative":"Z","confidence":0.5,"calculatedAnswer":"x","reasoning":"x","missingData":null,"warning":null,"validation":{"hasSufficientData":true,"alternativeMatchesReasoning":true}}]}
            """;

        Assert.Throws<InferenceValidationException>(() =>
            _validator.ValidateAndDeserialize(
                json,
                new InferenceRequestDto([TestQuestionFactory.Create(1)])));
    }

    [Fact]
    public void ValidateAndDeserialize_RejectsDifferentResultCount()
    {
        Assert.Throws<InferenceValidationException>(() =>
            _validator.ValidateAndDeserialize(
                "{\"results\":[]}",
                new InferenceRequestDto([TestQuestionFactory.Create(1)])));
    }

    [Fact]
    public void ValidateAndDeserialize_AcceptsInsufficientDataWithoutAlternative()
    {
        const string json =
            """
            {"results":[{"questionNumber":1,"status":"insufficient_data","selectedAlternative":null,"confidence":0.1,"calculatedAnswer":null,"reasoning":"O gráfico necessário não foi fornecido.","missingData":"Gráfico","warning":null,"validation":{"hasSufficientData":false,"alternativeMatchesReasoning":false}}]}
            """;

        var result = _validator.ValidateAndDeserialize(
            json,
            new InferenceRequestDto([TestQuestionFactory.Create(1)]));

        Assert.Single(result);
        Assert.Equal(InferenceStatuses.InsufficientData, result[0].Status);
        Assert.Null(result[0].SelectedAlternative);
    }

    [Fact]
    public void ValidateAndDeserialize_RejectsAlternativeThatDisagreesWithReasoningConclusion()
    {
        const string json =
            """
            {"results":[{"questionNumber":1,"status":"answered","selectedAlternative":"A","confidence":0.9,"calculatedAnswer":"20","reasoning":"O cálculo resulta em 20, portanto alternativa B.","missingData":null,"warning":null,"validation":{"hasSufficientData":true,"alternativeMatchesReasoning":true}}]}
            """;

        var exception = Assert.Throws<InferenceValidationException>(() =>
            _validator.ValidateAndDeserialize(
                json,
                new InferenceRequestDto([TestQuestionFactory.Create(1)])));

        Assert.Contains("selectedAlternative", exception.Message);
        Assert.Contains("alternativa 'B'", exception.Message);
    }

    [Fact]
    public void ValidateAndDeserialize_DoesNotTreatPortugueseArticleAsAlternativeA()
    {
        const string json =
            """
            {"results":[{"questionNumber":1,"status":"answered","selectedAlternative":"B","confidence":0.9,"calculatedAnswer":"20","reasoning":"A alternativa a ser escolhida corresponde ao resultado calculado.","missingData":null,"warning":null,"validation":{"hasSufficientData":true,"alternativeMatchesReasoning":true}}]}
            """;

        var result = _validator.ValidateAndDeserialize(
            json,
            new InferenceRequestDto([TestQuestionFactory.Create(1)]));

        Assert.Equal("B", Assert.Single(result).SelectedAlternative);
    }

    [Fact]
    public void ValidateAndDeserialize_RejectsAlternativeWhenStatusIsInsufficientData()
    {
        const string json =
            """
            {"results":[{"questionNumber":1,"status":"insufficient_data","selectedAlternative":"A","confidence":0.2,"calculatedAnswer":null,"reasoning":"A imagem está ausente.","missingData":"Imagem","warning":null,"validation":{"hasSufficientData":false,"alternativeMatchesReasoning":false}}]}
            """;

        Assert.Throws<InferenceValidationException>(() =>
            _validator.ValidateAndDeserialize(
                json,
                new InferenceRequestDto([TestQuestionFactory.Create(1)])));
    }
}
