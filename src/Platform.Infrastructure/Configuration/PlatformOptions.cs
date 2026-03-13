namespace Platform.Infrastructure.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string LicensePepper { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 120;

    public int RefreshTokenLifetimeDays { get; set; } = 30;

    public int ActivationLimitPerHour { get; set; } = 10;

    public int RefreshLimitPerHour { get; set; } = 120;

    public int TelemetryLimitPerMinute { get; set; } = 200;

    public int DeviceOfflineThresholdMinutes { get; set; } = 15;
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; } = true;

    public string Configuration { get; set; } = "localhost:6379";
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string ReleaseStorageRoot { get; set; } = "storage/releases";

    public string TranslationStorageRoot { get; set; } = "storage/translations";

    public string TranslationTempRoot { get; set; } = "storage/translations-temp";
}

public sealed class ServerIdentityOptions
{
    public const string SectionName = "ServerIdentity";

    public string ServerName { get; set; } = "SecureLicensePlatform";

    public string EnvironmentDisplayName { get; set; } = "Development";
}

public sealed class UpdateSigningOptions
{
    public const string SectionName = "UpdateSigning";

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string PublicKeyPath { get; set; } = string.Empty;
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminUserName { get; set; } = "admin";

    public string AdminDisplayName { get; set; } = "Главный администратор";

    public string AdminEmail { get; set; } = "admin@example.com";

    public string AdminPassword { get; set; } = "ChangeThisPassword!";

    public List<DemoLicenseOptions> DemoLicenses { get; set; } = new();
}

public sealed class DemoLicenseOptions
{
    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public string RawLicenseKey { get; set; } = string.Empty;

    public string Type { get; set; } = "Subscription";

    public int MaxDevices { get; set; } = 1;

    public int OfflineGracePeriodHours { get; set; } = 72;

    public string UpdateChannel { get; set; } = "Stable";

    public DateTimeOffset? ExpiresUtc { get; set; }
}

public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    public string QueueName { get; set; } = "translations";

    public int MaxPayloadBytes { get; set; } = 2_000_000;

    public int MaxFilesPerRequest { get; set; } = 64;

    public int MaxSegmentsPerRequest { get; set; } = 4_000;

    public int MaxCharactersPerRequest { get; set; } = 120_000;

    public int QueuePollIntervalSeconds { get; set; } = 5;

    public int ProcessingLeaseSeconds { get; set; } = 120;

    public int JobTimeoutMinutes { get; set; } = 30;

    public int SnapshotRetentionHours { get; set; } = 6;

    public int ArtifactRetentionHours { get; set; } = 72;

    public int RetryBaseDelaySeconds { get; set; } = 5;

    public int MaxRetryAttempts { get; set; } = 3;

    public int AnalyzeLimitPerHour { get; set; } = 20;

    public int JobCreateLimitPerHour { get; set; } = 10;

    public int WorkerBatchCharacters { get; set; } = 3_500;

    public int ProviderThrottlePerMinute { get; set; } = 120;
}

public sealed class YandexTranslateOptions
{
    public const string SectionName = "TranslationProviders:Yandex";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = "https://translate.api.cloud.yandex.net/translate/v2/translate";

    public string LanguagesEndpoint { get; set; } = "https://translate.api.cloud.yandex.net/translate/v2/languages";

    public string FolderId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyEnvVar { get; set; } = "YANDEX_TRANSLATE_API_KEY";

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxBatchCharacters { get; set; } = 4_000;

    public int FailureThreshold { get; set; } = 3;

    public int CircuitBreakSeconds { get; set; } = 60;
}
