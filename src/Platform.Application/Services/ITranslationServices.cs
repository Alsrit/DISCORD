using System.IO;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Domain.Common;
using Platform.Domain.Translations;

namespace Platform.Application.Services;

public interface ITranslationJobService
{
    Task<OperationResult<AnalyzeModResponse>> AnalyzeAsync(
        Guid sessionId,
        AnalyzeModRequest request,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<TranslationJobCreatedResponse>> CreateJobAsync(
        Guid sessionId,
        CreateTranslationJobRequest request,
        string? idempotencyKey,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<TranslationJobStatusDto>> GetJobAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyCollection<TranslationFileResultDto>>> GetJobFilesAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<TranslationDownloadInfoDto>> GetDownloadInfoAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult<(Stream Stream, string FileName, string ContentType)>> OpenDownloadAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<OperationResult> CancelJobAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LanguageOptionDto>> GetLanguagesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ActiveGlossaryDto>> GetActiveGlossariesAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<OperationResult<TranslationQuotaStatusDto>> GetCurrentQuotaAsync(
        Guid sessionId,
        RequestContext context,
        CancellationToken cancellationToken);
}

public interface ITranslationProvider
{
    string ProviderCode { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LanguageOptionDto>> GetLanguagesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> TranslateAsync(
        string sourceLanguage,
        string targetLanguage,
        IReadOnlyCollection<string> texts,
        CancellationToken cancellationToken);
}

public interface IGlossaryService
{
    Task<EffectiveGlossary> GetEffectiveGlossaryAsync(
        Guid licenseId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ActiveGlossaryDto>> GetActiveGlossariesAsync(
        Guid licenseId,
        CancellationToken cancellationToken);
}

public interface IQuotaService
{
    Task<OperationResult<TranslationQuotaStatusDto>> GetCurrentQuotaAsync(
        Guid licenseId,
        Guid deviceId,
        CancellationToken cancellationToken);

    Task<OperationResult<TranslationQuotaStatusDto>> ReserveAsync(
        Guid licenseId,
        Guid deviceId,
        int characterCount,
        int fileCount,
        int segmentCount,
        CancellationToken cancellationToken);

    Task ReleaseReservationAsync(
        Guid licenseId,
        Guid deviceId,
        int characterCount,
        CancellationToken cancellationToken);

    Task CommitUsageAsync(
        Guid licenseId,
        Guid deviceId,
        int reservedCharacterCount,
        int consumedCharacterCount,
        TranslationJobState terminalState,
        CancellationToken cancellationToken);
}

public interface IModAnalysisService
{
    Task<ModAnalysisResult> AnalyzeAsync(
        AnalyzeModRequest request,
        CancellationToken cancellationToken);

    Task<ParsedLocalizationFile> ParseFileAsync(
        UploadedLocalizationFileDto file,
        CancellationToken cancellationToken);
}

public interface ITranslationPackagingService
{
    Task<PackagedTranslationResult> PackageAsync(
        TranslationJob job,
        IReadOnlyCollection<TranslationFile> files,
        CancellationToken cancellationToken);
}

public interface ITranslationResultService
{
    Task<IReadOnlyCollection<TranslationFileResultDto>> GetFilesAsync(
        TranslationJob job,
        CancellationToken cancellationToken);

    Task<TranslationDownloadInfoDto?> GetDownloadInfoAsync(
        TranslationJob job,
        CancellationToken cancellationToken);

    Task<(Stream Stream, string FileName, string ContentType)?> OpenDownloadAsync(
        TranslationJob job,
        CancellationToken cancellationToken);
}

public interface ISubmodManifestService
{
    SubmodManifestPreviewDto BuildPreview(
        TranslationJob job,
        IReadOnlyCollection<TranslationFile> files);
}

public sealed record ParsedLocalizationEntry(
    int LineNumber,
    string LocalizationKey,
    string Prefix,
    string SourceText,
    string Suffix);

public sealed record ParsedLocalizationFile(
    string RelativePath,
    string SanitizedPath,
    string HeaderKey,
    string SourceLanguage,
    string OriginalSha256,
    long OriginalSizeBytes,
    string OriginalContent,
    IReadOnlyCollection<FileWarningDto> Warnings,
    IReadOnlyCollection<ParsedLocalizationEntry> Entries);

public sealed record ModAnalysisResult(
    string PayloadSha256,
    int FileCount,
    int SegmentCount,
    int CharacterCount,
    IReadOnlyCollection<FileWarningDto> Warnings,
    IReadOnlyCollection<ParsedLocalizationFile> Files);

public sealed record EffectiveGlossary(
    IReadOnlyDictionary<string, string> Terms,
    IReadOnlyCollection<string> FrozenTerms,
    IReadOnlyCollection<string> SkipTerms);

public sealed record PackagedTranslationResult(
    string FileName,
    string StoragePath,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string ManifestJson);
