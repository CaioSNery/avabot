namespace AvaBot.Domain.Models;

public sealed class ActivityNavigationResult
{
    public Activity? OpenedActivity { get; init; }

    public bool PreparedForQuestions { get; init; }

    public int NavigationCount { get; init; }
}
