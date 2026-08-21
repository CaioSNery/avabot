namespace AvaBot.Domain.Models.Questions;

public sealed record ExtractedQuizData(
    string Title,
    IReadOnlyList<ExtractedQuestionData> Questions);

public sealed record ExtractedQuestionData(
    string NumberText,
    string Statement,
    IReadOnlyList<ExtractedAlternativeData> Alternatives,
    IReadOnlyList<ExtractedImageData> Images,
    IReadOnlyList<ExtractedTableData> Tables,
    IReadOnlyList<ExtractedCodeBlockData> CodeBlocks,
    IReadOnlyList<string>? Formulas = null,
    string ScreenshotDataUrl = "");

public sealed record ExtractedAlternativeData(string Id, string Text);

public sealed record ExtractedImageData(string Description, string Source);

public sealed record ExtractedTableData(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record ExtractedCodeBlockData(string Language, string Content);
