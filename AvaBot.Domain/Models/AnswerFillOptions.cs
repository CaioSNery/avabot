namespace AvaBot.Domain.Models;

public sealed class AnswerFillOptions
{
    public const string SectionName = "AnswerFill";

    public string QuestionSelector { get; init; } =
        ".que, [data-testid='question'], [data-question-id], .question";

    public string QuestionNumberSelector { get; init; } =
        "[data-testid='question-number'], .qno, .info .no, .question-number";

    public string AlternativeSelector { get; init; } =
        "[data-testid='alternative'], .answer label, .alternative, .option";

    public string QuestionNavigationSelector { get; init; } =
        "a.qnbutton[href], [data-testid='question-navigation'] a[href]";

    public string NextPageSelector { get; init; } =
        "input.mod_quiz-next-nav[type='submit'], " +
        "button.mod_quiz-next-nav, " +
        "input[name='next'][type='submit'], " +
        "button[name='next'], " +
        "input[type='submit'][value*='Próxima' i], " +
        "button:has-text('Próxima página')";

    public int ClickTimeout { get; init; } = 10000;

    public int ManualAnswerTimeout { get; init; } = 30000;

    public double MinimumConfidence { get; init; } = 0.55;

    public bool DryRun { get; init; }
}
