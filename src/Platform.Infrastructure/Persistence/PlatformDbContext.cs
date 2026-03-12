using Microsoft.EntityFrameworkCore;
using Platform.Domain.Administration;
using Platform.Domain.Auditing;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Updates;

namespace Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public DbSet<License> Licenses => Set<License>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<ClientSession> ClientSessions => Set<ClientSession>();

    public DbSet<LicenseActivation> LicenseActivations => Set<LicenseActivation>();

    public DbSet<LicenseAuditEvent> LicenseAuditEvents => Set<LicenseAuditEvent>();

    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();

    public DbSet<SecurityIncident> SecurityIncidents => Set<SecurityIncident>();

    public DbSet<UpdateChannelDefinition> UpdateChannels => Set<UpdateChannelDefinition>();

    public DbSet<ApplicationRelease> ApplicationReleases => Set<ApplicationRelease>();

    public DbSet<ReleaseArtifact> ReleaseArtifacts => Set<ReleaseArtifact>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedUtc = now;
                entry.Entity.UpdatedUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        ConfigureLicense(modelBuilder);
        ConfigureDevice(modelBuilder);
        ConfigureSession(modelBuilder);
        ConfigureActivation(modelBuilder);
        ConfigureAudit(modelBuilder);
        ConfigureTelemetry(modelBuilder);
        ConfigureSecurityIncident(modelBuilder);
        ConfigureUpdateChannels(modelBuilder);
        ConfigureReleases(modelBuilder);
        ConfigureAdminUsers(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureLicense(ModelBuilder builder)
    {
        builder.Entity<License>(entity =>
        {
            entity.ToTable("licenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LicenseKeyHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.LicenseKeyMasked).HasMaxLength(64).IsRequired();
            entity.Property(x => x.LookupPrefix).HasMaxLength(16).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.CustomerEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.RevocationReason).HasMaxLength(512);
            entity.HasIndex(x => x.LicenseKeyHash).IsUnique();
            entity.HasIndex(x => x.LookupPrefix);
        });
    }

    private static void ConfigureDevice(ModelBuilder builder)
    {
        builder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceFingerprintHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.InstallationId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DeviceName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.MachineName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.OperatingSystem).HasMaxLength(256).IsRequired();
            entity.Property(x => x.LastKnownIp).HasMaxLength(128);
            entity.Property(x => x.LastKnownUserAgent).HasMaxLength(1024);
            entity.Property(x => x.CurrentClientVersion).HasMaxLength(64);
            entity.Property(x => x.RevocationReason).HasMaxLength(512);
            entity.HasIndex(x => new { x.LicenseId, x.DeviceFingerprintHash, x.InstallationId }).IsUnique();
            entity.HasOne(x => x.License)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSession(ModelBuilder builder)
    {
        builder.Entity<ClientSession>(entity =>
        {
            entity.ToTable("client_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccessTokenHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.RefreshTokenHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.LastKnownIp).HasMaxLength(128);
            entity.Property(x => x.CurrentClientVersion).HasMaxLength(64);
            entity.Property(x => x.RevocationReason).HasMaxLength(512);
            entity.HasIndex(x => x.AccessTokenHash).IsUnique();
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasOne(x => x.License)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureActivation(ModelBuilder builder)
    {
        builder.Entity<LicenseActivation>(entity =>
        {
            entity.ToTable("license_activations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FailureReason).HasMaxLength(512);
            entity.Property(x => x.RequestedInstallationId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RequestedDeviceFingerprintHash).HasMaxLength(256).IsRequired();
            entity.Property(x => x.RequestedClientVersion).HasMaxLength(64);
            entity.Property(x => x.IpAddress).HasMaxLength(128);
            entity.Property(x => x.UserAgent).HasMaxLength(1024);
            entity.HasOne(x => x.License)
                .WithMany(x => x.Activations)
                .HasForeignKey(x => x.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<LicenseAuditEvent>(entity =>
        {
            entity.ToTable("license_audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorIdentifier).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(128).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.CreatedUtc);
            entity.HasOne(x => x.License)
                .WithMany(x => x.AuditEvents)
                .HasForeignKey(x => x.LicenseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureTelemetry(ModelBuilder builder)
    {
        builder.Entity<TelemetryEvent>(entity =>
        {
            entity.ToTable("telemetry_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
            entity.Property(x => x.ClientVersion).HasMaxLength(64);
            entity.HasIndex(x => x.ReceivedUtc);
        });
    }

    private static void ConfigureSecurityIncident(ModelBuilder builder)
    {
        builder.Entity<SecurityIncident>(entity =>
        {
            entity.ToTable("security_incidents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(128);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.OccurredUtc);
        });
    }

    private static void ConfigureUpdateChannels(ModelBuilder builder)
    {
        builder.Entity<UpdateChannelDefinition>(entity =>
        {
            entity.ToTable("update_channels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.HasIndex(x => x.Code).IsUnique();
        });
    }

    private static void ConfigureReleases(ModelBuilder builder)
    {
        builder.Entity<ApplicationRelease>(entity =>
        {
            entity.ToTable("application_releases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2048);
            entity.Property(x => x.MinimumSupportedVersion).HasMaxLength(64);
            entity.HasIndex(x => new { x.UpdateChannelDefinitionId, x.Version }).IsUnique();
            entity.HasOne(x => x.UpdateChannelDefinition)
                .WithMany(x => x.Releases)
                .HasForeignKey(x => x.UpdateChannelDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReleaseArtifact>(entity =>
        {
            entity.ToTable("release_artifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(256).IsRequired();
            entity.Property(x => x.SignatureBase64).HasColumnType("text");
            entity.Property(x => x.SignaturePayload).HasColumnType("text");
            entity.Property(x => x.SignatureAlgorithm).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.ApplicationRelease)
                .WithMany(x => x.Artifacts)
                .HasForeignKey(x => x.ApplicationReleaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAdminUsers(ModelBuilder builder)
    {
        builder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("admin_users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(1024).IsRequired();
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
        });
    }
}
