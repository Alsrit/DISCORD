using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Infrastructure.Configuration;

namespace Platform.Infrastructure.Services.Translations;

public sealed class ModAnalysisService(
    ITranslationPathSanitizer pathSanitizer,
    IOptions<TranslationOptions> options) : IModAnalysisService
{
    private static readonly Regex HeaderRegex = new(@"^\s*(?<header>l_[A-Za-z0-9_]+)\s*:\s*$", RegexOptions.Compiled);
    private static readonly Regex EntryRegex = new(
        @"^(?<prefix>\s*(?<key>[^#:\s][^:]*):(?<version>\d+)?\s*"")(?<text>(?:\\.|[^""])*)(""(?<suffix>\s*(#.*)?))$",
        RegexOptions.Compiled);

    private readonly TranslationOptions _options = options.Value;

    public async Task<ModAnalysisResult> AnalyzeAsync(
        AnalyzeModRequest request,
        CancellationToken cancellationToken)
    {
        var payloadBytes = request.Files.Sum(x => Encoding.UTF8.GetByteCount(x.Content));
        if (payloadBytes > _options.MaxPayloadBytes)
        {
            throw new TranslationRequestValidationException("Размер payload задания перевода превышает разрешённый лимит.", "payload_too_large");
        }

        if (request.Files.Count > _options.MaxFilesPerRequest)
        {
            throw new TranslationRequestValidationException("Количество файлов превышает разрешённый лимит.", "too_many_files");
        }

        var files = new List<ParsedLocalizationFile>(request.Files.Count);
        foreach (var file in request.Files)
        {
            files.Add(await ParseFileAsync(file, cancellationToken));
        }

        var segmentCount = files.Sum(x => x.Entries.Count);
        if (segmentCount > _options.MaxSegmentsPerRequest)
        {
            throw new TranslationRequestValidationException("Количество сегментов превышает разрешённый лимит.", "too_many_segments");
        }

        var charCount = files.Sum(x => x.Entries.Sum(entry => entry.SourceText.Length));
        if (charCount > _options.MaxCharactersPerRequest)
        {
            throw new TranslationRequestValidationException("Количество символов превышает разрешённый лимит.", "too_many_characters");
        }

        var warnings = files.SelectMany(x => x.Warnings).ToArray();
        var payloadHash = ComputePayloadHash(files);

        return new ModAnalysisResult(payloadHash, files.Count, segmentCount, charCount, warnings, files);
    }

    public Task<ParsedLocalizationFile> ParseFileAsync(
        UploadedLocalizationFileDto file,
        CancellationToken cancellationToken)
    {
        var sanitizedPath = pathSanitizer.SanitizeRelativePath(file.RelativePath);
        var lines = file.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var warnings = new List<FileWarningDto>();
        var entries = new List<ParsedLocalizationEntry>();
        string headerKey = string.Empty;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var headerMatch = HeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                headerKey = headerMatch.Groups["header"].Value;
                continue;
            }

            var entryMatch = EntryRegex.Match(line);
            if (!entryMatch.Success)
            {
                continue;
            }

            entries.Add(new ParsedLocalizationEntry(
                index + 1,
                entryMatch.Groups["key"].Value.Trim(),
                entryMatch.Groups["prefix"].Value,
                entryMatch.Groups["text"].Value,
                entryMatch.Groups["suffix"].Value));
        }

        if (string.IsNullOrWhiteSpace(headerKey))
        {
            headerKey = $"l_{NormalizeLanguageHeader(file.SourceLanguage)}";
            warnings.Add(new FileWarningDto("missing_header", "В localisation-файле не найден language header. Будет использован заголовок по языку источника."));
        }

        if (entries.Count == 0)
        {
            warnings.Add(new FileWarningDto("no_entries", "Файл не содержит пригодных для перевода localisation-строк."));
        }

        var computedSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file.Content)));
        if (!string.Equals(computedSha, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new TranslationRequestValidationException("Контрольная сумма localisation-файла не совпадает с заявленной.", "checksum_mismatch");
        }

        return Task.FromResult(new ParsedLocalizationFile(
            file.RelativePath,
            sanitizedPath,
            headerKey,
            file.SourceLanguage,
            computedSha,
            file.SizeBytes,
            file.Content.Replace("\r\n", "\n", StringComparison.Ordinal),
            warnings,
            entries));
    }

    private static string NormalizeLanguageHeader(string sourceLanguage) =>
        sourceLanguage.ToLowerInvariant() switch
        {
            "en" => "english",
            "ru" => "russian",
            "de" => "german",
            "fr" => "french",
            "es" => "spanish",
            _ => sourceLanguage.ToLowerInvariant()
        };

    private static string ComputePayloadHash(IReadOnlyCollection<ParsedLocalizationFile> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files.OrderBy(x => x.SanitizedPath, StringComparer.Ordinal))
        {
            builder.Append(file.SanitizedPath)
                .Append('|')
                .Append(file.OriginalSha256)
                .Append('|')
                .Append(file.Entries.Count)
                .Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
