namespace AvaBot.Domain.Models;

public sealed class ManualQuizNavigationOptions
{
    public const string SectionName = "ManualQuizNavigation";

    public bool Enabled { get; init; } = true;

    public int AnswerTimeout { get; init; }

    public int MaxPages { get; init; } = 50;
}
