using System.Text.Json;
using Platform.Client.Core.Configuration;

namespace Platform.Client.Core.Services;

public sealed class ClientSettingsStore(ClientPathService pathService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ClientSettings Load()
    {
        pathService.EnsureFolders();
        if (!File.Exists(pathService.SettingsPath))
        {
            var settings = new ClientSettings { InstallationId = Guid.NewGuid().ToString("N") };
            Save(settings);
            return settings;
        }

        var json = File.ReadAllText(pathService.SettingsPath);
        var settingsFromDisk = JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions) ?? new ClientSettings();
        if (string.IsNullOrWhiteSpace(settingsFromDisk.InstallationId))
        {
            settingsFromDisk.InstallationId = Guid.NewGuid().ToString("N");
            Save(settingsFromDisk);
        }

        return settingsFromDisk;
    }

    public void Save(ClientSettings settings)
    {
        pathService.EnsureFolders();
        File.WriteAllText(pathService.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
