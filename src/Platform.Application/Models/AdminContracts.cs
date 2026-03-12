using Platform.Domain.Common;

namespace Platform.Application.Models;

public sealed record DashboardSummaryDto(
    int TotalLicenses,
    int ActiveLicenses,
    int OnlineDevices,
    int RevokedDevices,
    int PendingIncidents,
    int PublishedReleases,
    int TelemetryErrorsLast24Hours);

public sealed record LicenseListItemDto(
    Guid Id,
    string MaskedKey,
    string CustomerName,
    string CustomerEmail,
    string Status,
    LicenseType Type,
    DateTimeOffset? ExpiresUtc,
    int MaxDevices,
    int ActiveDevices,
    UpdateChannelCode UpdateChannel);

public sealed record DeviceListItemDto(
    Guid Id,
    Guid LicenseId,
    string DeviceName,
    string MachineName,
    string ClientVersion,
    string OperatingSystem,
    string Status,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc);

public sealed record AuditEventDto(
    DateTimeOffset CreatedUtc,
    string Severity,
    string Category,
    string EventType,
    string Message,
    string Actor,
    string PayloadJson);

public sealed record ReleaseListItemDto(
    Guid Id,
    string Version,
    string Channel,
    string State,
    bool Mandatory,
    DateTimeOffset? PublishedUtc,
    string Summary);

public sealed record TelemetryListItemDto(
    DateTimeOffset OccurredUtc,
    string Severity,
    string EventType,
    string Message,
    string ClientVersion);

public sealed record SecurityIncidentDto(
    Guid Id,
    DateTimeOffset OccurredUtc,
    string Type,
    string Severity,
    string Description,
    string IpAddress,
    bool Resolved);

public sealed record CreateLicenseRequest(
    string CustomerName,
    string CustomerEmail,
    LicenseType Type,
    int MaxDevices,
    int OfflineGracePeriodHours,
    DateTimeOffset? ExpiresUtc,
    UpdateChannelCode UpdateChannel,
    string Notes);

public sealed record ExtendLicenseRequest(Guid LicenseId, DateTimeOffset? NewExpiryUtc, string Comment);

public sealed record RevokeLicenseRequest(Guid LicenseId, string Reason);

public sealed record RevokeDeviceRequest(Guid DeviceId, string Reason);

public sealed record PublishReleaseRequest(
    string Version,
    UpdateChannelCode Channel,
    bool Mandatory,
    string MinimumSupportedVersion,
    string Summary);

public sealed record AdminAuthenticationResult(Guid UserId, string UserName, string DisplayName, string Role);
