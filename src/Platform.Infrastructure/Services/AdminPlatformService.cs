using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Administration;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Translations;
using Platform.Domain.Updates;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Services.Translations;

namespace Platform.Infrastructure.Services;

public sealed class AdminPlatformService(
    PlatformDbContext dbContext,
    ILicenseKeyProtector licenseKeyProtector,
    IUpdateSignatureService updateSignatureService,
    IAuditTrailService auditTrailService,
    ITranslationQueueService translationQueueService,
    IClock clock,
    IOptions<StorageOptions> storageOptions,
    ILogger<AdminPlatformService> logger) : IAdminPlatformService
{
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();
    private readonly StorageOptions _storage = storageOptions.Value;

    public async Task<DashboardSummaryDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return new DashboardSummaryDto(
            await dbContext.Licenses.CountAsync(cancellationToken),
            await dbContext.Licenses.CountAsync(x => x.State == LicenseState.Active && (x.ExpiresUtc == null || x.ExpiresUtc > now) && x.RevokedUtc == null, cancellationToken),
            await dbContext.Devices.CountAsync(x => x.State == DeviceState.Active && x.LastSeenUtc >= now.AddMinutes(-15), cancellationToken),
            await dbContext.Devices.CountAsync(x => x.State != DeviceState.Active, cancellationToken),
            await dbContext.SecurityIncidents.CountAsync(x => x.ResolvedUtc == null, cancellationToken),
            await dbContext.ApplicationReleases.CountAsync(x => x.State == ReleaseState.Published, cancellationToken),
            await dbContext.TelemetryEvents.CountAsync(x => x.ReceivedUtc >= now.AddHours(-24) && x.Severity >= TelemetrySeverity.Error, cancellationToken));
    }

    public async Task<IReadOnlyCollection<LicenseListItemDto>> GetLicensesAsync(CancellationToken cancellationToken) =>
        await dbContext.Licenses
            .Include(x => x.Devices)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new LicenseListItemDto(
                x.Id,
                x.LicenseKeyMasked,
                x.CustomerName,
                x.CustomerEmail,
                x.State == LicenseState.Revoked || x.RevokedUtc != null
                    ? "Заблокирована"
                    : x.ExpiresUtc != null && x.ExpiresUtc <= DateTimeOffset.UtcNow
                        ? "Истекла"
                        : "Активна",
                x.Type,
                x.ExpiresUtc,
                x.MaxDevices,
                x.Devices.Count(d => d.State == DeviceState.Active && d.RevokedUtc == null),
                x.UpdateChannel))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DeviceListItemDto>> GetDevicesAsync(CancellationToken cancellationToken) =>
        await dbContext.Devices
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new DeviceListItemDto(
                x.Id,
                x.LicenseId,
                x.DeviceName,
                x.MachineName,
                x.CurrentClientVersion,
                x.OperatingSystem,
                x.State == DeviceState.Active ? "Активно" : "Заблокировано",
                x.FirstSeenUtc,
                x.LastSeenUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AuditEventDto>> GetAuditEventsAsync(CancellationToken cancellationToken) =>
        await dbContext.LicenseAuditEvents
            .OrderByDescending(x => x.CreatedUtc)
            .Take(200)
            .Select(x => new AuditEventDto(
                x.CreatedUtc,
                x.Severity.ToString(),
                x.Category,
                x.EventType,
                x.Message,
                x.ActorIdentifier,
                x.PayloadJson))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ReleaseListItemDto>> GetReleasesAsync(CancellationToken cancellationToken) =>
        await dbContext.ApplicationReleases
            .Include(x => x.UpdateChannelDefinition)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new ReleaseListItemDto(
                x.Id,
                x.Version,
                x.UpdateChannelDefinition.DisplayName,
                x.State.ToString(),
                x.IsMandatory,
                x.PublishedUtc,
                x.Summary))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TelemetryListItemDto>> GetTelemetryAsync(CancellationToken cancellationToken) =>
        await dbContext.TelemetryEvents
            .OrderByDescending(x => x.ReceivedUtc)
            .Take(200)
            .Select(x => new TelemetryListItemDto(
                x.OccurredUtc,
                x.Severity.ToString(),
                x.EventType,
                x.Message,
                x.ClientVersion))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SecurityIncidentDto>> GetSecurityIncidentsAsync(CancellationToken cancellationToken) =>
        await dbContext.SecurityIncidents
            .OrderByDescending(x => x.OccurredUtc)
            .Take(200)
            .Select(x => new SecurityIncidentDto(
                x.Id,
                x.OccurredUtc,
                x.Type.ToString(),
                x.Severity.ToString(),
                x.Description,
                x.IpAddress,
                x.ResolvedUtc != null))
            .ToListAsync(cancellationToken);

    public async Task<PagedResultDto<TranslationJobAdminListItemDto>> GetTranslationJobsAsync(
        TranslationJobListQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var jobsQuery = dbContext.TranslationJobs
            .Include(x => x.License)
            .Include(x => x.Device)
            .AsQueryable();

        if (query.State.HasValue)
        {
            jobsQuery = jobsQuery.Where(x => x.State == query.State.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            jobsQuery = jobsQuery.Where(x =>
                x.ModName.Contains(search) ||
                x.License.LicenseKeyMasked.Contains(search) ||
                x.Device.DeviceName.Contains(search));
        }

        var totalCount = await jobsQuery.CountAsync(cancellationToken);
        var items = await jobsQuery
            .OrderByDescending(x => x.RequestedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TranslationJobAdminListItemDto(
                x.Id,
                x.State.ToString(),
                x.ProviderCode,
                x.ModName,
                x.SourceLanguage,
                x.TargetLanguage,
                x.License.LicenseKeyMasked,
                x.Device.DeviceName,
                x.TotalFiles,
                x.TotalSegments,
                x.TotalCharacters,
                x.RetryCount,
                x.FailureReason,
                x.RequestedUtc,
                x.CompletedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<TranslationJobAdminListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<TranslationUsageAdminDto>> GetTranslationUsageAsync(CancellationToken cancellationToken) =>
        await dbContext.TranslationUsages
            .Include(x => x.License)
            .OrderByDescending(x => x.UsageDate)
            .ThenByDescending(x => x.UpdatedUtc)
            .Take(200)
            .Select(x => new TranslationUsageAdminDto(
                x.LicenseId,
                x.License.LicenseKeyMasked,
                x.License.CustomerName,
                x.UsageDate,
                x.ReservedCharacters,
                x.ConsumedCharacters,
                x.JobsCreated,
                x.JobsCompleted,
                x.JobsFailed,
                x.JobsCancelled,
                x.AnalysisRequests))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TranslationQuotaAdminDto>> GetTranslationQuotasAsync(CancellationToken cancellationToken) =>
        await dbContext.TranslationQuotas
            .Include(x => x.License)
            .OrderBy(x => x.License.CustomerName)
            .Select(x => new TranslationQuotaAdminDto(
                x.LicenseId,
                x.License.LicenseKeyMasked,
                x.MaxFilesPerJob,
                x.MaxSegmentsPerJob,
                x.MaxCharactersPerJob,
                x.MaxCharactersPerDay,
                x.MaxConcurrentJobs,
                x.MaxJobsPerHour,
                x.MaxAnalysisPerHour,
                x.IsEnabled,
                x.Notes))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TranslationGlossaryAdminDto>> GetTranslationGlossariesAsync(CancellationToken cancellationToken) =>
        await dbContext.TranslationGlossaries
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => new TranslationGlossaryAdminDto(
                x.Id,
                x.LicenseId,
                x.Name,
                x.Scope,
                x.SourceLanguage,
                x.TargetLanguage,
                x.Priority,
                x.IsActive,
                TranslationJson.Deserialize(x.TermsJson, Array.Empty<TranslationGlossaryTermDto>()).Length,
                TranslationJson.Deserialize(x.FrozenTermsJson, Array.Empty<string>()).Length,
                TranslationJson.Deserialize(x.SkipTermsJson, Array.Empty<string>()).Length,
                x.Description))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<TranslationProviderAdminDto>> GetTranslationProvidersAsync(CancellationToken cancellationToken) =>
        await dbContext.TranslationProviderSettings
            .OrderBy(x => x.ProviderCode)
            .Select(x => new TranslationProviderAdminDto(
                x.Id,
                x.ProviderCode,
                x.DisplayName,
                x.IsEnabled,
                x.Endpoint,
                x.LanguagesEndpoint,
                x.FolderId,
                x.SecretReference,
                x.LastKnownStatus,
                x.LastError,
                x.LastHealthCheckUtc,
                x.TimeoutSeconds,
                x.MaxBatchCharacters))
            .ToListAsync(cancellationToken);

    public async Task<TranslationQueueStatusDto> GetTranslationQueueStatusAsync(CancellationToken cancellationToken)
    {
        var queued = await dbContext.TranslationJobs.CountAsync(x => x.State == TranslationJobState.Queued, cancellationToken);
        var processing = await dbContext.TranslationJobs.CountAsync(x => x.State == TranslationJobState.Processing, cancellationToken);
        var failed = await dbContext.TranslationJobs.CountAsync(x => x.State == TranslationJobState.Failed, cancellationToken);
        var cancelRequested = await dbContext.TranslationJobs.CountAsync(x => x.State == TranslationJobState.CancelRequested, cancellationToken);
        var queueLength = await translationQueueService.GetQueuedCountAsync(cancellationToken);

        return new TranslationQueueStatusDto(
            queueLength >= 0 ? $"translation-jobs ({queueLength})" : "translation-jobs",
            queued,
            processing,
            failed,
            cancelRequested,
            clock.UtcNow);
    }

    public async Task<OperationResult<string>> CreateLicenseAsync(
        CreateLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var rawKey = GenerateLicenseKey();
        var license = new License
        {
            LicenseKeyHash = licenseKeyProtector.Hash(rawKey),
            LicenseKeyMasked = licenseKeyProtector.Mask(rawKey),
            LookupPrefix = licenseKeyProtector.GetLookupPrefix(rawKey),
            CustomerName = request.CustomerName?.Trim() ?? string.Empty,
            CustomerEmail = request.CustomerEmail?.Trim() ?? string.Empty,
            Type = request.Type,
            State = LicenseState.Active,
            MaxDevices = request.MaxDevices,
            OfflineGracePeriodHours = request.OfflineGracePeriodHours,
            ExpiresUtc = request.ExpiresUtc,
            UpdateChannel = request.UpdateChannel,
            Notes = request.Notes?.Trim() ?? string.Empty
        };

        dbContext.Licenses.Add(license);
        dbContext.TranslationQuotas.Add(new TranslationQuota
        {
            License = license,
            MaxFilesPerJob = 64,
            MaxSegmentsPerJob = 4000,
            MaxCharactersPerJob = 120000,
            MaxCharactersPerDay = 480000,
            MaxConcurrentJobs = 2,
            MaxJobsPerHour = 10,
            MaxAnalysisPerHour = 20,
            IsEnabled = true,
            Notes = "Базовая квота, созданная вместе с лицензией."
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditTrailService.WriteAsync(
            license.Id,
            null,
            null,
            "admin",
            "license_created",
            "Создана новая лицензия.",
            AuditSeverity.Information,
            context,
            new { request.CustomerName, request.CustomerEmail, request.MaxDevices },
            cancellationToken);

        return OperationResult<string>.Success(rawKey, "Лицензия создана.");
    }

    public async Task<OperationResult> ExtendLicenseAsync(
        ExtendLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var license = await dbContext.Licenses.FirstOrDefaultAsync(x => x.Id == request.LicenseId, cancellationToken);
        if (license is null)
        {
            return OperationResult.Failure("Лицензия не найдена.", "license_not_found");
        }

        license.ExpiresUtc = request.NewExpiryUtc;
        license.State = LicenseState.Active;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditTrailService.WriteAsync(
            license.Id,
            null,
            null,
            "admin",
            "license_extended",
            "Срок действия лицензии обновлён.",
            AuditSeverity.Information,
            context,
            new { request.NewExpiryUtc, request.Comment },
            cancellationToken);

        return OperationResult.Success("Срок действия лицензии изменён.");
    }

    public async Task<OperationResult> RevokeLicenseAsync(
        RevokeLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var license = await dbContext.Licenses
            .Include(x => x.Devices)
            .Include(x => x.Sessions)
            .FirstOrDefaultAsync(x => x.Id == request.LicenseId, cancellationToken);

        if (license is null)
        {
            return OperationResult.Failure("Лицензия не найдена.", "license_not_found");
        }

        license.State = LicenseState.Revoked;
        license.RevokedUtc = clock.UtcNow;
        license.RevocationReason = request.Reason;

        foreach (var device in license.Devices)
        {
            device.State = DeviceState.Revoked;
            device.RevokedUtc = clock.UtcNow;
            device.RevocationReason = request.Reason;
        }

        foreach (var session in license.Sessions.Where(x => x.State == SessionState.Active))
        {
            session.State = SessionState.Revoked;
            session.RevokedUtc = clock.UtcNow;
            session.RevocationReason = request.Reason;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditTrailService.WriteAsync(
            license.Id,
            null,
            null,
            "admin",
            "license_revoked",
            "Лицензия отозвана.",
            AuditSeverity.Critical,
            context,
            new { request.Reason },
            cancellationToken);

        return OperationResult.Success("Лицензия отозвана.");
    }

    public async Task<OperationResult> DeleteLicenseAsync(
        DeleteLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var license = await dbContext.Licenses
            .Include(x => x.Devices)
            .Include(x => x.Sessions)
            .Include(x => x.Activations)
            .FirstOrDefaultAsync(x => x.Id == request.LicenseId, cancellationToken);

        if (license is null)
        {
            return OperationResult.Failure("Лицензия не найдена.", "license_not_found");
        }

        var hasUsage =
            license.Devices.Count != 0 ||
            license.Sessions.Count != 0 ||
            license.Activations.Count != 0;

        if (hasUsage)
        {
            return OperationResult.Failure(
                "Лицензию уже использовали. Для таких ключей доступен отзыв, а не удаление.",
                "license_in_use");
        }

        var maskedKey = license.LicenseKeyMasked;
        var customerEmail = license.CustomerEmail;

        dbContext.Licenses.Remove(license);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Deleted unused license {MaskedKey} for {CustomerEmail}. Actor: {Actor}",
            maskedKey,
            customerEmail,
            context.AdminUserName ?? context.IpAddress);

        return OperationResult.Success("Лицензия удалена.");
    }

    public async Task<OperationResult> RevokeDeviceAsync(
        RevokeDeviceRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .Include(x => x.Sessions)
            .FirstOrDefaultAsync(x => x.Id == request.DeviceId, cancellationToken);

        if (device is null)
        {
            return OperationResult.Failure("Устройство не найдено.", "device_not_found");
        }

        device.State = DeviceState.Revoked;
        device.RevokedUtc = clock.UtcNow;
        device.RevocationReason = request.Reason;

        foreach (var session in device.Sessions.Where(x => x.State == SessionState.Active))
        {
            session.State = SessionState.Revoked;
            session.RevokedUtc = clock.UtcNow;
            session.RevocationReason = request.Reason;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditTrailService.WriteAsync(
            device.LicenseId,
            device.Id,
            null,
            "admin",
            "device_revoked",
            "Устройство заблокировано.",
            AuditSeverity.Warning,
            context,
            new { request.Reason },
            cancellationToken);

        return OperationResult.Success("Устройство заблокировано.");
    }

    public async Task<OperationResult> PublishReleaseAsync(
        PublishReleaseRequest request,
        IFormFile package,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (package.Length == 0)
        {
            return OperationResult.Failure("Файл пакета обновления пуст.", "empty_package");
        }

        var extension = Path.GetExtension(package.FileName).ToLowerInvariant();
        if (extension is not ".zip" and not ".msix" and not ".msixbundle")
        {
            return OperationResult.Failure("Поддерживаются пакеты ZIP, MSIX и MSIXBundle.", "unsupported_package");
        }

        var channelCode = VersioningHelper.ToChannelCode(request.Channel);
        var channel = await dbContext.UpdateChannels.FirstOrDefaultAsync(x => x.Code == channelCode, cancellationToken);
        if (channel is null)
        {
            return OperationResult.Failure("Канал обновлений не найден.", "channel_not_found");
        }

        Directory.CreateDirectory(_storage.ReleaseStorageRoot);
        var fileName = $"{request.Version}_{package.FileName}";
        var storagePath = Path.Combine(_storage.ReleaseStorageRoot, fileName);

        await using (var fileStream = File.Create(storagePath))
        {
            await package.CopyToAsync(fileStream, cancellationToken);
        }

        string sha256;
        await using (var stream = File.OpenRead(storagePath))
        {
            sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        }

        var publishedUtc = clock.UtcNow;
        var signature = await updateSignatureService.SignReleaseAsync(
            request.Version,
            channelCode,
            sha256,
            request.Mandatory,
            publishedUtc,
            cancellationToken);

        var release = new ApplicationRelease
        {
            Version = request.Version,
            UpdateChannelDefinitionId = channel.Id,
            State = ReleaseState.Published,
            IsMandatory = request.Mandatory,
            MinimumSupportedVersion = request.MinimumSupportedVersion,
            Summary = request.Summary,
            PublishedUtc = publishedUtc,
            Artifacts =
            [
                new ReleaseArtifact
                {
                    FileName = package.FileName,
                    ContentType = package.ContentType ?? "application/octet-stream",
                    StoragePath = storagePath,
                    SizeBytes = package.Length,
                    Sha256 = sha256,
                    SignatureBase64 = signature.SignatureBase64,
                    SignaturePayload = signature.Payload,
                    SignatureAlgorithm = signature.Algorithm,
                    IsPrimary = true
                }
            ]
        };

        dbContext.ApplicationReleases.Add(release);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Published release {Version} for channel {Channel}", request.Version, channelCode);
        return OperationResult.Success("Релиз опубликован.");
    }

    public async Task<OperationResult> UpsertTranslationQuotaAsync(
        UpsertTranslationQuotaRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var quota = await dbContext.TranslationQuotas.FirstOrDefaultAsync(x => x.LicenseId == request.LicenseId, cancellationToken);
        if (quota is null)
        {
            quota = new TranslationQuota
            {
                LicenseId = request.LicenseId
            };

            dbContext.TranslationQuotas.Add(quota);
        }

        quota.MaxFilesPerJob = request.MaxFilesPerJob;
        quota.MaxSegmentsPerJob = request.MaxSegmentsPerJob;
        quota.MaxCharactersPerJob = request.MaxCharactersPerJob;
        quota.MaxCharactersPerDay = request.MaxCharactersPerDay;
        quota.MaxConcurrentJobs = request.MaxConcurrentJobs;
        quota.MaxJobsPerHour = request.MaxJobsPerHour;
        quota.MaxAnalysisPerHour = request.MaxAnalysisPerHour;
        quota.IsEnabled = request.IsEnabled;
        quota.Notes = request.Notes?.Trim() ?? string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditTrailService.WriteAsync(
            request.LicenseId,
            null,
            null,
            "admin",
            "translation_quota_upserted",
            "Квота перевода обновлена.",
            AuditSeverity.Information,
            context,
            request,
            cancellationToken);

        return OperationResult.Success("Квота перевода сохранена.");
    }

    public async Task<OperationResult> UpsertTranslationGlossaryAsync(
        UpsertTranslationGlossaryRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        TranslationGlossary? glossary = null;
        if (request.Id.HasValue)
        {
            glossary = await dbContext.TranslationGlossaries.FirstOrDefaultAsync(x => x.Id == request.Id.Value, cancellationToken);
        }

        glossary ??= new TranslationGlossary();
        if (!await dbContext.TranslationGlossaries.AnyAsync(x => x.Id == glossary.Id, cancellationToken))
        {
            dbContext.TranslationGlossaries.Add(glossary);
        }

        glossary.LicenseId = request.LicenseId;
        glossary.Name = request.Name.Trim();
        glossary.Scope = request.Scope;
        glossary.SourceLanguage = request.SourceLanguage.Trim();
        glossary.TargetLanguage = request.TargetLanguage.Trim();
        glossary.Priority = request.Priority;
        glossary.IsActive = request.IsActive;
        glossary.Description = request.Description?.Trim() ?? string.Empty;
        glossary.TermsJson = TranslationJson.Serialize(request.Terms);
        glossary.FrozenTermsJson = TranslationJson.Serialize(request.FrozenTerms);
        glossary.SkipTermsJson = TranslationJson.Serialize(request.SkipTerms);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.LicenseId.HasValue)
        {
            await auditTrailService.WriteAsync(
                request.LicenseId.Value,
                null,
                null,
                "admin",
                "translation_glossary_upserted",
                "Glossary перевода обновлён.",
                AuditSeverity.Information,
                context,
                new { request.Name, request.Scope, request.SourceLanguage, request.TargetLanguage },
                cancellationToken);
        }

        return OperationResult.Success("Glossary перевода сохранён.");
    }

    public async Task<OperationResult> SetTranslationProviderStateAsync(
        SetTranslationProviderStateRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var provider = await dbContext.TranslationProviderSettings.FirstOrDefaultAsync(x => x.Id == request.ProviderId, cancellationToken);
        if (provider is null)
        {
            return OperationResult.Failure("Провайдер перевода не найден.", "translation_provider_not_found");
        }

        provider.IsEnabled = request.IsEnabled;
        provider.LastHealthCheckUtc = clock.UtcNow;
        provider.LastKnownStatus = request.IsEnabled ? "enabled" : "disabled";
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success("Состояние провайдера перевода обновлено.");
    }

    public async Task<OperationResult<AdminAuthenticationResult>> ValidateAdminCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var admin = await dbContext.AdminUsers.FirstOrDefaultAsync(x => x.UserName == userName && x.IsActive, cancellationToken);
        if (admin is null)
        {
            return OperationResult<AdminAuthenticationResult>.Failure("Пользователь не найден.", "admin_not_found");
        }

        var verification = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        if (verification is PasswordVerificationResult.Failed)
        {
            return OperationResult<AdminAuthenticationResult>.Failure("Неверные учётные данные.", "invalid_credentials");
        }

        admin.LastLoginUtc = clock.UtcNow;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            admin.PasswordHash = _passwordHasher.HashPassword(admin, password);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<AdminAuthenticationResult>.Success(
            new AdminAuthenticationResult(admin.Id, admin.UserName, admin.DisplayName, admin.Role.ToString()),
            "Аутентификация выполнена.");
    }

    private static string GenerateLicenseKey()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var raw = Convert.ToHexString(bytes);
        return $"SLP-{raw[..4]}-{raw[4..8]}-{raw[8..12]}-{raw[12..16]}";
    }
}
