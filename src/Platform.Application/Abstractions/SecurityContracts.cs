using Platform.Domain.Auditing;
using Platform.Domain.Common;

namespace Platform.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ILicenseKeyProtector
{
    string Hash(string rawLicenseKey);

    string Mask(string rawLicenseKey);

    string GetLookupPrefix(string rawLicenseKey);
}

public sealed record IssuedTokenPair(
    string AccessToken,
    string AccessTokenHash,
    DateTimeOffset AccessTokenExpiresUtc,
    string RefreshToken,
    string RefreshTokenHash,
    DateTimeOffset RefreshTokenExpiresUtc);

public interface ITokenService
{
    IssuedTokenPair CreateTokenPair(TimeSpan accessLifetime, TimeSpan refreshLifetime);

    string HashToken(string rawToken);
}

public interface IRateLimitService
{
    Task<bool> ConsumeAsync(string bucket, string key, int limit, TimeSpan window, CancellationToken cancellationToken);
}

public interface IUpdateSignatureService
{
    Task<(string Payload, string SignatureBase64, string Algorithm)> SignReleaseAsync(
        string version,
        string channel,
        string sha256,
        bool mandatory,
        DateTimeOffset publishedUtc,
        CancellationToken cancellationToken);
}

public sealed record AuthenticatedSessionContext(
    Guid SessionId,
    Guid LicenseId,
    Guid DeviceId,
    string CustomerName,
    string LicenseStatus,
    string ClientVersion);

public interface ISessionTokenValidator
{
    Task<AuthenticatedSessionContext?> ValidateAccessTokenAsync(string rawAccessToken, CancellationToken cancellationToken);
}

public interface IAuditTrailService
{
    Task WriteAsync(
        Guid? licenseId,
        Guid? deviceId,
        Guid? adminUserId,
        string category,
        string eventType,
        string message,
        AuditSeverity severity,
        RequestContext context,
        object? payload,
        CancellationToken cancellationToken);
}

public interface ISecurityIncidentService
{
    Task CaptureAsync(
        SecurityIncidentType type,
        string description,
        AuditSeverity severity,
        RequestContext context,
        Guid? licenseId,
        Guid? deviceId,
        Guid? sessionId,
        object? payload,
        CancellationToken cancellationToken);
}
