namespace AvaBot.Domain.Models.Questions;

public sealed record Question(
    int Number,
    string Statement,
    IReadOnlyList<Alternative> Alternatives,
    IReadOnlyList<Image> Images,
    IReadOnlyList<Table> Tables,
    IReadOnlyList<CodeBlock> CodeBlocks,
    IReadOnlyList<string>? Formulas = null,
    string ScreenshotDataUrl = "");
