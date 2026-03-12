using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Domain.Auditing;
using Platform.Domain.Common;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services;

public sealed class AuditTrailService(PlatformDbContext dbContext) : IAuditTrailService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(
        Guid? licenseId,
        Guid? deviceId,
        Guid? adminUserId,
        string category,
        string eventType,
        string message,
        AuditSeverity severity,
        RequestContext context,
        object? payload,
        CancellationToken cancellationToken)
    {
        if (licenseId is null)
        {
            return;
        }

        var exists = await dbContext.Licenses.AnyAsync(x => x.Id == licenseId.Value, cancellationToken);
        if (!exists)
        {
            return;
        }

        dbContext.LicenseAuditEvents.Add(new LicenseAuditEvent
        {
            LicenseId = licenseId.Value,
            DeviceId = deviceId,
            AdminUserId = adminUserId,
            ActorType = adminUserId.HasValue ? AuditActorType.Administrator : AuditActorType.Client,
            ActorIdentifier = context.AdminUserName ?? context.IpAddress,
            Severity = severity,
            Category = category,
            EventType = eventType,
            Message = message,
            PayloadJson = JsonSerializer.Serialize(payload ?? new { }, JsonOptions)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
