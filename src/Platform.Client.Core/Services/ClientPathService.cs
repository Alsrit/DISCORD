namespace Platform.Client.Core.Services;

public sealed class ClientPathService(string? rootPath = null)
{
    public string RootPath { get; } = string.IsNullOrWhiteSpace(rootPath)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureLicensePlatform")
        : Path.GetFullPath(rootPath);

    public string SettingsPath => Path.Combine(RootPath, "clientsettings.json");

    public string TokensPath => Path.Combine(RootPath, "session.dat");

    public string LogsPath => Path.Combine(RootPath, "logs.jsonl");

    public string UpdatesPath => Path.Combine(RootPath, "updates");

    public string TranslationRootPath => Path.Combine(RootPath, "translations");

    public string TranslationDownloadsPath => Path.Combine(TranslationRootPath, "downloads");

    public string SubmodsWorkspacePath => Path.Combine(RootPath, "submods");

    public string SubmodsStagingPath => Path.Combine(SubmodsWorkspacePath, "staging");

    public string SubmodsBackupsPath => Path.Combine(SubmodsWorkspacePath, "backups");

    public void EnsureFolders()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(UpdatesPath);
        Directory.CreateDirectory(TranslationRootPath);
        Directory.CreateDirectory(TranslationDownloadsPath);
        Directory.CreateDirectory(SubmodsWorkspacePath);
        Directory.CreateDirectory(SubmodsStagingPath);
        Directory.CreateDirectory(SubmodsBackupsPath);
    }
}
