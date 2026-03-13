using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Platform.Application.Models;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class SubmodBuildService(
    ClientPathService pathService,
    StellarisPathResolver pathResolver)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public SubmodBuildPreview CreatePreview(
        StellarisModDescriptor sourceMod,
        SubmodManifestPreviewDto manifest,
        string requestedSubmodName,
        bool dryRun,
        bool createBackup)
    {
        var submodName = string.IsNullOrWhiteSpace(requestedSubmodName)
            ? $"[RU] {sourceMod.Name} (Auto Translation)"
            : requestedSubmodName.Trim();

        var outputRoot = pathResolver.GetSubmodOutputRoot();
        var folderName = SanitizeFileName(submodName);
        var outputFolder = Path.Combine(outputRoot, folderName);
        var descriptorPath = Path.Combine(outputFolder, "descriptor.mod");
        var externalDescriptorPath = Path.Combine(outputRoot, folderName + ".mod");
        var manifestPath = Path.Combine(outputFolder, "secure-license-platform.manifest.json");

        return new SubmodBuildPreview(
            submodName,
            outputRoot,
            outputFolder,
            descriptorPath,
            externalDescriptorPath,
            manifestPath,
            Directory.Exists(outputFolder),
            createBackup,
            dryRun,
            manifest.OutputFiles,
            manifest.Notes);
    }

    public async Task<SubmodBuildResult> BuildFromPackageAsync(
        StellarisModDescriptor sourceMod,
        string packagePath,
        string requestedSubmodName,
        bool dryRun,
        bool createBackup,
        CancellationToken cancellationToken)
    {
        pathService.EnsureFolders();
        Directory.CreateDirectory(pathService.SubmodsStagingPath);
        Directory.CreateDirectory(pathService.SubmodsBackupsPath);

        await using var packageStream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        var manifestEntry = archive.GetEntry("translation-submod-manifest.json");
        if (manifestEntry is null)
        {
            return new SubmodBuildResult(false, "В архиве результата отсутствует translation-submod-manifest.json.", string.Empty, string.Empty, string.Empty, string.Empty, null);
        }

        SubmodManifestPreviewDto manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = (await JsonSerializer.DeserializeAsync<SubmodManifestPreviewDto>(manifestStream, JsonOptions, cancellationToken))
                ?? new SubmodManifestPreviewDto("[RU] Mod", "descriptor.mod", "ru", Array.Empty<string>(), Array.Empty<string>());
        }

        var preview = CreatePreview(sourceMod, manifest, requestedSubmodName, dryRun, createBackup);
        if (dryRun)
        {
            return new SubmodBuildResult(
                true,
                "Проверка записи завершена. Изменения на диск не записывались.",
                preview.OutputFolderPath,
                preview.DescriptorPath,
                preview.ExternalDescriptorPath,
                preview.ManifestPath,
                null);
        }

        Directory.CreateDirectory(preview.OutputRootPath);

        var stagingPath = Path.Combine(pathService.SubmodsStagingPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingPath);

        try
        {
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.FullName) ||
                    entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                    string.Equals(entry.FullName, "translation-submod-manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var safeRelativePath = GetSafeRelativePath(entry.FullName);
                if (string.IsNullOrWhiteSpace(safeRelativePath))
                {
                    continue;
                }

                var destinationPath = Path.Combine(stagingPath, safeRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? stagingPath);
                await using var source = entry.Open();
                await using var target = File.Create(destinationPath);
                await source.CopyToAsync(target, cancellationToken);
            }

            await File.WriteAllTextAsync(
                Path.Combine(stagingPath, "descriptor.mod"),
                BuildDescriptorContent(preview.SubmodName, sourceMod, preview.OutputFolderPath),
                new UTF8Encoding(false),
                cancellationToken);

            var localManifest = new
            {
                generatedUtc = DateTimeOffset.UtcNow,
                sourceMod = new
                {
                    sourceMod.Name,
                    sourceMod.RootPath,
                    sourceMod.DescriptorPath,
                    sourceMod.OriginalReference,
                    sourceMod.Version,
                    sourceMod.SupportedVersion,
                    sourceMod.SourceKind
                },
                packagePath,
                preview.SubmodName,
                preview.OutputFiles,
                preview.Notes
            };
            await File.WriteAllTextAsync(
                Path.Combine(stagingPath, "secure-license-platform.manifest.json"),
                JsonSerializer.Serialize(localManifest, JsonOptions),
                new UTF8Encoding(false),
                cancellationToken);

            string? backupPath = null;
            if (Directory.Exists(preview.OutputFolderPath))
            {
                if (createBackup)
                {
                    backupPath = Path.Combine(pathService.SubmodsBackupsPath, $"{Path.GetFileName(preview.OutputFolderPath)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
                    Directory.Move(preview.OutputFolderPath, backupPath);
                    if (File.Exists(preview.ExternalDescriptorPath))
                    {
                        Directory.CreateDirectory(backupPath);
                        File.Move(preview.ExternalDescriptorPath, Path.Combine(backupPath, Path.GetFileName(preview.ExternalDescriptorPath)), overwrite: true);
                    }
                }
                else
                {
                    Directory.Delete(preview.OutputFolderPath, recursive: true);
                    if (File.Exists(preview.ExternalDescriptorPath))
                    {
                        File.Delete(preview.ExternalDescriptorPath);
                    }
                }
            }

            Directory.Move(stagingPath, preview.OutputFolderPath);
            await File.WriteAllTextAsync(
                preview.ExternalDescriptorPath,
                BuildDescriptorContent(preview.SubmodName, sourceMod, preview.OutputFolderPath),
                new UTF8Encoding(false),
                cancellationToken);

            return new SubmodBuildResult(
                true,
                "Сабмод успешно создан в отдельной папке.",
                preview.OutputFolderPath,
                preview.DescriptorPath,
                preview.ExternalDescriptorPath,
                preview.ManifestPath,
                backupPath);
        }
        finally
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }
        }
    }

    private string BuildDescriptorContent(string submodName, StellarisModDescriptor sourceMod, string outputFolderPath)
    {
        var supportedVersion = string.IsNullOrWhiteSpace(sourceMod.SupportedVersion) ? "*" : sourceMod.SupportedVersion;
        var relativePath = GetDescriptorPathValue(outputFolderPath);
        var tagLines = new[]
        {
            "tags={",
            "  \"Translation\"",
            "  \"Auto Translation\"",
            "}"
        };

        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"name=\"{submodName}\"",
                $"path=\"{relativePath}\"",
                $"supported_version=\"{supportedVersion}\""
            }.Concat(tagLines)) + Environment.NewLine;
    }

    private string GetDescriptorPathValue(string outputFolderPath)
    {
        var modsRoot = pathResolver.GetModsDirectory();
        if (outputFolderPath.StartsWith(modsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return "mod/" + Path.GetRelativePath(modsRoot, outputFolderPath).Replace('\\', '/');
        }

        return outputFolderPath.Replace('\\', '/');
    }

    private static string GetSafeRelativePath(string archivePath)
    {
        var normalized = archivePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            return string.Empty;
        }

        return Path.Combine(segments);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "stellaris_translation_submod" : sanitized;
    }
}
