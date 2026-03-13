using Microsoft.EntityFrameworkCore;
using Platform.Domain.Administration;
using Platform.Domain.Auditing;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Translations;
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

    public DbSet<TranslationJob> TranslationJobs => Set<TranslationJob>();

    public DbSet<TranslationJobItem> TranslationJobItems => Set<TranslationJobItem>();

    public DbSet<TranslationSegment> TranslationSegments => Set<TranslationSegment>();

    public DbSet<TranslationFile> TranslationFiles => Set<TranslationFile>();

    public DbSet<TranslationProviderSettings> TranslationProviderSettings => Set<TranslationProviderSettings>();

    public DbSet<TranslationGlossary> TranslationGlossaries => Set<TranslationGlossary>();

    public DbSet<TranslationQuota> TranslationQuotas => Set<TranslationQuota>();

    public DbSet<TranslationUsage> TranslationUsages => Set<TranslationUsage>();

    public DbSet<TranslationAuditEvent> TranslationAuditEvents => Set<TranslationAuditEvent>();

    public DbSet<SubmodBuildArtifact> SubmodBuildArtifacts => Set<SubmodBuildArtifact>();

    public DbSet<ModAnalysisSnapshot> ModAnalysisSnapshots => Set<ModAnalysisSnapshot>();

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
        ConfigureTranslations(modelBuilder);

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

    private static void ConfigureTranslations(ModelBuilder builder)
    {
        builder.Entity<TranslationJob>(entity =>
        {
            entity.ToTable("translation_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.QueueName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ProviderCode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ModName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.OriginalModReference).HasMaxLength(512);
            entity.Property(x => x.RequestedSubmodName).HasMaxLength(256);
            entity.Property(x => x.SourceLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TargetLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.FailureCode).HasMaxLength(128);
            entity.Property(x => x.FailureReason).HasMaxLength(2048);
            entity.Property(x => x.ResultStoragePath).HasMaxLength(512);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.LicenseId, x.DeviceId, x.IdempotencyKey });
            entity.HasIndex(x => new { x.State, x.RequestedUtc });
            entity.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AnalysisSnapshot).WithMany().HasForeignKey(x => x.AnalysisSnapshotId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TranslationFile>(entity =>
        {
            entity.ToTable("translation_files");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RelativePath).HasMaxLength(512).IsRequired();
            entity.Property(x => x.SanitizedPath).HasMaxLength(512).IsRequired();
            entity.Property(x => x.HeaderKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SourceLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TargetLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.OriginalSha256).HasMaxLength(128).IsRequired();
            entity.Property(x => x.WarningJson).HasColumnType("jsonb");
            entity.Property(x => x.OriginalContent).HasColumnType("text");
            entity.Property(x => x.TranslatedContent).HasColumnType("text");
            entity.HasIndex(x => new { x.TranslationJobId, x.SanitizedPath }).IsUnique();
            entity.HasOne(x => x.TranslationJob).WithMany(x => x.Files).HasForeignKey(x => x.TranslationJobId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TranslationSegment>(entity =>
        {
            entity.ToTable("translation_segments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LocalizationKey).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Prefix).HasColumnType("text");
            entity.Property(x => x.Suffix).HasColumnType("text");
            entity.Property(x => x.SourceText).HasColumnType("text");
            entity.Property(x => x.ProtectedSourceText).HasColumnType("text");
            entity.Property(x => x.ProtectedTranslationText).HasColumnType("text");
            entity.Property(x => x.FinalText).HasColumnType("text");
            entity.Property(x => x.PlaceholderMapJson).HasColumnType("jsonb");
            entity.Property(x => x.ValidationMessage).HasMaxLength(2048);
            entity.HasIndex(x => new { x.TranslationFileId, x.Sequence }).IsUnique();
            entity.HasOne(x => x.TranslationFile).WithMany(x => x.Segments).HasForeignKey(x => x.TranslationFileId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TranslationJobItem>(entity =>
        {
            entity.ToTable("translation_job_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProviderCode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.SegmentIdsJson).HasColumnType("jsonb");
            entity.Property(x => x.RequestPayloadJson).HasColumnType("jsonb");
            entity.Property(x => x.ResponsePayloadJson).HasColumnType("jsonb");
            entity.Property(x => x.FailureReason).HasMaxLength(2048);
            entity.HasIndex(x => new { x.TranslationJobId, x.BatchNumber }).IsUnique();
            entity.HasOne(x => x.TranslationJob).WithMany(x => x.Items).HasForeignKey(x => x.TranslationJobId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TranslationProviderSettings>(entity =>
        {
            entity.ToTable("translation_provider_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProviderCode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Endpoint).HasMaxLength(512).IsRequired();
            entity.Property(x => x.LanguagesEndpoint).HasMaxLength(512).IsRequired();
            entity.Property(x => x.FolderId).HasMaxLength(128);
            entity.Property(x => x.SecretReference).HasMaxLength(128);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.Property(x => x.LastKnownStatus).HasMaxLength(64);
            entity.Property(x => x.LastError).HasMaxLength(2048);
            entity.HasIndex(x => x.ProviderCode).IsUnique();
        });

        builder.Entity<TranslationGlossary>(entity =>
        {
            entity.ToTable("translation_glossaries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1024);
            entity.Property(x => x.SourceLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TargetLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TermsJson).HasColumnType("jsonb");
            entity.Property(x => x.SkipTermsJson).HasColumnType("jsonb");
            entity.Property(x => x.FrozenTermsJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.Scope, x.LicenseId, x.SourceLanguage, x.TargetLanguage, x.Name });
            entity.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TranslationQuota>(entity =>
        {
            entity.ToTable("translation_quotas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Notes).HasMaxLength(1024);
            entity.HasIndex(x => x.LicenseId).IsUnique();
            entity.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TranslationUsage>(entity =>
        {
            entity.ToTable("translation_usage");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.LicenseId, x.DeviceId, x.UsageDate }).IsUnique();
            entity.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TranslationAuditEvent>(entity =>
        {
            entity.ToTable("translation_audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorIdentifier).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(128).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.TranslationJobId, x.OccurredUtc });
            entity.HasOne(x => x.TranslationJob).WithMany(x => x.AuditEvents).HasForeignKey(x => x.TranslationJobId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SubmodBuildArtifact>(entity =>
        {
            entity.ToTable("submod_build_artifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ManifestJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.TranslationJobId, x.IsPrimary });
            entity.HasOne(x => x.TranslationJob).WithMany(x => x.Artifacts).HasForeignKey(x => x.TranslationJobId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModAnalysisSnapshot>(entity =>
        {
            entity.ToTable("mod_analysis_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ModName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ModVersion).HasMaxLength(64);
            entity.Property(x => x.OriginalModReference).HasMaxLength(512);
            entity.Property(x => x.SourceLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.TargetLanguage).HasMaxLength(16).IsRequired();
            entity.Property(x => x.PayloadSha256).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FilesJson).HasColumnType("jsonb");
            entity.Property(x => x.WarningsJson).HasColumnType("jsonb");
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.LicenseId, x.DeviceId, x.PayloadSha256 });
            entity.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
