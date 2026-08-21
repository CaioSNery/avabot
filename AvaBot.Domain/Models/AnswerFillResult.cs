namespace AvaBot.Domain.Models;

public sealed class AnswerFillResult
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public static AnswerFillResult Ok(string message) =>
        new() { Success = true, Message = message };

    public static AnswerFillResult Fail(string message) =>
        new() { Success = false, Message = message };
}
