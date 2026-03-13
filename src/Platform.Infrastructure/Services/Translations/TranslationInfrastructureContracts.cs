using Platform.Application.Abstractions;
using Platform.Application.Services;

namespace Platform.Infrastructure.Services.Translations;

public interface ITranslationQueueService
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken);

    Task<Guid?> TryDequeueAsync(CancellationToken cancellationToken);

    Task<long> GetQueuedCountAsync(CancellationToken cancellationToken);
}

public interface ITranslationPathSanitizer
{
    string SanitizeRelativePath(string path);

    string GetSafeStoragePath(string rootPath, string relativePath);
}

public sealed record ProtectedTextResult(
    string Text,
    IReadOnlyDictionary<string, string> PlaceholderMap);

public interface ITranslationPlaceholderProtector
{
    ProtectedTextResult Protect(string text, EffectiveGlossary glossary);

    OperationResult<string> Restore(string translatedText, IReadOnlyDictionary<string, string> placeholderMap);
}

public interface ITranslationJobProcessor
{
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);

    Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken);
}
