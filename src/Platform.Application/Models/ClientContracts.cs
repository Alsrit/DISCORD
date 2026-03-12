using Platform.Domain.Common;

namespace Platform.Application.Models;

public sealed record ActivateLicenseRequest(
    string LicenseKey,
    string InstallationId,
    string DeviceFingerprint,
    string DeviceName,
    string MachineName,
    string OperatingSystem,
    string ClientVersion,
    UpdateChannelCode PreferredChannel);

public sealed record RefreshSessionRequest(
    string RefreshToken,
    string InstallationId,
    string DeviceFingerprint,
    string ClientVersion);

public sealed record HeartbeatRequest(
    string ClientVersion,
    string OperatingSystem,
    bool IsInteractive,
    string? LastError);

public sealed record ClientTelemetryItem(
    string EventType,
    string Message,
    string Severity,
    DateTimeOffset OccurredUtc,
    string PayloadJson);

public sealed record TelemetryBatchRequest(IReadOnlyCollection<ClientTelemetryItem> Events);

public sealed record UpdateCheckRequest(string CurrentVersion, UpdateChannelCode PreferredChannel);

public sealed record ServerInfoDto(
    string ServerName,
    string EnvironmentName,
    string ApiVersion,
    DateTimeOffset UtcNow,
    bool IsDatabaseReachable,
    bool IsRedisReachable);

public sealed record LicenseStatusDto(
    Guid LicenseId,
    string MaskedKey,
    string CustomerName,
    string CustomerEmail,
    string Status,
    LicenseType Type,
    DateTimeOffset? ExpiresUtc,
    int MaxDevices,
    int ActiveDevices,
    int OfflineGracePeriodHours,
    UpdateChannelCode UpdateChannel,
    DateTimeOffset LastValidatedUtc,
    string Message);

public sealed record SessionTokensDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresUtc);

public sealed record ActivationResponse(
    SessionTokensDto Tokens,
    LicenseStatusDto License,
    ServerInfoDto Server);

public sealed record LicenseSyncResponse(
    LicenseStatusDto License,
    ServerInfoDto Server,
    DateTimeOffset LastSynchronizedUtc,
    bool LicenseValid);

public sealed record UpdatePackageDto(
    Guid ReleaseId,
    string Version,
    bool Mandatory,
    string Summary,
    string Channel,
    string DownloadUrl,
    string Sha256,
    string SignatureBase64,
    string SignaturePayload,
    string SignatureAlgorithm,
    long SizeBytes,
    DateTimeOffset PublishedUtc);

public sealed record UpdateCheckResponse(
    bool UpdateAvailable,
    string CurrentVersion,
    UpdatePackageDto? Package,
    string Message);
