using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class StellarisModDiscoveryService(
    StellarisPathResolver pathResolver,
    StellarisDescriptorParser descriptorParser,
    StellarisLocalizationParser localizationParser)
{
    public Task<IReadOnlyCollection<StellarisModDescriptor>> DiscoverAsync(CancellationToken cancellationToken) =>
        Task.Run(() => DiscoverInternal(cancellationToken), cancellationToken);

    private IReadOnlyCollection<StellarisModDescriptor> DiscoverInternal(CancellationToken cancellationToken)
    {
        var mods = new Dictionary<string, StellarisModDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var localMod in DiscoverLocalMods(cancellationToken))
        {
            mods[localMod.RootPath] = localMod;
        }

        foreach (var workshopMod in DiscoverWorkshopMods(cancellationToken))
        {
            mods[workshopMod.RootPath] = workshopMod;
        }

        return mods.Values
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.SourceKind)
            .ToArray();
    }

    private IEnumerable<StellarisModDescriptor> DiscoverLocalMods(CancellationToken cancellationToken)
    {
        var modsDirectory = pathResolver.GetModsDirectory();
        if (!Directory.Exists(modsDirectory))
        {
            yield break;
        }

        foreach (var descriptorPath in Directory.EnumerateFiles(modsDirectory, "*.mod", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFileName(descriptorPath), "descriptor.mod", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StellarisDescriptorParser.ParsedDescriptor descriptor;
            try
            {
                descriptor = descriptorParser.Parse(descriptorPath, StellarisModSourceKind.Local, "Локальный мод");
            }
            catch
            {
                continue;
            }

            if (!Directory.Exists(descriptor.RootPath))
            {
                continue;
            }

            yield return BuildDescriptor(descriptor, cancellationToken);
        }
    }

    private IEnumerable<StellarisModDescriptor> DiscoverWorkshopMods(CancellationToken cancellationToken)
    {
        foreach (var workshopRoot in pathResolver.GetWorkshopContentRoots())
        {
            foreach (var modDirectory in Directory.EnumerateDirectories(workshopRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var descriptorPath = Path.Combine(modDirectory, "descriptor.mod");
                if (!File.Exists(descriptorPath))
                {
                    continue;
                }

                StellarisDescriptorParser.ParsedDescriptor descriptor;
                try
                {
                    descriptor = descriptorParser.Parse(
                        descriptorPath,
                        StellarisModSourceKind.Workshop,
                        "Steam Workshop",
                        modDirectory);
                }
                catch
                {
                    continue;
                }

                yield return BuildDescriptor(descriptor, cancellationToken);
            }
        }
    }

    private StellarisModDescriptor BuildDescriptor(StellarisDescriptorParser.ParsedDescriptor descriptor, CancellationToken cancellationToken)
    {
        var localizationFiles = new List<StellarisLocalizationFile>();
        var localizationRoot = Path.Combine(descriptor.RootPath, "localisation");
        if (Directory.Exists(localizationRoot))
        {
            foreach (var file in Directory.EnumerateFiles(localizationRoot, "*.yml", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    localizationFiles.Add(localizationParser.Parse(descriptor.RootPath, file));
                }
                catch
                {
                    // Skip malformed files during discovery; the server-side analyzer still validates the upload payload.
                }
            }
        }

        return new StellarisModDescriptor(
            descriptor.Name,
            descriptor.RootPath,
            descriptor.DescriptorPath,
            descriptor.OriginalReference,
            descriptor.SourceKind,
            descriptor.SourceLabel,
            descriptor.Version,
            descriptor.SupportedVersion,
            descriptor.RemoteFileId,
            descriptor.Tags,
            localizationFiles.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
