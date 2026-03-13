using Platform.Domain.Common;

namespace Platform.Application.Models;

public sealed record PagedResultDto<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TranslationJobListQuery(
    int Page = 1,
    int PageSize = 20,
    TranslationJobState? State = null,
    string? Search = null);

public sealed record TranslationJobAdminListItemDto(
    Guid JobId,
    string Status,
    string ProviderCode,
    string ModName,
    string SourceLanguage,
    string TargetLanguage,
    string LicenseMaskedKey,
    string DeviceName,
    int TotalFiles,
    int TotalSegments,
    int TotalCharacters,
    int RetryCount,
    string FailureReason,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? CompletedUtc);

public sealed record TranslationUsageAdminDto(
    Guid LicenseId,
    string LicenseMaskedKey,
    string CustomerName,
    DateOnly UsageDate,
    int ReservedCharacters,
    int ConsumedCharacters,
    int JobsCreated,
    int JobsCompleted,
    int JobsFailed,
    int JobsCancelled,
    int AnalysisRequests);

public sealed record TranslationQuotaAdminDto(
    Guid LicenseId,
    string LicenseMaskedKey,
    int MaxFilesPerJob,
    int MaxSegmentsPerJob,
    int MaxCharactersPerJob,
    int MaxCharactersPerDay,
    int MaxConcurrentJobs,
    int MaxJobsPerHour,
    int MaxAnalysisPerHour,
    bool IsEnabled,
    string Notes);

public sealed record UpsertTranslationQuotaRequest(
    Guid LicenseId,
    int MaxFilesPerJob,
    int MaxSegmentsPerJob,
    int MaxCharactersPerJob,
    int MaxCharactersPerDay,
    int MaxConcurrentJobs,
    int MaxJobsPerHour,
    int MaxAnalysisPerHour,
    bool IsEnabled,
    string Notes);

public sealed record TranslationGlossaryAdminDto(
    Guid Id,
    Guid? LicenseId,
    string Name,
    TranslationGlossaryScope Scope,
    string SourceLanguage,
    string TargetLanguage,
    int Priority,
    bool IsActive,
    int TermsCount,
    int FrozenTermsCount,
    int SkipTermsCount,
    string Description);

public sealed record UpsertTranslationGlossaryRequest(
    Guid? Id,
    Guid? LicenseId,
    string Name,
    TranslationGlossaryScope Scope,
    string SourceLanguage,
    string TargetLanguage,
    int Priority,
    bool IsActive,
    string Description,
    IReadOnlyCollection<TranslationGlossaryTermDto> Terms,
    IReadOnlyCollection<string> FrozenTerms,
    IReadOnlyCollection<string> SkipTerms);

public sealed record TranslationGlossaryTermDto(string Source, string Target);

public sealed record TranslationProviderAdminDto(
    Guid Id,
    string ProviderCode,
    string DisplayName,
    bool IsEnabled,
    string Endpoint,
    string LanguagesEndpoint,
    string FolderId,
    string SecretReference,
    string LastKnownStatus,
    string LastError,
    DateTimeOffset? LastHealthCheckUtc,
    int TimeoutSeconds,
    int MaxBatchCharacters);

public sealed record SetTranslationProviderStateRequest(
    Guid ProviderId,
    bool IsEnabled);

public sealed record TranslationQueueStatusDto(
    string QueueName,
    int QueuedJobs,
    int ProcessingJobs,
    int FailedJobs,
    int CancelRequestedJobs,
    DateTimeOffset GeneratedUtc);
