using AvaBot.Application.Services;
using AvaBot.Domain.Models;

namespace AvaBot.Tests;

public sealed class ActivityTypeIdentificationTests
{
    [Theory]
    [InlineData("Infográfico")]
    [InlineData("Cronograma")]
    [InlineData("Programa de Nivelamento")]
    [InlineData("Conteúdo introdutório")]
    public void IdentifyType_TreatsGenericResourcesAsPassiveContent(string name)
    {
        var result = ActivityExplorerService.IdentifyType(
            name,
            "Sem categoria",
            "https://www.avaeduc.com.br/mod/resource/view.php?id=1");

        Assert.Equal(ActivityType.MaterialInformativo, result);
    }

    [Fact]
    public void IdentifyType_DoesNotOpenCertificate()
    {
        var result = ActivityExplorerService.IdentifyType(
            "Abrir Certificado Personalizado",
            "Sem categoria",
            "https://www.avaeduc.com.br/mod/customcert/view.php?id=1");

        Assert.Equal(ActivityType.Outro, result);
    }

    [Fact]
    public void IdentifyType_RecognizesGenericQuizByUrl()
    {
        var result = ActivityExplorerService.IdentifyType(
            "Avalie seus conhecimentos",
            "Sem categoria",
            "https://www.avaeduc.com.br/mod/quiz/view.php?id=1");

        Assert.Equal(ActivityType.Questionario, result);
    }

    [Theory]
    [InlineData(ActivityType.AtividadeDiagnostica)]
    [InlineData(ActivityType.AtividadeAprendizagem)]
    [InlineData(ActivityType.AvaliacaoUnidade)]
    [InlineData(ActivityType.Questionario)]
    public void EvaluativeActivities_RequireLiveCompletionConfirmation(
        ActivityType activityType)
    {
        Assert.True(ActivityExecutionFilter.IsEvaluative(activityType));
    }

    [Theory]
    [InlineData(ActivityType.WebAula)]
    [InlineData(ActivityType.MaterialInformativo)]
    [InlineData(ActivityType.ConteudoProfessor)]
    [InlineData(ActivityType.AulaPratica)]
    public void PassiveActivities_CanKeepUsingLocalVisitHistory(
        ActivityType activityType)
    {
        Assert.False(ActivityExecutionFilter.IsEvaluative(activityType));
    }

    [Fact]
    public void LocalHistory_DoesNotOverrideExplicitPendingStateFromAva()
    {
        Assert.False(ActivityExplorerService.CanUseLocalCompletion(
            ActivityType.MaterialInformativo,
            ActivityCompletionStatus.Pending));
    }

    [Fact]
    public void LocalHistory_CanCompletePassiveResourceWithoutAvaIndicator()
    {
        Assert.True(ActivityExplorerService.CanUseLocalCompletion(
            ActivityType.MaterialInformativo,
            ActivityCompletionStatus.Unknown));
    }

    [Fact]
    public void LocalHistory_DoesNotOverridePendingStateForEvaluativeActivity()
    {
        Assert.False(ActivityExplorerService.CanUseLocalCompletion(
            ActivityType.Questionario,
            ActivityCompletionStatus.Pending));
    }

    [Theory]
    [InlineData(ActivityType.AtividadeDiagnostica)]
    [InlineData(ActivityType.AtividadeAprendizagem)]
    [InlineData(ActivityType.AvaliacaoUnidade)]
    [InlineData(ActivityType.Questionario)]
    public void PassiveSection_DoesNotDowngradeEvaluativeActivity(
        ActivityType activityType)
    {
        var result = ActivityExplorerService.ApplySectionMode(
            activityType,
            passiveOnly: true);

        Assert.Equal(activityType, result);
    }

    [Fact]
    public void PassiveSection_StillTreatsOrdinaryContentAsPassive()
    {
        var result = ActivityExplorerService.ApplySectionMode(
            ActivityType.WebAula,
            passiveOnly: true);

        Assert.Equal(ActivityType.MaterialInformativo, result);
    }
}
