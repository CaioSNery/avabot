using AvaBot.Domain.Models;

namespace AvaBot.Tests;

public sealed class ActivityExecutionFilterTests
{
    [Fact]
    public void ShouldNavigate_ReturnsFalseForCompletedActivityInCompleteMode()
    {
        var filter = new ActivityExecutionFilter
        {
            Mode = ActivityExecutionMode.Completo
        };
        var activity = CreateActivity(ActivityCompletionStatus.Completed);

        Assert.False(filter.ShouldNavigate(activity));
    }

    [Fact]
    public void ShouldNavigate_ReturnsTrueForPendingActivityInCompleteMode()
    {
        var filter = new ActivityExecutionFilter
        {
            Mode = ActivityExecutionMode.Completo
        };
        var activity = CreateActivity(ActivityCompletionStatus.Pending);

        Assert.True(filter.ShouldNavigate(activity));
    }

    [Theory]
    [InlineData(ActivityType.VideoAulaResumo)]
    [InlineData(ActivityType.AvaliacaoUnidade)]
    [InlineData(ActivityType.ConteudoProfessor)]
    [InlineData(ActivityType.MaterialInformativo)]
    [InlineData(ActivityType.AulaPratica)]
    [InlineData(ActivityType.Questionario)]
    public void ShouldNavigate_ReturnsTrueForNewPendingActivityTypes(
        ActivityType activityType)
    {
        var filter = new ActivityExecutionFilter
        {
            Mode = ActivityExecutionMode.Completo
        };
        var activity = CreateActivity(
            ActivityCompletionStatus.Pending,
            activityType);

        Assert.True(filter.ShouldNavigate(activity));
    }

    [Fact]
    public void ShouldNavigate_UnitEvaluationIsEligibleInEvaluativeMode()
    {
        var filter = new ActivityExecutionFilter
        {
            Mode = ActivityExecutionMode.ApenasAvaliativas
        };
        var activity = CreateActivity(
            ActivityCompletionStatus.Pending,
            ActivityType.AvaliacaoUnidade);

        Assert.True(filter.ShouldNavigate(activity));
    }

    private static Activity CreateActivity(
        ActivityCompletionStatus status,
        ActivityType type = ActivityType.AtividadeDiagnostica) => new()
    {
        Nome = "Atividade Diagnóstica",
        Tipo = type,
        CompletionStatus = status,
        Locator = null!
    };
}
