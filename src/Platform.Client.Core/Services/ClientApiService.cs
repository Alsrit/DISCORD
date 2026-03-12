using System.Net.Http.Json;
using Platform.Application.Models;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class ClientApiService(
    ClientSettingsStore settingsStore,
    SecureTokenStore tokenStore,
    DeviceIdentityService deviceIdentityService,
    ClientLogService logService,
    ClientPathService pathService,
    PinnedHttpClientFactory httpClientFactory)
{
    public StoredSessionBundle? GetCurrentSession() => tokenStore.Load();

    public void ClearSession() => tokenStore.Clear();

    public async Task<ActivationResponse?> ActivateAsync(string licenseKey, string clientVersion, CancellationToken cancellationToken)
    {
        var settings = settingsStore.Load();
        var profile = deviceIdentityService.GetCurrentProfile();
        using var client = httpClientFactory.Create(settings);

        var response = await client.PostAsJsonAsync(
            "api/client/v1/activate",
            new ActivateLicenseRequest(
                licenseKey,
                profile.InstallationId,
                profile.DeviceFingerprint,
                profile.DeviceName,
                profile.MachineName,
                profile.OperatingSystem,
                clientVersion,
                settings.PreferredChannel),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logService.Write("Error", "Активация", $"Ошибка активации: {response.StatusCode}");
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<ActivationResponse>(cancellationToken: cancellationToken);
        if (payload is not null)
        {
            SaveTokens(payload);
            logService.Write("Information", "Активация", "Лицензия успешно активирована.", new { payload.License.CustomerEmail });
        }

        return payload;
    }

    public async Task<bool> EnsureAuthorizedAsync(string clientVersion, CancellationToken cancellationToken)
    {
        var session = tokenStore.Load();
        if (session is null)
        {
            return false;
        }

        if (session.AccessTokenExpiresUtc > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return true;
        }

        return await RefreshAsync(clientVersion, cancellationToken) is not null;
    }

    public async Task<ActivationResponse?> RefreshAsync(string clientVersion, CancellationToken cancellationToken)
    {
        var session = tokenStore.Load();
        if (session is null)
        {
            return null;
        }

        var settings = settingsStore.Load();
        var profile = deviceIdentityService.GetCurrentProfile();
        using var client = httpClientFactory.Create(settings);

        var response = await client.PostAsJsonAsync(
            "api/client/v1/refresh",
            new RefreshSessionRequest(
                session.RefreshToken,
                profile.InstallationId,
                profile.DeviceFingerprint,
                clientVersion),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logService.Write("Warning", "Сессия", "Не удалось обновить access token.");
            tokenStore.Clear();
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<ActivationResponse>(cancellationToken: cancellationToken);
        if (payload is not null)
        {
            SaveTokens(payload);
        }

        return payload;
    }

    public async Task<LicenseSyncResponse?> GetStatusAsync(string clientVersion, CancellationToken cancellationToken)
    {
        if (!await EnsureAuthorizedAsync(clientVersion, cancellationToken))
        {
            return null;
        }

        var settings = settingsStore.Load();
        var session = tokenStore.Load();
        if (session is null)
        {
            return null;
        }

        using var client = httpClientFactory.Create(settings, session.AccessToken);
        var response = await client.GetAsync("api/client/v1/license/status", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<LicenseSyncResponse>(cancellationToken: cancellationToken);
        if (payload is not null)
        {
            session.LastKnownLicense = payload.License;
            session.LastValidatedUtc = payload.LastSynchronizedUtc;
            tokenStore.Save(session);
        }

        return payload;
    }

    public async Task<LicenseSyncResponse?> HeartbeatAsync(string clientVersion, string? lastError, CancellationToken cancellationToken)
    {
        if (!await EnsureAuthorizedAsync(clientVersion, cancellationToken))
        {
            return null;
        }

        var settings = settingsStore.Load();
        var session = tokenStore.Load();
        var profile = deviceIdentityService.GetCurrentProfile();
        if (session is null)
        {
            return null;
        }

        using var client = httpClientFactory.Create(settings, session.AccessToken);
        var response = await client.PostAsJsonAsync(
            "api/client/v1/heartbeat",
            new HeartbeatRequest(clientVersion, profile.OperatingSystem, true, lastError),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<LicenseSyncResponse>(cancellationToken: cancellationToken);
        if (payload is not null)
        {
            session.LastKnownLicense = payload.License;
            session.LastValidatedUtc = payload.LastSynchronizedUtc;
            tokenStore.Save(session);
        }

        return payload;
    }

    public async Task<ServerInfoDto?> GetServerInfoAsync(CancellationToken cancellationToken)
    {
        var settings = settingsStore.Load();
        using var client = httpClientFactory.Create(settings);
        var response = await client.GetAsync("api/client/v1/system/info", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ServerInfoDto>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<UpdateCheckResponse?> CheckUpdatesAsync(string clientVersion, CancellationToken cancellationToken)
    {
        if (!await EnsureAuthorizedAsync(clientVersion, cancellationToken))
        {
            return null;
        }

        var settings = settingsStore.Load();
        var session = tokenStore.Load();
        if (session is null)
        {
            return null;
        }

        using var client = httpClientFactory.Create(settings, session.AccessToken);
        var response = await client.GetAsync(
            $"api/client/v1/updates/check?currentVersion={Uri.EscapeDataString(clientVersion)}&preferredChannel={settings.PreferredChannel}",
            cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UpdateCheckResponse>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<string?> DownloadUpdateAsync(string clientVersion, UpdatePackageDto package, CancellationToken cancellationToken)
    {
        if (!await EnsureAuthorizedAsync(clientVersion, cancellationToken))
        {
            return null;
        }

        var settings = settingsStore.Load();
        var session = tokenStore.Load();
        if (session is null)
        {
            return null;
        }

        pathService.EnsureFolders();
        var destinationPath = Path.Combine(pathService.UpdatesPath, package.Version + "_" + package.ReleaseId + Path.GetExtension(package.DownloadUrl) switch
        {
            { Length: > 0 } extension => extension,
            _ => ".bin"
        });

        using var client = httpClientFactory.Create(settings, session.AccessToken);
        await using var source = await client.GetStreamAsync(package.DownloadUrl.TrimStart('/'), cancellationToken);
        await using var target = File.Create(destinationPath);
        await source.CopyToAsync(target, cancellationToken);

        return destinationPath;
    }

    public async Task<bool> SendTelemetryAsync(IEnumerable<ClientLogEntry> items, string clientVersion, CancellationToken cancellationToken)
    {
        if (!await EnsureAuthorizedAsync(clientVersion, cancellationToken))
        {
            return false;
        }

        var settings = settingsStore.Load();
        var session = tokenStore.Load();
        if (session is null)
        {
            return false;
        }

        using var client = httpClientFactory.Create(settings, session.AccessToken);
        var payload = new TelemetryBatchRequest(items.Select(item =>
            new ClientTelemetryItem(
                item.Category,
                item.Message,
                item.Level,
                item.Timestamp,
                item.PayloadJson)).ToArray());

        var response = await client.PostAsJsonAsync("api/client/v1/telemetry", payload, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private void SaveTokens(ActivationResponse payload)
    {
        tokenStore.Save(new StoredSessionBundle
        {
            AccessToken = payload.Tokens.AccessToken,
            AccessTokenExpiresUtc = payload.Tokens.AccessTokenExpiresUtc,
            RefreshToken = payload.Tokens.RefreshToken,
            RefreshTokenExpiresUtc = payload.Tokens.RefreshTokenExpiresUtc,
            LastValidatedUtc = DateTimeOffset.UtcNow,
            LastKnownLicense = payload.License
        });
    }
}
