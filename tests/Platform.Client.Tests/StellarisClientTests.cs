using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Platform.Application.Models;
using Platform.Client.Core.Configuration;
using Platform.Client.Core.Services;
using Xunit;

namespace Platform.Client.Tests;

public sealed class StellarisClientTests
{
    [Fact]
    public void DescriptorParser_ParsesLocalDescriptor()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var userModRoot = Path.Combine(tempRoot, "Stellaris", "mod");
            Directory.CreateDirectory(Path.Combine(userModRoot, "my_mod"));
            var descriptorPath = Path.Combine(userModRoot, "my_mod.mod");
            File.WriteAllText(
                descriptorPath,
                """
                name="My Translation Target"
                path="mod/my_mod"
                supported_version="3.14.*"
                tags={
                  "Total Conversion"
                  "Lore"
                }
                """,
                Encoding.UTF8);

            var parser = new StellarisDescriptorParser();
            var descriptor = parser.Parse(descriptorPath, Platform.Client.Core.Models.StellarisModSourceKind.Local, "Локальный мод");

            Assert.Equal("My Translation Target", descriptor.Name);
            Assert.EndsWith(Path.Combine("Stellaris", "mod", "my_mod"), descriptor.RootPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("3.14.*", descriptor.SupportedVersion);
            Assert.Contains("Lore", descriptor.Tags);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LocalizationParser_ExtractsHeaderAndEntries()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var modRoot = Path.Combine(tempRoot, "mod_root");
            var filePath = Path.Combine(modRoot, "localisation", "english", "test_l_english.yml");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(
                filePath,
                """
                l_english:
                 test_key:0 "Star Empire"
                 second_key:0 "Naval Capacity"
                """,
                new UTF8Encoding(false));

            var parser = new StellarisLocalizationParser();
            var file = parser.Parse(modRoot, filePath);

            Assert.Equal("l_english", file.HeaderKey);
            Assert.Equal("en", file.SourceLanguage);
            Assert.Equal(2, file.SegmentCount);
            Assert.Equal("test_key", file.Entries.First().LocalizationKey);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SubmodBuildService_DryRunDoesNotWriteFiles()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var paths = CreateClientPathServices(tempRoot, out var settingsStore);
            settingsStore.Save(new ClientSettings
            {
                InstallationId = Guid.NewGuid().ToString("N"),
                StellarisUserDataPath = Path.Combine(tempRoot, "Stellaris"),
                SubmodOutputRoot = Path.Combine(tempRoot, "Stellaris", "mod")
            });

            var modRoot = Path.Combine(tempRoot, "source-mod");
            Directory.CreateDirectory(modRoot);
            var sourceMod = new Platform.Client.Core.Models.StellarisModDescriptor(
                "Source Mod",
                modRoot,
                Path.Combine(modRoot, "descriptor.mod"),
                "mod/source",
                Platform.Client.Core.Models.StellarisModSourceKind.Local,
                "Локальный мод",
                "1.0.0",
                "3.14.*",
                null,
                Array.Empty<string>(),
                Array.Empty<Platform.Client.Core.Models.StellarisLocalizationFile>());

            var packagePath = CreateResultPackage(tempRoot, "Dry Run Mod");
            var builder = new SubmodBuildService(paths, new StellarisPathResolver(settingsStore));
            var result = await builder.BuildFromPackageAsync(sourceMod, packagePath, "[RU] Dry Run Mod", dryRun: true, createBackup: true, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(Directory.Exists(result.OutputFolderPath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SubmodBuildService_WritesSeparateSubmodAndBackup()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var paths = CreateClientPathServices(tempRoot, out var settingsStore);
            var modsOutputRoot = Path.Combine(tempRoot, "Stellaris", "mod");
            settingsStore.Save(new ClientSettings
            {
                InstallationId = Guid.NewGuid().ToString("N"),
                StellarisUserDataPath = Path.Combine(tempRoot, "Stellaris"),
                SubmodOutputRoot = modsOutputRoot
            });

            var modRoot = Path.Combine(tempRoot, "source-mod");
            Directory.CreateDirectory(modRoot);
            var sourceMod = new Platform.Client.Core.Models.StellarisModDescriptor(
                "Source Mod",
                modRoot,
                Path.Combine(modRoot, "descriptor.mod"),
                "mod/source",
                Platform.Client.Core.Models.StellarisModSourceKind.Local,
                "Локальный мод",
                "1.0.0",
                "3.14.*",
                null,
                Array.Empty<string>(),
                Array.Empty<Platform.Client.Core.Models.StellarisLocalizationFile>());

            var packagePath = CreateResultPackage(tempRoot, "Built Mod");
            var builder = new SubmodBuildService(paths, new StellarisPathResolver(settingsStore));

            var first = await builder.BuildFromPackageAsync(sourceMod, packagePath, "[RU] Built Mod", dryRun: false, createBackup: true, CancellationToken.None);
            Assert.True(first.Succeeded);
            Assert.True(File.Exists(Path.Combine(first.OutputFolderPath, "localisation", "russian", "result_l_russian.yml")));
            Assert.True(File.Exists(first.ExternalDescriptorPath));

            var second = await builder.BuildFromPackageAsync(sourceMod, packagePath, "[RU] Built Mod", dryRun: false, createBackup: true, CancellationToken.None);
            Assert.True(second.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(second.BackupPath));
            Assert.True(Directory.Exists(second.BackupPath!));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static ClientPathService CreateClientPathServices(string tempRoot, out ClientSettingsStore settingsStore)
    {
        var pathService = new ClientPathService(tempRoot);
        settingsStore = new ClientSettingsStore(pathService);
        return pathService;
    }

    private static string CreateResultPackage(string tempRoot, string submodName)
    {
        var packagePath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        var fileEntry = archive.CreateEntry("localisation/russian/result_l_russian.yml");
        using (var writer = new StreamWriter(fileEntry.Open(), new UTF8Encoding(false)))
        {
            writer.Write(
                """
                l_russian:
                 test_key:0 "Звёздная империя"
                """);
        }

        var manifestEntry = archive.CreateEntry("translation-submod-manifest.json");
        using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
        {
            writer.Write(JsonSerializer.Serialize(new SubmodManifestPreviewDto(
                $"[RU] {submodName}",
                "descriptor.mod",
                "ru",
                new[] { "localisation/russian/result_l_russian.yml" },
                new[] { "Оригинальный мод не изменяется." })));
        }

        return packagePath;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "secure-platform-client-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
