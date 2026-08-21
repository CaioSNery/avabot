using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Models;

namespace AvaBot.Application.Services;

public sealed class QuestionBatchBuilder
{
    public InferenceRequestDto Build(InferenceRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Questions);

        if (request.Questions.Count == 0)
        {
            throw new InferenceValidationException(
                "O lote de inferência deve conter ao menos uma questão.");
        }

        var result = new List<QuestionRequestDto>(request.Questions.Count);

        foreach (var group in request.Questions.GroupBy(question => question.QuestionNumber))
        {
            var first = group.First();
            ValidateQuestion(first);

            if (group.Skip(1).Any(question => !AreEquivalent(first, question)))
            {
                throw new InferenceValidationException(
                    $"A questão {group.Key} foi informada mais de uma vez com conteúdos diferentes.");
            }

            result.Add(first);
        }

        return new InferenceRequestDto(
            result
                .OrderBy(question => question.QuestionNumber)
                .ToArray())
        {
            VisualMode = request.VisualMode,
            ResponseToReview = request.ResponseToReview
        };
    }

    private static void ValidateQuestion(QuestionRequestDto question)
    {
        if (question.QuestionNumber <= 0)
        {
            throw new InferenceValidationException(
                "O número da questão deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(question.Statement))
        {
            throw new InferenceValidationException(
                $"A questão {question.QuestionNumber} não possui enunciado.");
        }

        if (question.Alternatives.Count < 2)
        {
            throw new InferenceValidationException(
                $"A questão {question.QuestionNumber} deve possuir ao menos duas alternativas.");
        }

        if (question.Alternatives.Any(alternative =>
                string.IsNullOrWhiteSpace(alternative.Id) ||
                string.IsNullOrWhiteSpace(alternative.Text)))
        {
            throw new InferenceValidationException(
                $"A questão {question.QuestionNumber} possui alternativa sem identificador ou texto.");
        }

        if (question.Alternatives
            .Select(alternative => alternative.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != question.Alternatives.Count)
        {
            throw new InferenceValidationException(
                $"A questão {question.QuestionNumber} possui identificadores de alternativas repetidos.");
        }
    }

    private static bool AreEquivalent(
        QuestionRequestDto left,
        QuestionRequestDto right)
    {
        if (!string.Equals(left.Statement, right.Statement, StringComparison.Ordinal) ||
            left.Alternatives.Count != right.Alternatives.Count ||
            !left.Formulas.SequenceEqual(right.Formulas, StringComparer.Ordinal) ||
            left.Tables.Count != right.Tables.Count)
        {
            return false;
        }

        var alternativesEquivalent = left.Alternatives
            .Zip(right.Alternatives)
            .All(pair =>
                string.Equals(pair.First.Id, pair.Second.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(pair.First.Text, pair.Second.Text, StringComparison.Ordinal));

        var tablesEquivalent = left.Tables.Zip(right.Tables).All(pair =>
            pair.First.Headers.SequenceEqual(pair.Second.Headers, StringComparer.Ordinal) &&
            pair.First.Rows.Count == pair.Second.Rows.Count &&
            pair.First.Rows.Zip(pair.Second.Rows).All(rows =>
                rows.First.SequenceEqual(rows.Second, StringComparer.Ordinal)));

        return alternativesEquivalent && tablesEquivalent;
    }
}
