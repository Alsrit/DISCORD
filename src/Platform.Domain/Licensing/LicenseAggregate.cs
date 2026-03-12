using Platform.Domain.Common;
using Platform.Domain.Auditing;

namespace Platform.Domain.Licensing;

public sealed class License : EntityBase
{
    public string LicenseKeyHash { get; set; } = string.Empty;

    public string LicenseKeyMasked { get; set; } = string.Empty;

    public string LookupPrefix { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerEmail { get; set; } = string.Empty;

    public LicenseType Type { get; set; } = LicenseType.Subscription;

    public LicenseState State { get; set; } = LicenseState.Active;

    public int MaxDevices { get; set; } = 1;

    public int OfflineGracePeriodHours { get; set; } = 72;

    public DateTimeOffset? ExpiresUtc { get; set; }

    public DateTimeOffset? RevokedUtc { get; set; }

    public string? RevocationReason { get; set; }

    public UpdateChannelCode UpdateChannel { get; set; } = UpdateChannelCode.Stable;

    public string Notes { get; set; } = string.Empty;

    public ICollection<Device> Devices { get; set; } = new List<Device>();

    public ICollection<ClientSession> Sessions { get; set; } = new List<ClientSession>();

    public ICollection<LicenseActivation> Activations { get; set; } = new List<LicenseActivation>();

    public ICollection<LicenseAuditEvent> AuditEvents { get; set; } = new List<LicenseAuditEvent>();

    public bool IsExpired(DateTimeOffset utcNow) => ExpiresUtc.HasValue && ExpiresUtc.Value <= utcNow;

    public bool IsUsable(DateTimeOffset utcNow) =>
        State == LicenseState.Active &&
        RevokedUtc is null &&
        !IsExpired(utcNow);

    public string GetDisplayState(DateTimeOffset utcNow)
    {
        if (State == LicenseState.Revoked || RevokedUtc is not null)
        {
            return "Заблокирована";
        }

        if (State == LicenseState.Suspended)
        {
            return "Приостановлена";
        }

        if (IsExpired(utcNow))
        {
            return "Истекла";
        }

        if (ExpiresUtc.HasValue && ExpiresUtc.Value <= utcNow.AddDays(7))
        {
            return "Истекает";
        }

        return "Активна";
    }
}

public sealed class Device : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public string DeviceFingerprintHash { get; set; } = string.Empty;

    public string InstallationId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string LastKnownIp { get; set; } = string.Empty;

    public string LastKnownUserAgent { get; set; } = string.Empty;

    public string CurrentClientVersion { get; set; } = string.Empty;

    public DeviceState State { get; set; } = DeviceState.Active;

    public DateTimeOffset FirstSeenUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RevokedUtc { get; set; }

    public string? RevocationReason { get; set; }

    public ICollection<ClientSession> Sessions { get; set; } = new List<ClientSession>();

    public bool Matches(string fingerprintHash, string installationId) =>
        string.Equals(DeviceFingerprintHash, fingerprintHash, StringComparison.Ordinal) &&
        string.Equals(InstallationId, installationId, StringComparison.Ordinal);

    public bool IsUsable() => State == DeviceState.Active && RevokedUtc is null;
}

public sealed class ClientSession : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public Guid DeviceId { get; set; }

    public Device Device { get; set; } = null!;

    public string AccessTokenHash { get; set; } = string.Empty;

    public string RefreshTokenHash { get; set; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresUtc { get; set; }

    public DateTimeOffset RefreshTokenExpiresUtc { get; set; }

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;

    public string LastKnownIp { get; set; } = string.Empty;

    public string CurrentClientVersion { get; set; } = string.Empty;

    public SessionState State { get; set; } = SessionState.Active;

    public DateTimeOffset? RevokedUtc { get; set; }

    public string? RevocationReason { get; set; }

    public bool IsAccessTokenValid(DateTimeOffset utcNow) =>
        State == SessionState.Active &&
        RevokedUtc is null &&
        AccessTokenExpiresUtc > utcNow;

    public bool IsRefreshTokenValid(DateTimeOffset utcNow) =>
        State == SessionState.Active &&
        RevokedUtc is null &&
        RefreshTokenExpiresUtc > utcNow;
}

public sealed class LicenseActivation : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public Guid? DeviceId { get; set; }

    public Device? Device { get; set; }

    public bool Success { get; set; }

    public string FailureReason { get; set; } = string.Empty;

    public string RequestedInstallationId { get; set; } = string.Empty;

    public string RequestedDeviceFingerprintHash { get; set; } = string.Empty;

    public string RequestedClientVersion { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;
}
