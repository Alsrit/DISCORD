namespace Platform.Client.Core.Services;

public sealed class ClientPathService
{
    public string RootPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecureLicensePlatform");

    public string SettingsPath => Path.Combine(RootPath, "clientsettings.json");

    public string TokensPath => Path.Combine(RootPath, "session.dat");

    public string LogsPath => Path.Combine(RootPath, "logs.jsonl");

    public string UpdatesPath => Path.Combine(RootPath, "updates");

    public void EnsureFolders()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(UpdatesPath);
    }
}
