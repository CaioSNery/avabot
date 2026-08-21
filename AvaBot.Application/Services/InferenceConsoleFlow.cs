using System.Text.Json;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class InferenceConsoleFlow(
    IInferenceEngine inferenceEngine,
    IOptions<OpenAiOptions> options,
    ILogger<InferenceConsoleFlow> logger) : IInferenceConsoleFlow
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private readonly OpenAiOptions _options = options.Value;

    public async Task<IReadOnlyList<InferenceResultDto>> ExecuteFromFileAsync(
        string questionsJsonPath,
        bool visualMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionsJsonPath);

        if (!_options.EnableConsoleInference)
        {
            logger.LogInformation(
                "Inferência didática desabilitada em OpenAI:EnableConsoleInference");
            return [];
        }

        var fullPath = Path.GetFullPath(questionsJsonPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "O arquivo de questões não foi encontrado para a demonstração de inferência.",
                fullPath);
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        var quiz = await JsonSerializer.DeserializeAsync<Quiz>(
                stream,
                SerializerOptions,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "questions.json não contém um quiz válido.");

        var request = CreateRequest(quiz, visualMode);

        Console.WriteLine();
        Console.WriteLine("===== JSON ENVIADO À RESPONSES API =====");
        Console.WriteLine(JsonSerializer.Serialize(request, SerializerOptions));
        Console.WriteLine("===== FIM DO JSON ENVIADO =====");
        Console.WriteLine();

        logger.LogInformation(
            "Enviando questões para inferência; modelo primário: {PrimaryModel}; fallback: {FallbackModel}; modo visual: {VisualMode}; questões: {QuestionCount}; alternativas: {AlternativeCount}; fórmulas: {FormulaCount}; tabelas: {TableCount}; screenshots: {ScreenshotCount}; bytes visuais: {ScreenshotBytes}",
            _options.PrimaryModel,
            _options.FallbackModel,
            visualMode ? "SIM" : "NÃO",
            request.Questions.Count,
            request.Questions.Sum(question => question.Alternatives.Count),
            request.Questions.Sum(question => question.Formulas.Count),
            request.Questions.Sum(question => question.Tables.Count),
            request.Questions.Count(question => question.ScreenshotPresent),
            request.Questions.Sum(question => question.ScreenshotBytes));

        var results = await inferenceEngine.ExecuteAsync(request, cancellationToken);
        var response = new InferenceResponse(results);

        Console.WriteLine();
        Console.WriteLine("===== JSON RECEBIDO DA RESPONSES API =====");
        Console.WriteLine(JsonSerializer.Serialize(response, SerializerOptions));
        Console.WriteLine("===== FIM DO JSON RECEBIDO =====");
        Console.WriteLine();

        logger.LogInformation(
            "Resposta didática exibida no console; resultados: {ResultCount}; respondidas: {AnsweredCount}; dados insuficientes: {InsufficientDataCount}; revisão manual: {ManualReviewCount}; avisos: {WarningCount}. Nenhuma ação foi enviada ao Playwright",
            results.Count,
            results.Count(result => string.Equals(
                result.Status,
                InferenceStatuses.Answered,
                StringComparison.Ordinal)),
            results.Count(result => string.Equals(
                result.Status,
                InferenceStatuses.InsufficientData,
                StringComparison.Ordinal)),
            results.Count(result => string.Equals(
                result.Status,
                InferenceStatuses.ManualReviewRequired,
                StringComparison.Ordinal)),
            results.Count(result => !string.IsNullOrWhiteSpace(result.Warning)));

        return results;
    }

    private static InferenceRequestDto CreateRequest(Quiz quiz, bool visualMode) =>
        new(
            quiz.Questions
                .Select(question => new QuestionRequestDto
                {
                    QuestionNumber = question.Number,
                    Statement = question.Statement,
                    Alternatives = question.Alternatives
                        .Select(alternative => new AlternativeRequestDto(
                            alternative.Id,
                            alternative.Text))
                        .ToArray(),
                    Formulas = question.Formulas ?? [],
                    Tables = question.Tables
                        .Select(table => new TableRequestDto(
                            table.Headers,
                            table.Rows))
                        .ToArray(),
                    VisualDescriptions = question.Images
                        .Select(image => image.Description)
                        .Where(description => !string.IsNullOrWhiteSpace(description))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    ScreenshotDataUrl = question.ScreenshotDataUrl
                })
                .ToArray())
        {
            VisualMode = visualMode
        };

    private sealed record InferenceResponse(
        IReadOnlyList<InferenceResultDto> Results);
}
