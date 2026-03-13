namespace Platform.Domain.Common;

public enum LicenseType
{
    Trial = 1,
    Subscription = 2,
    Perpetual = 3
}

public enum LicenseState
{
    Draft = 1,
    Active = 2,
    Suspended = 3,
    Revoked = 4
}

public enum DeviceState
{
    Active = 1,
    Revoked = 2,
    Blocked = 3
}

public enum SessionState
{
    Active = 1,
    Revoked = 2,
    Expired = 3
}

public enum AuditSeverity
{
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public enum AuditActorType
{
    System = 1,
    Client = 2,
    Administrator = 3
}

public enum TelemetrySeverity
{
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public enum UpdateChannelCode
{
    Stable = 1,
    Beta = 2,
    Internal = 3
}

public enum ReleaseState
{
    Draft = 1,
    Published = 2,
    Retired = 3
}

public enum AdminRole
{
    Administrator = 1,
    Operator = 2,
    Auditor = 3
}

public enum SecurityIncidentType
{
    InvalidLicenseKey = 1,
    ActivationRateLimited = 2,
    DeviceCapExceeded = 3,
    RefreshTokenReplay = 4,
    DeviceRevokedAttempt = 5,
    UpdateSignatureFailure = 6,
    CertificatePinMismatch = 7,
    SessionRejected = 8,
    TranslationRateLimited = 9,
    TranslationQuotaExceeded = 10,
    TranslationPayloadRejected = 11,
    TranslationProviderFailure = 12,
    TranslationReplayDetected = 13,
    TranslationPathTraversalDetected = 14
}

public enum TranslationJobState
{
    Pending = 1,
    Queued = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5,
    CancelRequested = 6,
    Cancelled = 7,
    Expired = 8
}

public enum TranslationFileState
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum TranslationSegmentState
{
    Pending = 1,
    Protected = 2,
    Translated = 3,
    Validated = 4,
    Failed = 5,
    Skipped = 6
}

public enum TranslationJobItemState
{
    Pending = 1,
    Processing = 2,
    Retrying = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public enum TranslationGlossaryScope
{
    System = 1,
    Game = 2,
    License = 3
}

public enum TranslationArtifactType
{
    ResultPackage = 1,
    ManifestPreview = 2,
    AnalysisReport = 3
}
