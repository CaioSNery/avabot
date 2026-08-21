namespace AvaBot.Domain.Models;

public sealed class ActivityNavigationOptions
{
    public const string SectionName = "ActivityNavigation";

    public ActivityExecutionMode Mode { get; set; } = ActivityExecutionMode.Completo;
}
