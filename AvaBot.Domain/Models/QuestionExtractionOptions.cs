namespace AvaBot.Domain.Models;

public sealed class QuestionExtractionOptions
{
    public const string SectionName = "QuestionExtraction";

    public string QuizTitleSelector { get; init; } =
        "[data-testid='quiz-title'], .quiz-title, main h1, div[role='main'] h2";

    public string QuestionSelector { get; init; } =
        ".que, [data-testid='question'], [data-question-id], .question";

    public string StatementSelector { get; init; } =
        "[data-testid='question-statement'], .qtext, .question-text, .statement";

    public string QuestionNumberSelector { get; init; } =
        "[data-testid='question-number'], .qno, .info .no, .question-number";

    public string AlternativeSelector { get; init; } =
        "[data-testid='alternative'], .answer label, .alternative, .option";

    public int ManualStartTimeout { get; init; } = 300000;

    public string QuestionNavigationSelector { get; init; } =
        "a.qnbutton[href], [data-testid='question-navigation'] a[href]";
}
