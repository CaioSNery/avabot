namespace AvaBot.Shared;

public sealed record OperationResult(bool IsSuccess, string Message)
{
    public static OperationResult Success(string message) => new(true, message);

    public static OperationResult Failure(string message) => new(false, message);
}
