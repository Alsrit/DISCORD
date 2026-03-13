using Platform.Domain.Common;

namespace Platform.Application.Models;

public sealed record UploadedLocalizationFileDto(
    string RelativePath,
    string Content,
    string SourceLanguage,
    string Sha256,
    long SizeBytes);

public sealed record AnalyzeModRequest(
    string ModName,
    string ModVersion,
    string OriginalModReference,
    string SourceLanguage,
    IReadOnlyCollection<UploadedLocalizationFileDto> Files);

public sealed record FileWarningDto(string Code, string Message);

public sealed record ModFileAnalysisDto(
    string RelativePath,
    string SanitizedPath,
    string HeaderKey,
    string SourceLanguage,
    string Sha256,
    int SegmentCount,
    int CharacterCount,
    bool Supported,
    IReadOnlyCollection<FileWarningDto> Warnings);

public sealed record AnalyzeModResponse(
    Guid SnapshotId,
    string ModName,
    string SourceLanguage,
    int FileCount,
    int SegmentCount,
    int CharacterCount,
    IReadOnlyCollection<ModFileAnalysisDto> Files,
    IReadOnlyCollection<FileWarningDto> Warnings,
    DateTimeOffset ExpiresUtc);

public sealed record CreateTranslationJobRequest(
    Guid? AnalysisSnapshotId,
    string ModName,
    string OriginalModReference,
    string SourceLanguage,
    string TargetLanguage,
    string RequestedSubmodName,
    string ProviderCode,
    IReadOnlyCollection<UploadedLocalizationFileDto> Files);

public sealed record TranslationQuotaStatusDto(
    int MaxFilesPerJob,
    int MaxSegmentsPerJob,
    int MaxCharactersPerJob,
    int MaxCharactersPerDay,
    int RemainingCharactersToday,
    int MaxConcurrentJobs,
    int ActiveJobs,
    int MaxJobsPerHour,
    int RemainingJobsThisHour,
    int MaxAnalysisPerHour,
    int RemainingAnalysisThisHour);

public sealed record TranslationJobCreatedResponse(
    Guid JobId,
    string Status,
    DateTimeOffset RequestedUtc,
    TranslationQuotaStatusDto Quota,
    string Message);

public sealed record TranslationFileResultDto(
    Guid FileId,
    string RelativePath,
    string SanitizedPath,
    string HeaderKey,
    string Status,
    int SegmentCount,
    int CharacterCount,
    IReadOnlyCollection<FileWarningDto> Warnings);

public sealed record SubmodManifestPreviewDto(
    string SubmodName,
    string DescriptorName,
    string TargetLanguage,
    IReadOnlyCollection<string> OutputFiles,
    IReadOnlyCollection<string> Notes);

public sealed record TranslationJobStatusDto(
    Guid JobId,
    string Status,
    string ProviderCode,
    string ModName,
    string SourceLanguage,
    string TargetLanguage,
    string RequestedSubmodName,
    int TotalFiles,
    int TotalSegments,
    int TotalCharacters,
    int ProcessedSegments,
    int ProcessedCharacters,
    int RetryCount,
    bool DownloadAvailable,
    string FailureCode,
    string FailureReason,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    DateTimeOffset? CancelRequestedUtc,
    SubmodManifestPreviewDto? ManifestPreview);

public sealed record TranslationDownloadInfoDto(
    Guid JobId,
    Guid ArtifactId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedUtc);

public sealed record LanguageOptionDto(
    string Code,
    string DisplayName,
    bool SupportedAsSource,
    bool SupportedAsTarget);

public sealed record ActiveGlossaryDto(
    Guid Id,
    string Name,
    string Scope,
    string SourceLanguage,
    string TargetLanguage,
    int TermsCount,
    int FrozenTermsCount,
    int SkipTermsCount,
    string Description);
