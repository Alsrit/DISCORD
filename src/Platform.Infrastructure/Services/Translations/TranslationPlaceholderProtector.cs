using System.Text.RegularExpressions;
using Platform.Application.Abstractions;
using Platform.Application.Services;

namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationPlaceholderProtector : ITranslationPlaceholderProtector
{
    private static readonly Regex[] BuiltInPatterns =
    [
        new(@"\$[^$\r\n]+\$", RegexOptions.Compiled),
        new(@"£[A-Za-z0-9_]+£", RegexOptions.Compiled),
        new(@"\[[^\]\r\n]+\]", RegexOptions.Compiled),
        new(@"§.", RegexOptions.Compiled),
        new(@"%\d*\$?[sdif]", RegexOptions.Compiled),
        new(@"\\n", RegexOptions.Compiled),
        new(@"\{[0-9]+\}", RegexOptions.Compiled)
    ];

    public ProtectedTextResult Protect(string text, EffectiveGlossary glossary)
    {
        var working = text;
        var index = 0;
        var placeholderMap = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var term in glossary.FrozenTerms.Concat(glossary.SkipTerms).Distinct(StringComparer.Ordinal).OrderByDescending(x => x.Length))
        {
            if (string.IsNullOrWhiteSpace(term) || !working.Contains(term, StringComparison.Ordinal))
            {
                continue;
            }

            var placeholder = CreatePlaceholder(index++);
            placeholderMap[placeholder] = term;
            working = working.Replace(term, placeholder, StringComparison.Ordinal);
        }

        foreach (var regex in BuiltInPatterns)
        {
            working = regex.Replace(working, match =>
            {
                var existing = placeholderMap.FirstOrDefault(x => x.Value == match.Value);
                if (!string.IsNullOrEmpty(existing.Key))
                {
                    return existing.Key;
                }

                var placeholder = CreatePlaceholder(index++);
                placeholderMap[placeholder] = match.Value;
                return placeholder;
            });
        }

        return new ProtectedTextResult(working, placeholderMap);
    }

    public OperationResult<string> Restore(string translatedText, IReadOnlyDictionary<string, string> placeholderMap)
    {
        var working = translatedText;
        foreach (var pair in placeholderMap.OrderByDescending(x => x.Key.Length))
        {
            if (!working.Contains(pair.Key, StringComparison.Ordinal))
            {
                return OperationResult<string>.Failure(
                    $"Защитный токен {pair.Key} отсутствует в ответе провайдера.",
                    "placeholder_missing");
            }

            working = working.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        }

        if (working.Contains("__SLP_", StringComparison.Ordinal))
        {
            return OperationResult<string>.Failure(
                "После восстановления в переводе остались служебные токены.",
                "placeholder_restore_failed");
        }

        return OperationResult<string>.Success(working);
    }

    private static string CreatePlaceholder(int index) => $"__SLP_{index:0000}__";
}
