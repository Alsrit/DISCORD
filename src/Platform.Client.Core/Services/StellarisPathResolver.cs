using Platform.Client.Core.Configuration;

namespace Platform.Client.Core.Services;

public sealed class StellarisPathResolver(ClientSettingsStore settingsStore)
{
    private const string StellarisWorkshopId = "281990";

    public string GetUserDataRoot()
    {
        var settings = settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.StellarisUserDataPath) && Directory.Exists(settings.StellarisUserDataPath))
        {
            return Path.GetFullPath(settings.StellarisUserDataPath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive",
            "Stellaris");
    }

    public string GetModsDirectory() => Path.Combine(GetUserDataRoot(), "mod");

    public string GetSubmodOutputRoot()
    {
        var settings = settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.SubmodOutputRoot))
        {
            return Path.GetFullPath(settings.SubmodOutputRoot);
        }

        return GetModsDirectory();
    }

    public IReadOnlyCollection<string> GetWorkshopContentRoots()
    {
        var settings = settingsStore.Load();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var libraryRoot in GetSteamLibraryRoots(settings))
        {
            var workshopRoot = Path.Combine(libraryRoot, "steamapps", "workshop", "content", StellarisWorkshopId);
            if (Directory.Exists(workshopRoot))
            {
                roots.Add(Path.GetFullPath(workshopRoot));
            }
        }

        return roots.ToArray();
    }

    private static IReadOnlyCollection<string> GetSteamLibraryRoots(ClientSettings settings)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidateSteamRoot = !string.IsNullOrWhiteSpace(settings.SteamRootPath)
            ? settings.SteamRootPath
            : DetectSteamRoot();

        if (!string.IsNullOrWhiteSpace(candidateSteamRoot) && Directory.Exists(candidateSteamRoot))
        {
            roots.Add(Path.GetFullPath(candidateSteamRoot));
            foreach (var library in ParseLibraryFolders(candidateSteamRoot))
            {
                roots.Add(library);
            }
        }

        return roots.ToArray();
    }

    private static string DetectSteamRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    private static IReadOnlyCollection<string> ParseLibraryFolders(string steamRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var libraryFoldersPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersPath))
        {
            return result.ToArray();
        }

        foreach (var rawLine in File.ReadLines(libraryFoldersPath))
        {
            var line = rawLine.Trim();
            if (!line.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var quotes = line.Split('"', StringSplitOptions.RemoveEmptyEntries);
            var pathValue = quotes
                .SkipWhile(x => !string.Equals(x, "path", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(pathValue))
            {
                continue;
            }

            var normalized = pathValue.Replace(@"\\", @"\");
            if (Directory.Exists(normalized))
            {
                result.Add(Path.GetFullPath(normalized));
            }
        }

        return result.ToArray();
    }
}
