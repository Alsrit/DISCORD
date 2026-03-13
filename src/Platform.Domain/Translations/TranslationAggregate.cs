using Platform.Domain.Common;
using Platform.Domain.Licensing;

namespace Platform.Domain.Translations;

public sealed class TranslationJob : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public Guid? SessionId { get; set; }

    public Guid? AnalysisSnapshotId { get; set; }

    public ModAnalysisSnapshot? AnalysisSnapshot { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string QueueName { get; set; } = "translations";

    public string ProviderCode { get; set; } = "yandex";

    public string ModName { get; set; } = string.Empty;

    public string OriginalModReference { get; set; } = string.Empty;

    public string RequestedSubmodName { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = "en";

    public string TargetLanguage { get; set; } = "ru";

    public TranslationJobState State { get; set; } = TranslationJobState.Pending;

    public int TotalFiles { get; set; }

    public int TotalSegments { get; set; }

    public int TotalCharacters { get; set; }

    public int ProcessedSegments { get; set; }

    public int ProcessedCharacters { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    public int Priority { get; set; } = 100;

    public DateTimeOffset RequestedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    public DateTimeOffset? CancelRequestedUtc { get; set; }

    public DateTimeOffset? CancelledUtc { get; set; }

    public DateTimeOffset? LastHeartbeatUtc { get; set; }

    public DateTimeOffset? LeaseExpiresUtc { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureReason { get; set; } = string.Empty;

    public string ResultStoragePath { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public ICollection<TranslationFile> Files { get; set; } = new List<TranslationFile>();

    public ICollection<TranslationJobItem> Items { get; set; } = new List<TranslationJobItem>();

    public ICollection<TranslationAuditEvent> AuditEvents { get; set; } = new List<TranslationAuditEvent>();

    public ICollection<SubmodBuildArtifact> Artifacts { get; set; } = new List<SubmodBuildArtifact>();
}

public sealed class TranslationFile : EntityBase
{
    public Guid TranslationJobId { get; set; }

    public TranslationJob TranslationJob { get; set; } = null!;

    public string RelativePath { get; set; } = string.Empty;

    public string SanitizedPath { get; set; } = string.Empty;

    public string HeaderKey { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = "en";

    public string TargetLanguage { get; set; } = "ru";

    public string OriginalSha256 { get; set; } = string.Empty;

    public long OriginalSizeBytes { get; set; }

    public int SegmentCount { get; set; }

    public int CharacterCount { get; set; }

    public TranslationFileState State { get; set; } = TranslationFileState.Pending;

    public string WarningJson { get; set; } = "[]";

    public string OriginalContent { get; set; } = string.Empty;

    public string TranslatedContent { get; set; } = string.Empty;

    public ICollection<TranslationSegment> Segments { get; set; } = new List<TranslationSegment>();
}

public sealed class TranslationSegment : EntityBase
{
    public Guid TranslationFileId { get; set; }

    public TranslationFile TranslationFile { get; set; } = null!;

    public int Sequence { get; set; }

    public int LineNumber { get; set; }

    public string LocalizationKey { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public string Suffix { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;

    public string ProtectedSourceText { get; set; } = string.Empty;

    public string ProtectedTranslationText { get; set; } = string.Empty;

    public string FinalText { get; set; } = string.Empty;

    public string PlaceholderMapJson { get; set; } = "{}";

    public int CharacterCount { get; set; }

    public TranslationSegmentState State { get; set; } = TranslationSegmentState.Pending;

    public string ValidationMessage { get; set; } = string.Empty;
}

public sealed class TranslationJobItem : EntityBase
{
    public Guid TranslationJobId { get; set; }

    public TranslationJob TranslationJob { get; set; } = null!;

    public int BatchNumber { get; set; }

    public string ProviderCode { get; set; } = "yandex";

    public TranslationJobItemState State { get; set; } = TranslationJobItemState.Pending;

    public string SegmentIdsJson { get; set; } = "[]";

    public int CharacterCount { get; set; }

    public int AttemptCount { get; set; }

    public string RequestPayloadJson { get; set; } = "{}";

    public string ResponsePayloadJson { get; set; } = "{}";

    public string FailureReason { get; set; } = string.Empty;

    public DateTimeOffset? StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }
}

public sealed class TranslationProviderSettings : EntityBase
{
    public string ProviderCode { get; set; } = "yandex";

    public string DisplayName { get; set; } = "Yandex Translate";

    public bool IsEnabled { get; set; } = true;

    public string Endpoint { get; set; } = string.Empty;

    public string LanguagesEndpoint { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;

    public string SecretReference { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxBatchCharacters { get; set; } = 4000;

    public int FailureThreshold { get; set; } = 3;

    public int CircuitBreakSeconds { get; set; } = 60;

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset? LastHealthCheckUtc { get; set; }

    public string LastKnownStatus { get; set; } = "unknown";

    public string LastError { get; set; } = string.Empty;
}

public sealed class TranslationGlossary : EntityBase
{
    public Guid? LicenseId { get; set; }

    public License? License { get; set; }

    public TranslationGlossaryScope Scope { get; set; } = TranslationGlossaryScope.System;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = "en";

    public string TargetLanguage { get; set; } = "ru";

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 100;

    public string TermsJson { get; set; } = "[]";

    public string SkipTermsJson { get; set; } = "[]";

    public string FrozenTermsJson { get; set; } = "[]";
}

public sealed class TranslationQuota : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;

    public int MaxFilesPerJob { get; set; } = 32;

    public int MaxSegmentsPerJob { get; set; } = 3000;

    public int MaxCharactersPerJob { get; set; } = 120000;

    public int MaxCharactersPerDay { get; set; } = 500000;

    public int MaxConcurrentJobs { get; set; } = 2;

    public int MaxJobsPerHour { get; set; } = 10;

    public int MaxAnalysisPerHour { get; set; } = 20;

    public string Notes { get; set; } = string.Empty;
}

public sealed class TranslationUsage : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public Guid? DeviceId { get; set; }

    public Device? Device { get; set; }

    public DateOnly UsageDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public int ReservedCharacters { get; set; }

    public int ConsumedCharacters { get; set; }

    public int JobsCreated { get; set; }

    public int JobsCompleted { get; set; }

    public int JobsFailed { get; set; }

    public int JobsCancelled { get; set; }

    public int AnalysisRequests { get; set; }
}

public sealed class TranslationAuditEvent : EntityBase
{
    public Guid TranslationJobId { get; set; }

    public TranslationJob TranslationJob { get; set; } = null!;

    public Guid? LicenseId { get; set; }

    public Guid? DeviceId { get; set; }

    public Guid? AdminUserId { get; set; }

    public AuditActorType ActorType { get; set; } = AuditActorType.System;

    public string ActorIdentifier { get; set; } = string.Empty;

    public AuditSeverity Severity { get; set; } = AuditSeverity.Information;

    public string Category { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SubmodBuildArtifact : EntityBase
{
    public Guid TranslationJobId { get; set; }

    public TranslationJob TranslationJob { get; set; } = null!;

    public TranslationArtifactType ArtifactType { get; set; } = TranslationArtifactType.ResultPackage;

    public bool IsPrimary { get; set; } = true;

    public string FileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string ManifestJson { get; set; } = "{}";
}

public sealed class ModAnalysisSnapshot : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public Guid? SessionId { get; set; }

    public string ModName { get; set; } = string.Empty;

    public string ModVersion { get; set; } = string.Empty;

    public string OriginalModReference { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = "en";

    public string TargetLanguage { get; set; } = "ru";

    public string PayloadSha256 { get; set; } = string.Empty;

    public int FileCount { get; set; }

    public int SegmentCount { get; set; }

    public int CharacterCount { get; set; }

    public bool IsValid { get; set; } = true;

    public string FilesJson { get; set; } = "[]";

    public string WarningsJson { get; set; } = "[]";

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset ExpiresUtc { get; set; } = DateTimeOffset.UtcNow.AddHours(6);
}
