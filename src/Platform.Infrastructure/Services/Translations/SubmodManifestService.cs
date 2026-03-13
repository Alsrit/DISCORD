using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Translations;

namespace Platform.Infrastructure.Services.Translations;

public sealed class SubmodManifestService : ISubmodManifestService
{
    public SubmodManifestPreviewDto BuildPreview(
        TranslationJob job,
        IReadOnlyCollection<TranslationFile> files)
    {
        var submodName = string.IsNullOrWhiteSpace(job.RequestedSubmodName)
            ? $"[RU] {job.ModName} (Auto Translation)"
            : job.RequestedSubmodName.Trim();

        var outputFiles = files
            .Select(x => x.SanitizedPath.Replace('\\', '/'))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new SubmodManifestPreviewDto(
            submodName,
            "descriptor.mod",
            job.TargetLanguage,
            outputFiles,
            [
                "Оригинальный мод не изменяется.",
                "В архив включаются только переведённые localisation-файлы.",
                "Клиент может использовать этот manifest для безопасной сборки отдельного submod."
            ]);
    }
}
