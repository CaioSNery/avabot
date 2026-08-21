using AvaBot.Domain.Models.Questions;
using Microsoft.Playwright;

namespace AvaBot.Infrastructure.Extraction;

public interface IQuestionExtractor
{
    Task<ExtractedQuizData> ExtractAsync(
        IPage page,
        bool captureScreenshots,
        CancellationToken cancellationToken = default);
}
