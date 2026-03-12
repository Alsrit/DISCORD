using System.Text.Json;
using Platform.Application.Abstractions;
using Platform.Domain.Auditing;
using Platform.Domain.Common;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services;

public sealed class SecurityIncidentService(PlatformDbContext dbContext) : ISecurityIncidentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CaptureAsync(
        SecurityIncidentType type,
        string description,
        AuditSeverity severity,
        RequestContext context,
        Guid? licenseId,
        Guid? deviceId,
        Guid? sessionId,
        object? payload,
        CancellationToken cancellationToken)
    {
        dbContext.SecurityIncidents.Add(new SecurityIncident
        {
            LicenseId = licenseId,
            DeviceId = deviceId,
            SessionId = sessionId,
            Type = type,
            Severity = severity,
            Description = description,
            IpAddress = context.IpAddress,
            PayloadJson = JsonSerializer.Serialize(payload ?? new { }, JsonOptions)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
