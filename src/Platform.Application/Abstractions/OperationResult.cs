namespace Platform.Application.Abstractions;

public sealed record OperationResult(bool Succeeded, string Message, string? ErrorCode = null)
{
    public static OperationResult Success(string message = "Операция выполнена.") => new(true, message);

    public static OperationResult Failure(string message, string errorCode) => new(false, message, errorCode);
}

public sealed record OperationResult<T>(bool Succeeded, string Message, T? Data, string? ErrorCode = null)
{
    public static OperationResult<T> Success(T data, string message = "Операция выполнена.") => new(true, message, data);

    public static OperationResult<T> Failure(string message, string errorCode) => new(false, message, default, errorCode);
}
