namespace AvaBot.Domain.Exceptions;

public sealed class InferenceValidationException : Exception
{
    public InferenceValidationException(string message)
        : base(message)
    {
    }

    public InferenceValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
