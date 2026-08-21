namespace AvaBot.Domain.Models.Questions;

public sealed record Table(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);
