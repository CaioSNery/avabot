using System.Text.Json;
using System.Text.RegularExpressions;
using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Models;

namespace AvaBot.Application.Services;

public sealed class InferenceValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex AlternativeConclusionRegex = new(
        @"\balternativa\s+(?:correta\s+(?:e|é)\s+)?[\""']?([A-E])[\""']?(?=\s*[\.,;:\)\]\}]|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public IReadOnlyList<InferenceResultDto> ValidateAndDeserialize(
        string json,
        InferenceRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Questions);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InferenceValidationException("A resposta da inferência está vazia.");
        }

        InferenceEnvelope envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<InferenceEnvelope>(json, SerializerOptions)
                ?? throw new InferenceValidationException(
                    "O JSON da inferência não contém um objeto válido.");
        }
        catch (JsonException exception)
        {
            throw new InferenceValidationException(
                "O motor de inferência retornou um JSON inválido.",
                exception);
        }

        var results = envelope.Results ?? [];

        if (results.Count != request.Questions.Count)
        {
            throw new InferenceValidationException(
                $"Foram enviadas {request.Questions.Count} questões, mas recebidas {results.Count} respostas.");
        }

        if (results.Select(result => result.QuestionNumber).Distinct().Count() != results.Count)
        {
            throw new InferenceValidationException(
                "A inferência retornou respostas duplicadas para a mesma questão.");
        }

        foreach (var result in results)
        {
            var question = request.Questions.FirstOrDefault(item =>
                item.QuestionNumber == result.QuestionNumber);

            if (question is null)
            {
                throw new InferenceValidationException(
                    $"A questão {result.QuestionNumber} retornada pela inferência não existe no lote enviado.");
            }

            if (!string.Equals(result.Status, InferenceStatuses.Answered, StringComparison.Ordinal) &&
                !string.Equals(result.Status, InferenceStatuses.InsufficientData, StringComparison.Ordinal))
            {
                throw new InferenceValidationException(
                    $"O status '{result.Status}' da questão {result.QuestionNumber} não é suportado.");
            }

            if (string.IsNullOrWhiteSpace(result.Reasoning))
            {
                throw new InferenceValidationException(
                    $"A justificativa da questão {result.QuestionNumber} está vazia.");
            }

            if (result.Validation is null)
            {
                throw new InferenceValidationException(
                    $"A resposta da questão {result.QuestionNumber} não contém o bloco validation.");
            }

            if (string.Equals(result.Status, InferenceStatuses.InsufficientData, StringComparison.Ordinal))
            {
                if (result.SelectedAlternative is not null)
                {
                    throw new InferenceValidationException(
                        $"A questão {result.QuestionNumber} possui dados insuficientes, mas retornou uma alternativa.");
                }

                if (string.IsNullOrWhiteSpace(result.MissingData))
                {
                    throw new InferenceValidationException(
                        $"A questão {result.QuestionNumber} foi marcada como dados insuficientes sem informar missingData.");
                }

                if (result.Validation.HasSufficientData)
                {
                    throw new InferenceValidationException(
                        $"A validação da questão {result.QuestionNumber} declara dados suficientes, mas o status é insufficient_data.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(result.SelectedAlternative))
                {
                    throw new InferenceValidationException(
                        $"A resposta da questão {result.QuestionNumber} está vazia.");
                }

                if (!question.Alternatives.Any(alternative =>
                        string.Equals(
                            alternative.Id,
                            result.SelectedAlternative,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InferenceValidationException(
                        $"A alternativa '{result.SelectedAlternative}' não existe na questão {result.QuestionNumber}.");
                }

                ValidateReasoningConsistency(result);

                if (!result.Validation.HasSufficientData ||
                    !result.Validation.AlternativeMatchesReasoning)
                {
                    throw new InferenceValidationException(
                        $"A validação interna da questão {result.QuestionNumber} não confirmou suficiência e coerência da alternativa.");
                }
            }

            if (result.Confidence is < 0 or > 1)
            {
                throw new InferenceValidationException(
                    $"A confiança da questão {result.QuestionNumber} deve estar entre 0 e 1.");
            }
        }

        return results
            .OrderBy(result => result.QuestionNumber)
            .ToArray();
    }

    private static void ValidateReasoningConsistency(InferenceResultDto result)
    {
        var conclusions = AlternativeConclusionRegex.Matches(result.Reasoning);

        if (conclusions.Count == 0)
        {
            return;
        }

        var concludedAlternative = conclusions[^1].Groups[1].Value;

        if (!string.Equals(
                concludedAlternative,
                result.SelectedAlternative,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InferenceValidationException(
                $"A questão {result.QuestionNumber} retornou selectedAlternative " +
                $"'{result.SelectedAlternative}', mas a conclusão do reasoning indica " +
                $"a alternativa '{concludedAlternative}'.");
        }
    }

    private sealed class InferenceEnvelope
    {
        public IReadOnlyList<InferenceResultDto>? Results { get; init; }
    }
}
