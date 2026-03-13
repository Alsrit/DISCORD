using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Platform.Application.Services;
using Platform.Domain.Translations;
using Platform.Infrastructure.Configuration;

namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationPackagingService(
    ITranslationPathSanitizer pathSanitizer,
    ISubmodManifestService submodManifestService,
    IOptions<StorageOptions> storageOptions) : ITranslationPackagingService
{
    private readonly StorageOptions _storage = storageOptions.Value;

    public async Task<PackagedTranslationResult> PackageAsync(
        TranslationJob job,
        IReadOnlyCollection<TranslationFile> files,
        CancellationToken cancellationToken)
    {
        var manifest = submodManifestService.BuildPreview(job, files);
        var manifestJson = TranslationJson.Serialize(manifest);

        Directory.CreateDirectory(_storage.TranslationStorageRoot);
        Directory.CreateDirectory(_storage.TranslationTempRoot);

        var tempPath = Path.Combine(_storage.TranslationTempRoot, $"{job.Id:N}.zip");
        var finalRelativePath = $"{job.Id:N}.zip";
        var finalPath = pathSanitizer.GetSafeStoragePath(_storage.TranslationStorageRoot, finalRelativePath);

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var fileStream = File.Create(tempPath))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var file in files.OrderBy(x => x.SanitizedPath, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(file.SanitizedPath.Replace('\\', '/'), CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                await writer.WriteAsync(file.TranslatedContent.AsMemory(), cancellationToken);
            }

            var manifestEntry = archive.CreateEntry("translation-submod-manifest.json", CompressionLevel.Optimal);
            await using (var entryStream = manifestEntry.Open())
            await using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(manifestJson.AsMemory(), cancellationToken);
            }
        }

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(tempPath, finalPath);
        await using var readStream = File.OpenRead(finalPath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(readStream, cancellationToken));
        var fileInfo = new FileInfo(finalPath);

        return new PackagedTranslationResult(
            Path.GetFileName(finalPath),
            finalPath,
            "application/zip",
            fileInfo.Length,
            hash,
            manifestJson);
    }
}
