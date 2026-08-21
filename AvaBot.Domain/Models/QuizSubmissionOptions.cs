namespace AvaBot.Domain.Models;

public sealed class QuizSubmissionOptions
{
    public const string SectionName = "QuizSubmission";

    public bool Enabled { get; init; }

    public int ManualCompletionTimeout { get; init; } = 1_800_000;
}
