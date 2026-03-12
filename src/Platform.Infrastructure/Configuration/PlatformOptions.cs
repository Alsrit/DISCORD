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
