using System.Diagnostics;
using System.Text.RegularExpressions;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed partial class QuestionParserService(
    ILogger<QuestionParserService> logger) : IQuestionParser
{
    public Quiz Parse(ExtractedQuizData extractedQuiz)
    {
        ArgumentNullException.ThrowIfNull(extractedQuiz);
        var stopwatch = Stopwatch.StartNew();

        var questions = extractedQuiz.Questions
            .Select((question, index) => ParseQuestion(question, index + 1))
            .ToArray();

        stopwatch.Stop();
        logger.LogInformation(
            "Quiz modelado em {ElapsedMilliseconds} ms; questões: {QuestionCount}; alternativas: {AlternativeCount}",
            stopwatch.ElapsedMilliseconds,
            questions.Length,
            questions.Sum(question => question.Alternatives.Count));

        return new Quiz(Normalize(extractedQuiz.Title), questions);
    }

    private static Question ParseQuestion(
        ExtractedQuestionData extractedQuestion,
        int fallbackNumber)
    {
        var alternatives = extractedQuestion.Alternatives
            .Select((alternative, index) => new Alternative(
                string.IsNullOrWhiteSpace(alternative.Id)
                    ? ToAlphabeticId(index)
                    : Normalize(alternative.Id),
                Normalize(alternative.Text)))
            .ToArray();

        return new Question(
            ParseNumber(extractedQuestion.NumberText, fallbackNumber),
            Normalize(extractedQuestion.Statement),
            alternatives,
            extractedQuestion.Images
                .Select(image => new Image(
                    Normalize(image.Description),
                    image.Source.Trim()))
                .ToArray(),
            extractedQuestion.Tables
                .Select(table => new Table(
                    table.Headers.Select(Normalize).ToArray(),
                    table.Rows
                        .Select(row => (IReadOnlyList<string>)row.Select(Normalize).ToArray())
                        .ToArray()))
                .ToArray(),
            extractedQuestion.CodeBlocks
                .Select(code => new CodeBlock(
                    Normalize(code.Language),
                    code.Content.Trim()))
                .ToArray(),
            (extractedQuestion.Formulas ?? [])
                .Select(Normalize)
                .Where(formula => !string.IsNullOrWhiteSpace(formula))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            extractedQuestion.ScreenshotDataUrl);
    }

    private static int ParseNumber(string value, int fallback)
    {
        var match = NumberRegex().Match(value ?? string.Empty);
        return match.Success && int.TryParse(match.Value, out var number) && number > 0
            ? number
            : fallback;
    }

    private static string ToAlphabeticId(int index) =>
        index < 26 ? ((char)('A' + index)).ToString() : (index + 1).ToString();

    private static string Normalize(string value) =>
        WhitespaceRegex().Replace(value ?? string.Empty, " ").Trim();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
