namespace AvaBot.Domain.Models;

public sealed class ActivityExecutionFilter
{
    public required ActivityExecutionMode Mode { get; init; }

    public bool ShouldNavigate(Activity activity)
    {
        if (activity.CompletionStatus == ActivityCompletionStatus.Completed)
        {
            return false;
        }

        return Mode switch
        {
            ActivityExecutionMode.Completo => IsSupported(activity.Tipo),
            ActivityExecutionMode.ApenasAvaliativas => IsEvaluative(activity.Tipo),
            ActivityExecutionMode.Teste => IsSupported(activity.Tipo),
            _ => false
        };
    }

    public bool StopAfterFirstNavigation => Mode == ActivityExecutionMode.Teste;

    private static bool IsSupported(ActivityType type) =>
        type is ActivityType.WebAula or
            ActivityType.VideoAulaResumo or
            ActivityType.ConteudoProfessor or
            ActivityType.MaterialInformativo or
            ActivityType.AtividadeDiagnostica or
            ActivityType.AtividadeAprendizagem or
            ActivityType.AvaliacaoUnidade or
            ActivityType.Questionario or
            ActivityType.AulaPratica;

    public static bool IsEvaluative(ActivityType type) =>
        type is ActivityType.AtividadeDiagnostica or
            ActivityType.AtividadeAprendizagem or
            ActivityType.AvaliacaoUnidade or
            ActivityType.Questionario;
}
