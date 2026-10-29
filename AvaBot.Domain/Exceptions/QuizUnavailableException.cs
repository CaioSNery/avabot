namespace AvaBot.Domain.Exceptions;

public sealed class QuizUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
