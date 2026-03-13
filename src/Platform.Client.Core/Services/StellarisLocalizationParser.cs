using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class StellarisLocalizationParser
{
    private static readonly Regex HeaderRegex = new(@"^\s*(?<header>l_[A-Za-z0-9_]+)\s*:\s*$", RegexOptions.Compiled);
    private static readonly Regex EntryRegex = new(@"^\s*(?<key>[^#\s][^:]*?)\s*:\d+\s+""(?<value>(?:[^""\\]|\\.)*)""", RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> LanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["english"] = "en",
        ["russian"] = "ru",
        ["german"] = "de",
        ["french"] = "fr",
        ["spanish"] = "es",
        ["polish"] = "pl",
        ["braz_por"] = "pt-BR"
    };

    public StellarisLocalizationFile Parse(string modRootPath, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var relativePath = Path.GetRelativePath(modRootPath, fullPath).Replace('\\', '/');
        var warnings = new List<string>();
        var entries = new List<StellarisLocalizationEntry>();

        var encoding = DetectEncoding(fullPath);
        var lines = File.ReadAllLines(fullPath, encoding);

        string headerKey = string.Empty;
        foreach (var rawLine in lines)
        {
            var headerMatch = HeaderRegex.Match(rawLine);
            if (headerMatch.Success)
            {
                headerKey = headerMatch.Groups["header"].Value;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(headerKey))
        {
            warnings.Add("В файле не найден корректный language header.");
            headerKey = "l_english";
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var match = EntryRegex.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            entries.Add(new StellarisLocalizationEntry(
                match.Groups["key"].Value.Trim(),
                match.Groups["value"].Value,
                index + 1));
        }

        if (entries.Count == 0)
        {
            warnings.Add("Файл не содержит переводимых строк Stellaris localisation.");
        }

        var headerLanguage = headerKey.StartsWith("l_", StringComparison.OrdinalIgnoreCase)
            ? headerKey[2..]
            : "english";
        var sourceLanguage = LanguageMap.TryGetValue(headerLanguage, out var mapped)
            ? mapped
            : headerLanguage;

        var bytes = File.ReadAllBytes(fullPath);
        var sha = Convert.ToHexString(SHA256.HashData(bytes));

        return new StellarisLocalizationFile(
            relativePath,
            fullPath,
            headerKey,
            sourceLanguage,
            entries.Count,
            entries.Sum(x => x.SourceText.Length),
            bytes.LongLength,
            sha,
            warnings,
            entries);
    }

    private static Encoding DetectEncoding(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        reader.Peek();
        return reader.CurrentEncoding;
    }
}
