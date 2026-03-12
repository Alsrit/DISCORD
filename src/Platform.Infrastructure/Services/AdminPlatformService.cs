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
using Platform.Domain.Updates;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services;

public sealed class AdminPlatformService(
    PlatformDbContext dbContext,
    ILicenseKeyProtector licenseKeyProtector,
    IUpdateSignatureService updateSignatureService,
    IAuditTrailService auditTrailService,
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
            "Удалена неиспользованная лицензия {MaskedKey} для {CustomerEmail}. Инициатор: {Actor}",
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

        logger.LogInformation("Опубликован релиз {Version} для канала {Channel}", request.Version, channelCode);
        return OperationResult.Success("Релиз опубликован.");
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
