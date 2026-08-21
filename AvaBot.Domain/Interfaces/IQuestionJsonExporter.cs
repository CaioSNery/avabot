using AvaBot.Domain.Models.Questions;

namespace AvaBot.Domain.Interfaces;

public interface IQuestionJsonExporter
{
    string Serialize(Quiz quiz);

    Task ExportAsync(
        Quiz quiz,
        string outputPath = "questions.json",
        CancellationToken cancellationToken = default);
}
