using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Auditing;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services;

public sealed class ClientPlatformService(
    PlatformDbContext dbContext,
    IClock clock,
    ILicenseKeyProtector licenseKeyProtector,
    ITokenService tokenService,
    IRateLimitService rateLimitService,
    IAuditTrailService auditTrailService,
    ISecurityIncidentService securityIncidentService,
    IRedisConnectionAccessor redisConnectionAccessor,
    IOptions<SecurityOptions> securityOptions,
    IOptions<ServerIdentityOptions> serverOptions,
    ILogger<ClientPlatformService> logger) : IClientPlatformService
{
    private readonly SecurityOptions _security = securityOptions.Value;
    private readonly ServerIdentityOptions _server = serverOptions.Value;

    public async Task<OperationResult<ActivationResponse>> ActivateAsync(
        ActivateLicenseRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (!await rateLimitService.ConsumeAsync("activate-ip", context.IpAddress, _security.ActivationLimitPerHour, TimeSpan.FromHours(1), cancellationToken))
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.ActivationRateLimited,
                "Превышен лимит попыток активации по IP.",
                AuditSeverity.Warning,
                context,
                null,
                null,
                null,
                new { request.InstallationId },
                cancellationToken);

            return OperationResult<ActivationResponse>.Failure("Слишком много попыток активации. Попробуйте позже.", "rate_limited");
        }

        var keyHash = licenseKeyProtector.Hash(request.LicenseKey);
        var utcNow = clock.UtcNow;
        var license = await dbContext.Licenses
            .Include(x => x.Devices)
            .Include(x => x.Sessions)
            .FirstOrDefaultAsync(x => x.LicenseKeyHash == keyHash, cancellationToken);

        if (license is null)
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.InvalidLicenseKey,
                "Попытка активации с неизвестным лицензионным ключом.",
                AuditSeverity.Warning,
                context,
                null,
                null,
                null,
                new { request.InstallationId, request.ClientVersion },
                cancellationToken);

            return OperationResult<ActivationResponse>.Failure("Лицензионный ключ не найден.", "license_not_found");
        }

        var fingerprintHash = tokenService.HashToken(request.DeviceFingerprint);
        var activation = new LicenseActivation
        {
            LicenseId = license.Id,
            RequestedInstallationId = request.InstallationId,
            RequestedDeviceFingerprintHash = fingerprintHash,
            RequestedClientVersion = request.ClientVersion,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent
        };

        if (!license.IsUsable(utcNow))
        {
            activation.Success = false;
            activation.FailureReason = "Лицензия недоступна для активации.";
            dbContext.LicenseActivations.Add(activation);
            await dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<ActivationResponse>.Failure($"Лицензия недоступна: {license.GetDisplayState(utcNow)}.", "license_unavailable");
        }

        var device = license.Devices.FirstOrDefault(x => x.Matches(fingerprintHash, request.InstallationId));
        if (device is null)
        {
            var activeDevices = license.Devices.Count(x => x.IsUsable());
            if (activeDevices >= license.MaxDevices)
            {
                activation.Success = false;
                activation.FailureReason = "Превышен лимит устройств.";
                dbContext.LicenseActivations.Add(activation);
                await dbContext.SaveChangesAsync(cancellationToken);

                await securityIncidentService.CaptureAsync(
                    SecurityIncidentType.DeviceCapExceeded,
                    "Попытка активации сверх лимита устройств.",
                    AuditSeverity.Warning,
                    context,
                    license.Id,
                    null,
                    null,
                    new { activeDevices, license.MaxDevices },
                    cancellationToken);

                return OperationResult<ActivationResponse>.Failure("Достигнут лимит устройств по лицензии.", "device_cap_exceeded");
            }

            device = new Device
            {
                LicenseId = license.Id,
                DeviceFingerprintHash = fingerprintHash,
                InstallationId = request.InstallationId,
                DeviceName = request.DeviceName,
                MachineName = request.MachineName,
                OperatingSystem = request.OperatingSystem,
                LastKnownIp = context.IpAddress,
                LastKnownUserAgent = context.UserAgent,
                CurrentClientVersion = request.ClientVersion,
                LastSeenUtc = utcNow,
                FirstSeenUtc = utcNow
            };

            dbContext.Devices.Add(device);
        }
        else if (!device.IsUsable())
        {
            activation.Success = false;
            activation.FailureReason = "Устройство отозвано.";
            activation.DeviceId = device.Id;
            dbContext.LicenseActivations.Add(activation);
            await dbContext.SaveChangesAsync(cancellationToken);

            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.DeviceRevokedAttempt,
                "Попытка активации ранее отозванного устройства.",
                AuditSeverity.Warning,
                context,
                license.Id,
                device.Id,
                null,
                new { request.DeviceName, request.MachineName },
                cancellationToken);

            return OperationResult<ActivationResponse>.Failure("Это устройство заблокировано администратором.", "device_revoked");
        }
        else
        {
            device.DeviceName = request.DeviceName;
            device.MachineName = request.MachineName;
            device.OperatingSystem = request.OperatingSystem;
            device.CurrentClientVersion = request.ClientVersion;
            device.LastKnownIp = context.IpAddress;
            device.LastKnownUserAgent = context.UserAgent;
            device.LastSeenUtc = utcNow;
        }

        var tokenPair = tokenService.CreateTokenPair(
            TimeSpan.FromMinutes(_security.AccessTokenLifetimeMinutes),
            TimeSpan.FromDays(_security.RefreshTokenLifetimeDays));

        var session = new ClientSession
        {
            LicenseId = license.Id,
            Device = device,
            AccessTokenHash = tokenPair.AccessTokenHash,
            RefreshTokenHash = tokenPair.RefreshTokenHash,
            AccessTokenExpiresUtc = tokenPair.AccessTokenExpiresUtc,
            RefreshTokenExpiresUtc = tokenPair.RefreshTokenExpiresUtc,
            CurrentClientVersion = request.ClientVersion,
            LastKnownIp = context.IpAddress,
            LastSeenUtc = utcNow
        };

        dbContext.ClientSessions.Add(session);
        activation.Success = true;
        activation.Device = device;
        dbContext.LicenseActivations.Add(activation);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditTrailService.WriteAsync(
            license.Id,
            device.Id,
            null,
            "license",
            "activation_success",
            "Устройство успешно активировано.",
            AuditSeverity.Information,
            context,
            new { device.DeviceName, device.MachineName, request.ClientVersion },
            cancellationToken);

        logger.LogInformation("Лицензия {LicenseId} активирована на устройстве {DeviceId}", license.Id, device.Id);

        return OperationResult<ActivationResponse>.Success(
            new ActivationResponse(
                new SessionTokensDto(
                    tokenPair.AccessToken,
                    tokenPair.AccessTokenExpiresUtc,
                    tokenPair.RefreshToken,
                    tokenPair.RefreshTokenExpiresUtc),
                BuildLicenseStatus(license, utcNow),
                await GetServerInfoAsync(cancellationToken)),
            "Активация выполнена успешно.");
    }

    public async Task<OperationResult<ActivationResponse>> RefreshAsync(
        RefreshSessionRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if (!await rateLimitService.ConsumeAsync("refresh-ip", context.IpAddress, _security.RefreshLimitPerHour, TimeSpan.FromHours(1), cancellationToken))
        {
            return OperationResult<ActivationResponse>.Failure("Слишком много запросов обновления сессии.", "rate_limited");
        }

        var refreshTokenHash = tokenService.HashToken(request.RefreshToken);
        var utcNow = clock.UtcNow;
        var session = await dbContext.ClientSessions
            .Include(x => x.License)
                .ThenInclude(x => x.Devices)
            .Include(x => x.Device)
            .FirstOrDefaultAsync(x => x.RefreshTokenHash == refreshTokenHash, cancellationToken);

        if (session is null || !session.IsRefreshTokenValid(utcNow))
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.RefreshTokenReplay,
                "Недействительный или просроченный refresh token.",
                AuditSeverity.Warning,
                context,
                session?.LicenseId,
                session?.DeviceId,
                session?.Id,
                new { request.InstallationId },
                cancellationToken);

            return OperationResult<ActivationResponse>.Failure("Сессия устарела. Требуется повторная активация.", "session_expired");
        }

        var fingerprintHash = tokenService.HashToken(request.DeviceFingerprint);
        if (!session.Device.Matches(fingerprintHash, request.InstallationId))
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.SessionRejected,
                "Refresh token предъявлен с другого устройства.",
                AuditSeverity.Critical,
                context,
                session.LicenseId,
                session.DeviceId,
                session.Id,
                new { request.InstallationId },
                cancellationToken);

            return OperationResult<ActivationResponse>.Failure("Параметры устройства не совпадают с активированной сессией.", "device_mismatch");
        }

        if (!session.Device.IsUsable() || !session.License.IsUsable(utcNow))
        {
            return OperationResult<ActivationResponse>.Failure("Лицензия или устройство больше не доступны.", "license_unavailable");
        }

        var tokenPair = tokenService.CreateTokenPair(
            TimeSpan.FromMinutes(_security.AccessTokenLifetimeMinutes),
            TimeSpan.FromDays(_security.RefreshTokenLifetimeDays));

        session.AccessTokenHash = tokenPair.AccessTokenHash;
        session.AccessTokenExpiresUtc = tokenPair.AccessTokenExpiresUtc;
        session.RefreshTokenHash = tokenPair.RefreshTokenHash;
        session.RefreshTokenExpiresUtc = tokenPair.RefreshTokenExpiresUtc;
        session.LastKnownIp = context.IpAddress;
        session.CurrentClientVersion = request.ClientVersion;
        session.LastSeenUtc = utcNow;

        session.Device.LastSeenUtc = utcNow;
        session.Device.CurrentClientVersion = request.ClientVersion;
        session.Device.LastKnownIp = context.IpAddress;

        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ActivationResponse>.Success(
            new ActivationResponse(
                new SessionTokensDto(
                    tokenPair.AccessToken,
                    tokenPair.AccessTokenExpiresUtc,
                    tokenPair.RefreshToken,
                    tokenPair.RefreshTokenExpiresUtc),
                BuildLicenseStatus(session.License, utcNow),
                await GetServerInfoAsync(cancellationToken)),
            "Сессия обновлена.");
    }

    public async Task<OperationResult<LicenseSyncResponse>> HeartbeatAsync(
        Guid sessionId,
        HeartbeatRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<LicenseSyncResponse>.Failure("Сессия не найдена.", "session_not_found");
        }

        session.LastSeenUtc = clock.UtcNow;
        session.LastKnownIp = context.IpAddress;
        session.CurrentClientVersion = request.ClientVersion;
        session.Device.LastSeenUtc = session.LastSeenUtc;
        session.Device.OperatingSystem = request.OperatingSystem;
        session.Device.CurrentClientVersion = request.ClientVersion;
        session.Device.LastKnownIp = context.IpAddress;
        session.Device.LastKnownUserAgent = context.UserAgent;

        await dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<LicenseSyncResponse>.Success(
            new LicenseSyncResponse(
                BuildLicenseStatus(session.License, clock.UtcNow),
                await GetServerInfoAsync(cancellationToken),
                clock.UtcNow,
                session.License.IsUsable(clock.UtcNow) && session.Device.IsUsable()),
            "Состояние синхронизировано.");
    }

    public async Task<OperationResult<LicenseSyncResponse>> GetStatusAsync(
        Guid sessionId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<LicenseSyncResponse>.Failure("Сессия не найдена.", "session_not_found");
        }

        return OperationResult<LicenseSyncResponse>.Success(
            new LicenseSyncResponse(
                BuildLicenseStatus(session.License, clock.UtcNow),
                await GetServerInfoAsync(cancellationToken),
                session.LastSeenUtc,
                session.License.IsUsable(clock.UtcNow) && session.Device.IsUsable()),
            "Статус получен.");
    }

    public async Task<OperationResult> RecordTelemetryAsync(
        Guid sessionId,
        TelemetryBatchRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var allowed = await rateLimitService.ConsumeAsync("telemetry", sessionId.ToString("N"), _security.TelemetryLimitPerMinute, TimeSpan.FromMinutes(1), cancellationToken);
        if (!allowed)
        {
            return OperationResult.Failure("Превышен лимит телеметрии.", "rate_limited");
        }

        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult.Failure("Сессия не найдена.", "session_not_found");
        }

        foreach (var item in request.Events.Take(_security.TelemetryLimitPerMinute))
        {
            dbContext.TelemetryEvents.Add(new TelemetryEvent
            {
                LicenseId = session.LicenseId,
                DeviceId = session.DeviceId,
                SessionId = session.Id,
                EventType = item.EventType,
                Message = item.Message,
                Severity = ParseSeverity(item.Severity),
                PayloadJson = item.PayloadJson,
                ClientVersion = session.CurrentClientVersion,
                OccurredUtc = item.OccurredUtc,
                ReceivedUtc = clock.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success("Телеметрия сохранена.");
    }

    public async Task<OperationResult<UpdateCheckResponse>> CheckUpdatesAsync(
        Guid sessionId,
        UpdateCheckRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<UpdateCheckResponse>.Failure("Сессия не найдена.", "session_not_found");
        }

        var requestedChannel = request.PreferredChannel <= session.License.UpdateChannel
            ? request.PreferredChannel
            : session.License.UpdateChannel;

        var channelCode = VersioningHelper.ToChannelCode(requestedChannel);
        var release = await dbContext.ApplicationReleases
            .Include(x => x.UpdateChannelDefinition)
            .Include(x => x.Artifacts)
            .Where(x => x.State == ReleaseState.Published && x.UpdateChannelDefinition.Code == channelCode)
            .OrderByDescending(x => x.PublishedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (release is null || !VersioningHelper.IsGreater(release.Version, request.CurrentVersion))
        {
            return OperationResult<UpdateCheckResponse>.Success(
                new UpdateCheckResponse(false, request.CurrentVersion, null, "Обновлений не найдено."),
                "Актуальная версия уже установлена.");
        }

        var artifact = release.Artifacts.FirstOrDefault(x => x.IsPrimary);
        if (artifact is null)
        {
            return OperationResult<UpdateCheckResponse>.Failure("Для релиза отсутствует артефакт.", "artifact_not_found");
        }

        var package = new UpdatePackageDto(
            release.Id,
            release.Version,
            release.IsMandatory,
            release.Summary,
            release.UpdateChannelDefinition.Code,
            $"/api/client/v1/updates/download/{release.Id}",
            artifact.Sha256,
            artifact.SignatureBase64,
            artifact.SignaturePayload,
            artifact.SignatureAlgorithm,
            artifact.SizeBytes,
            release.PublishedUtc ?? release.CreatedUtc);

        return OperationResult<UpdateCheckResponse>.Success(
            new UpdateCheckResponse(true, request.CurrentVersion, package, "Доступно обновление."),
            "Найдено обновление.");
    }

    public async Task<OperationResult<(Stream Stream, string FileName, string ContentType)>> OpenReleaseArtifactAsync(
        Guid sessionId,
        Guid releaseId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<(Stream Stream, string FileName, string ContentType)>.Failure("Сессия не найдена.", "session_not_found");
        }

        var release = await dbContext.ApplicationReleases
            .Include(x => x.Artifacts)
            .Include(x => x.UpdateChannelDefinition)
            .FirstOrDefaultAsync(x => x.Id == releaseId, cancellationToken);

        if (release is null || release.State != ReleaseState.Published)
        {
            return OperationResult<(Stream Stream, string FileName, string ContentType)>.Failure("Релиз не найден.", "release_not_found");
        }

        if (VersioningHelper.ToChannelCode(session.License.UpdateChannel) == "stable" && release.UpdateChannelDefinition.Code != "stable")
        {
            return OperationResult<(Stream Stream, string FileName, string ContentType)>.Failure("Этот канал обновлений недоступен для вашей лицензии.", "channel_denied");
        }

        var artifact = release.Artifacts.FirstOrDefault(x => x.IsPrimary);
        if (artifact is null || !File.Exists(artifact.StoragePath))
        {
            return OperationResult<(Stream Stream, string FileName, string ContentType)>.Failure("Файл обновления недоступен.", "artifact_not_found");
        }

        var stream = File.OpenRead(artifact.StoragePath);
        return OperationResult<(Stream Stream, string FileName, string ContentType)>.Success((stream, artifact.FileName, artifact.ContentType));
    }

    public async Task<ServerInfoDto> GetServerInfoAsync(CancellationToken cancellationToken)
    {
        var dbReachable = await dbContext.Database.CanConnectAsync(cancellationToken);
        var redisReachable = await redisConnectionAccessor.PingAsync(cancellationToken);

        return new ServerInfoDto(
            _server.ServerName,
            _server.EnvironmentDisplayName,
            typeof(ClientPlatformService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            clock.UtcNow,
            dbReachable,
            redisReachable);
    }

    private async Task<ClientSession?> LoadAuthorizedSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await dbContext.ClientSessions
            .Include(x => x.License)
                .ThenInclude(x => x.Devices)
            .Include(x => x.Device)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.State == SessionState.Active, cancellationToken);

    private LicenseStatusDto BuildLicenseStatus(License license, DateTimeOffset utcNow) =>
        new(
            license.Id,
            license.LicenseKeyMasked,
            license.CustomerName,
            license.CustomerEmail,
            license.GetDisplayState(utcNow),
            license.Type,
            license.ExpiresUtc,
            license.MaxDevices,
            license.Devices.Count(x => x.IsUsable()),
            license.OfflineGracePeriodHours,
            license.UpdateChannel,
            utcNow,
            license.IsUsable(utcNow)
                ? "Лицензия действительна и синхронизирована."
                : "Лицензия требует внимания.");

    private static TelemetrySeverity ParseSeverity(string severity) =>
        severity.Trim().ToLowerInvariant() switch
        {
            "warning" => TelemetrySeverity.Warning,
            "error" => TelemetrySeverity.Error,
            "critical" => TelemetrySeverity.Critical,
            _ => TelemetrySeverity.Information
        };
}
