using System.Diagnostics;
using System.Text.Json;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models.Questions;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class QuestionJsonExporter(
    ILogger<QuestionJsonExporter> logger) : IQuestionJsonExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Serialize(Quiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        return JsonSerializer.Serialize(quiz, SerializerOptions);
    }

    public async Task ExportAsync(
        Quiz quiz,
        string outputPath = "questions.json",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quiz);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var stopwatch = Stopwatch.StartNew();
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(Serialize(quiz).AsMemory(), cancellationToken);

        stopwatch.Stop();
        logger.LogInformation(
            "JSON exportado para {OutputPath} em {ElapsedMilliseconds} ms; questões: {QuestionCount}; alternativas: {AlternativeCount}",
            fullPath,
            stopwatch.ElapsedMilliseconds,
            quiz.Questions.Count,
            quiz.Questions.Sum(question => question.Alternatives.Count));
    }
}
