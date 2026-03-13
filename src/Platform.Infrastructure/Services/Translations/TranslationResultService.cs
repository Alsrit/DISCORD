using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Translations;

namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationResultService : ITranslationResultService
{
    public Task<IReadOnlyCollection<TranslationFileResultDto>> GetFilesAsync(
        TranslationJob job,
        CancellationToken cancellationToken)
    {
        var files = job.Files.Select(x => new TranslationFileResultDto(
            x.Id,
            x.RelativePath,
            x.SanitizedPath,
            x.HeaderKey,
            x.State.ToString(),
            x.SegmentCount,
            x.CharacterCount,
            TranslationJson.Deserialize(x.WarningJson, Array.Empty<FileWarningDto>())))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<TranslationFileResultDto>>(files);
    }

    public Task<TranslationDownloadInfoDto?> GetDownloadInfoAsync(
        TranslationJob job,
        CancellationToken cancellationToken)
    {
        var artifact = job.Artifacts
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefault(x => x.IsPrimary);

        if (artifact is null)
        {
            return Task.FromResult<TranslationDownloadInfoDto?>(null);
        }

        return Task.FromResult<TranslationDownloadInfoDto?>(new TranslationDownloadInfoDto(
            job.Id,
            artifact.Id,
            artifact.FileName,
            artifact.ContentType,
            artifact.SizeBytes,
            artifact.Sha256,
            artifact.CreatedUtc));
    }

    public Task<(Stream Stream, string FileName, string ContentType)?> OpenDownloadAsync(
        TranslationJob job,
        CancellationToken cancellationToken)
    {
        var artifact = job.Artifacts
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefault(x => x.IsPrimary);

        if (artifact is null || !File.Exists(artifact.StoragePath))
        {
            return Task.FromResult<(Stream Stream, string FileName, string ContentType)?>(null);
        }

        Stream stream = File.OpenRead(artifact.StoragePath);
        (Stream Stream, string FileName, string ContentType)? result = (stream, artifact.FileName, artifact.ContentType);
        return Task.FromResult(result);
    }
}
