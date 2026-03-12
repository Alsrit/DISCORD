using Platform.Application.Models;

namespace Platform.Client.Core.Models;

public sealed class StoredSessionBundle
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresUtc { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset RefreshTokenExpiresUtc { get; set; }

    public DateTimeOffset LastValidatedUtc { get; set; }

    public LicenseStatusDto? LastKnownLicense { get; set; }
}

public sealed record DeviceProfile(
    string InstallationId,
    string DeviceFingerprint,
    string DeviceName,
    string MachineName,
    string OperatingSystem);

public sealed record ClientLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string PayloadJson);

public sealed record DiagnosticsSnapshot(
    bool ApiReachable,
    string ServerName,
    string EnvironmentName,
    string ApiVersion,
    DateTimeOffset? ServerUtcNow,
    DateTimeOffset? LastSynchronizedUtc,
    string ConnectionMessage);
