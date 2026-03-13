using System.Text.RegularExpressions;

namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationPathSanitizer : ITranslationPathSanitizer
{
    private static readonly Regex DuplicateSlashRegex = new("/+", RegexOptions.Compiled);

    public string SanitizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new TranslationRequestValidationException("Путь к localisation-файлу пуст.", "invalid_path");
        }

        var normalized = path.Trim().Replace('\\', '/');
        normalized = DuplicateSlashRegex.Replace(normalized, "/");
        normalized = normalized.TrimStart('/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (Path.IsPathRooted(normalized) ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new TranslationRequestValidationException("Обнаружен небезопасный путь в составе мода.", "path_traversal_detected");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new TranslationRequestValidationException("Путь к localisation-файлу пуст.", "invalid_path");
        }

        foreach (var segment in segments)
        {
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new TranslationRequestValidationException("Путь к localisation-файлу содержит недопустимые символы.", "invalid_path");
            }
        }

        if (!normalized.StartsWith("localisation/", StringComparison.OrdinalIgnoreCase))
        {
            throw new TranslationRequestValidationException("Допускаются только файлы из каталога localisation.", "unsupported_path_scope");
        }

        return normalized;
    }

    public string GetSafeStoragePath(string rootPath, string relativePath)
    {
        Directory.CreateDirectory(rootPath);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(rootPath);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, normalized));

        if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new TranslationRequestValidationException("Путь выходит за пределы разрешённого каталога.", "path_traversal_detected");
        }

        return combined;
    }
}
