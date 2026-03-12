using Platform.Domain.Common;
using Platform.Domain.Licensing;

namespace Platform.Domain.Auditing;

public sealed class LicenseAuditEvent : EntityBase
{
    public Guid LicenseId { get; set; }

    public License License { get; set; } = null!;

    public Guid? DeviceId { get; set; }

    public Device? Device { get; set; }

    public Guid? AdminUserId { get; set; }

    public AuditActorType ActorType { get; set; }

    public string ActorIdentifier { get; set; } = string.Empty;

    public AuditSeverity Severity { get; set; } = AuditSeverity.Information;

    public string Category { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";
}

public sealed class TelemetryEvent : EntityBase
{
    public Guid? LicenseId { get; set; }

    public Guid? DeviceId { get; set; }

    public Guid? SessionId { get; set; }

    public TelemetrySeverity Severity { get; set; } = TelemetrySeverity.Information;

    public string EventType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string ClientVersion { get; set; } = string.Empty;

    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ReceivedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SecurityIncident : EntityBase
{
    public Guid? LicenseId { get; set; }

    public Guid? DeviceId { get; set; }

    public Guid? SessionId { get; set; }

    public SecurityIncidentType Type { get; set; }

    public AuditSeverity Severity { get; set; } = AuditSeverity.Warning;

    public string Description { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedUtc { get; set; }

    public Guid? ResolvedByAdminUserId { get; set; }
}
