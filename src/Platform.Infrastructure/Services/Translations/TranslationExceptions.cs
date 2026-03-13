namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationRequestValidationException(string message, string errorCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class TranslationProviderException(string message, string errorCode, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}
