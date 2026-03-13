using System.Text.RegularExpressions;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class StellarisDescriptorParser
{
    private static readonly Regex KeyValueRegex = new(@"^(?<key>[A-Za-z0-9_]+)\s*=\s*(?<value>.+)$", RegexOptions.Compiled);

    public ParsedDescriptor Parse(string descriptorPath, StellarisModSourceKind sourceKind, string sourceLabel, string? explicitRootPath = null)
    {
        var lines = File.ReadAllLines(descriptorPath);
        string name = Path.GetFileNameWithoutExtension(descriptorPath);
        string? version = null;
        string? supportedVersion = null;
        string? remoteFileId = null;
        string? pathValue = null;
        var tags = new List<string>();
        var insideTags = false;

        foreach (var rawLine in lines)
        {
            var line = StripComment(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (insideTags)
            {
                if (line.StartsWith("}", StringComparison.Ordinal))
                {
                    insideTags = false;
                    continue;
                }

                var tag = ParseScalar(line);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    tags.Add(tag);
                }

                continue;
            }

            if (line.StartsWith("tags", StringComparison.OrdinalIgnoreCase))
            {
                insideTags = true;
                continue;
            }

            var match = KeyValueRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups["key"].Value;
            var value = ParseScalar(match.Groups["value"].Value);

            switch (key)
            {
                case "name":
                    name = value;
                    break;
                case "version":
                    version = value;
                    break;
                case "supported_version":
                    supportedVersion = value;
                    break;
                case "path":
                    pathValue = value;
                    break;
                case "remote_file_id":
                    remoteFileId = value;
                    break;
            }
        }

        var resolvedRoot = ResolveRootPath(descriptorPath, explicitRootPath, pathValue);
        var originalReference = !string.IsNullOrWhiteSpace(remoteFileId)
            ? $"steam:{remoteFileId}"
            : resolvedRoot;

        return new ParsedDescriptor(
            name,
            resolvedRoot,
            descriptorPath,
            originalReference,
            sourceKind,
            sourceLabel,
            version,
            supportedVersion,
            remoteFileId,
            tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string ResolveRootPath(string descriptorPath, string? explicitRootPath, string? pathValue)
    {
        if (!string.IsNullOrWhiteSpace(explicitRootPath))
        {
            return Path.GetFullPath(explicitRootPath);
        }

        var descriptorDirectory = Path.GetDirectoryName(descriptorPath) ?? AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return Path.GetFullPath(descriptorDirectory);
        }

        if (Path.IsPathRooted(pathValue))
        {
            return Path.GetFullPath(pathValue);
        }

        if ((pathValue.StartsWith("mod/", StringComparison.OrdinalIgnoreCase) ||
             pathValue.StartsWith(@"mod\", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(Path.GetFileName(descriptorDirectory), "mod", StringComparison.OrdinalIgnoreCase))
        {
            var userDataRoot = Directory.GetParent(descriptorDirectory)?.FullName ?? descriptorDirectory;
            return Path.GetFullPath(Path.Combine(userDataRoot, pathValue));
        }

        return Path.GetFullPath(Path.Combine(descriptorDirectory, pathValue));
    }

    private static string StripComment(string value)
    {
        var commentIndex = value.IndexOf('#');
        return commentIndex >= 0 ? value[..commentIndex] : value;
    }

    private static string ParseScalar(string raw)
    {
        var trimmed = raw.Trim().TrimEnd('{').Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    public sealed record ParsedDescriptor(
        string Name,
        string RootPath,
        string DescriptorPath,
        string OriginalReference,
        StellarisModSourceKind SourceKind,
        string SourceLabel,
        string? Version,
        string? SupportedVersion,
        string? RemoteFileId,
        IReadOnlyCollection<string> Tags);
}
