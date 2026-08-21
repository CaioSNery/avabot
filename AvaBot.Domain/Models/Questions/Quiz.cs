namespace AvaBot.Domain.Models.Questions;

public sealed record Quiz(
    string Title,
    IReadOnlyList<Question> Questions);
