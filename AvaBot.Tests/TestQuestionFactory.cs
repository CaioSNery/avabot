using AvaBot.Domain.Models;

namespace AvaBot.Tests;

internal static class TestQuestionFactory
{
    public static QuestionRequestDto Create(
        int number,
        string statement = "Enunciado") => new()
    {
        QuestionNumber = number,
        Statement = statement,
        Alternatives =
        [
            new AlternativeRequestDto("A", "Alternativa A"),
            new AlternativeRequestDto("B", "Alternativa B")
        ]
    };
}
