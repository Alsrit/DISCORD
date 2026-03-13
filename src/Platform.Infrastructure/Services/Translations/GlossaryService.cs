using Microsoft.EntityFrameworkCore;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Common;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services.Translations;

public sealed class GlossaryService(PlatformDbContext dbContext) : IGlossaryService
{
    public async Task<EffectiveGlossary> GetEffectiveGlossaryAsync(
        Guid licenseId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TranslationGlossaries
            .Where(x =>
                x.IsActive &&
                x.SourceLanguage == sourceLanguage &&
                x.TargetLanguage == targetLanguage &&
                (x.Scope != TranslationGlossaryScope.License || x.LicenseId == licenseId))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Scope)
            .ToListAsync(cancellationToken);

        var terms = new Dictionary<string, string>(StringComparer.Ordinal);
        var frozen = new HashSet<string>(StringComparer.Ordinal);
        var skip = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var term in TranslationJson.Deserialize(row.TermsJson, Array.Empty<TranslationGlossaryTermDto>()))
            {
                if (!string.IsNullOrWhiteSpace(term.Source))
                {
                    terms[term.Source] = term.Target;
                }
            }

            foreach (var item in TranslationJson.Deserialize(row.FrozenTermsJson, Array.Empty<string>()))
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    frozen.Add(item);
                }
            }

            foreach (var item in TranslationJson.Deserialize(row.SkipTermsJson, Array.Empty<string>()))
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    skip.Add(item);
                }
            }
        }

        return new EffectiveGlossary(terms, frozen.ToArray(), skip.ToArray());
    }

    public async Task<IReadOnlyCollection<ActiveGlossaryDto>> GetActiveGlossariesAsync(
        Guid licenseId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.TranslationGlossaries
            .Where(x => x.IsActive && (x.Scope != TranslationGlossaryScope.License || x.LicenseId == licenseId))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            var terms = TranslationJson.Deserialize(x.TermsJson, Array.Empty<TranslationGlossaryTermDto>());
            var frozen = TranslationJson.Deserialize(x.FrozenTermsJson, Array.Empty<string>());
            var skip = TranslationJson.Deserialize(x.SkipTermsJson, Array.Empty<string>());

            return new ActiveGlossaryDto(
                x.Id,
                x.Name,
                x.Scope.ToString(),
                x.SourceLanguage,
                x.TargetLanguage,
                terms.Length,
                frozen.Length,
                skip.Length,
                x.Description);
        }).ToArray();
    }
}
