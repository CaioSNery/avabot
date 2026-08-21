namespace AvaBot.Infrastructure.OpenAI;

public sealed class PromptRepository(PromptLoader loader)
{
    private const string QuestionAnswerPromptFile = "QuestionAnswerPrompt.txt";
    private const string QuestionAnswerReviewPromptFile = "QuestionAnswerReviewPrompt.txt";
    private const string MathematicsQuestionAnswerPromptFile =
        "MathematicsQuestionAnswerPrompt.txt";
    private const string MathematicsQuestionAnswerReviewPromptFile =
        "MathematicsQuestionAnswerReviewPrompt.txt";

    public Task<string> GetQuestionAnswerPromptAsync(
        bool visualMode,
        CancellationToken cancellationToken = default) =>
        loader.LoadAsync(
            visualMode
                ? MathematicsQuestionAnswerPromptFile
                : QuestionAnswerPromptFile,
            cancellationToken);

    public Task<string> GetQuestionAnswerReviewPromptAsync(
        bool visualMode,
        CancellationToken cancellationToken = default) =>
        loader.LoadAsync(
            visualMode
                ? MathematicsQuestionAnswerReviewPromptFile
                : QuestionAnswerReviewPromptFile,
            cancellationToken);
}
