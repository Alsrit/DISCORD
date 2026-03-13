using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Admin.Infrastructure;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Common;

namespace Platform.Admin.Pages;

[Authorize]
public sealed class TranslationsModel(IAdminPlatformService adminPlatformService) : PageModel
{
    [BindProperty]
    public QuotaEditorInput QuotaEditor { get; set; } = new();

    [BindProperty]
    public GlossaryEditorInput GlossaryEditor { get; set; } = new();

    [BindProperty]
    public ProviderToggleInput ProviderToggle { get; set; } = new();

    [FromQuery]
    public TranslationJobState? State { get; set; }

    public PagedResultDto<TranslationJobAdminListItemDto> Jobs { get; private set; } = new([], 1, 20, 0);

    public IReadOnlyCollection<TranslationUsageAdminDto> Usage { get; private set; } = [];

    public IReadOnlyCollection<TranslationQuotaAdminDto> Quotas { get; private set; } = [];

    public IReadOnlyCollection<TranslationGlossaryAdminDto> Glossaries { get; private set; } = [];

    public IReadOnlyCollection<TranslationProviderAdminDto> Providers { get; private set; } = [];

    public TranslationQueueStatusDto QueueStatus { get; private set; } = new("translation-jobs", 0, 0, 0, 0, DateTimeOffset.UtcNow);

    public IReadOnlyCollection<SelectListItem> LicenseOptions { get; private set; } = [];

    public IReadOnlyCollection<SelectListItem> StateOptions { get; } =
    [
        new("Все", string.Empty),
        new("Queued", TranslationJobState.Queued.ToString()),
        new("Processing", TranslationJobState.Processing.ToString()),
        new("Completed", TranslationJobState.Completed.ToString()),
        new("Failed", TranslationJobState.Failed.ToString()),
        new("CancelRequested", TranslationJobState.CancelRequested.ToString())
    ];

    public IReadOnlyCollection<SelectListItem> GlossaryScopeOptions { get; } =
    [
        new("Системный", TranslationGlossaryScope.System.ToString()),
        new("Игровой (Stellaris)", TranslationGlossaryScope.Game.ToString()),
        new("Лицензионный", TranslationGlossaryScope.License.ToString())
    ];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveQuotaAsync(CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.UpsertTranslationQuotaAsync(
            new UpsertTranslationQuotaRequest(
                QuotaEditor.LicenseId,
                QuotaEditor.MaxFilesPerJob,
                QuotaEditor.MaxSegmentsPerJob,
                QuotaEditor.MaxCharactersPerJob,
                QuotaEditor.MaxCharactersPerDay,
                QuotaEditor.MaxConcurrentJobs,
                QuotaEditor.MaxJobsPerHour,
                QuotaEditor.MaxAnalysisPerHour,
                QuotaEditor.IsEnabled,
                QuotaEditor.Notes ?? string.Empty),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage(new { State });
    }

    public async Task<IActionResult> OnPostSaveGlossaryAsync(CancellationToken cancellationToken)
    {
        var terms = ParseTerms(GlossaryEditor.TermsText);
        var frozenTerms = ParseLines(GlossaryEditor.FrozenTermsText);
        var skipTerms = ParseLines(GlossaryEditor.SkipTermsText);

        var result = await adminPlatformService.UpsertTranslationGlossaryAsync(
            new UpsertTranslationGlossaryRequest(
                GlossaryEditor.Id,
                GlossaryEditor.Scope == TranslationGlossaryScope.License ? GlossaryEditor.LicenseId : null,
                GlossaryEditor.Name ?? string.Empty,
                GlossaryEditor.Scope,
                GlossaryEditor.SourceLanguage ?? "en",
                GlossaryEditor.TargetLanguage ?? "ru",
                GlossaryEditor.Priority,
                GlossaryEditor.IsActive,
                GlossaryEditor.Description ?? string.Empty,
                terms,
                frozenTerms,
                skipTerms),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage(new { State });
    }

    public async Task<IActionResult> OnPostToggleProviderAsync(CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.SetTranslationProviderStateAsync(
            new SetTranslationProviderStateRequest(ProviderToggle.ProviderId, ProviderToggle.IsEnabled),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage(new { State });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Jobs = await adminPlatformService.GetTranslationJobsAsync(new TranslationJobListQuery(1, 20, State), cancellationToken);
        Usage = await adminPlatformService.GetTranslationUsageAsync(cancellationToken);
        Quotas = await adminPlatformService.GetTranslationQuotasAsync(cancellationToken);
        Glossaries = await adminPlatformService.GetTranslationGlossariesAsync(cancellationToken);
        Providers = await adminPlatformService.GetTranslationProvidersAsync(cancellationToken);
        QueueStatus = await adminPlatformService.GetTranslationQueueStatusAsync(cancellationToken);

        var licenses = await adminPlatformService.GetLicensesAsync(cancellationToken);
        LicenseOptions = licenses
            .Select(x => new SelectListItem($"{x.CustomerName} ({x.MaskedKey})", x.Id.ToString()))
            .ToArray();
    }

    private static IReadOnlyCollection<TranslationGlossaryTermDto> ParseTerms(string? text)
    {
        var result = new List<TranslationGlossaryTermDto>();
        foreach (var line in ParseLines(text))
        {
            var parts = line.Split("=>", 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
            {
                result.Add(new TranslationGlossaryTermDto(parts[0], parts[1]));
            }
        }

        return result;
    }

    private static IReadOnlyCollection<string> ParseLines(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public sealed class QuotaEditorInput
    {
        public Guid LicenseId { get; set; }
        public int MaxFilesPerJob { get; set; } = 64;
        public int MaxSegmentsPerJob { get; set; } = 4000;
        public int MaxCharactersPerJob { get; set; } = 120000;
        public int MaxCharactersPerDay { get; set; } = 480000;
        public int MaxConcurrentJobs { get; set; } = 2;
        public int MaxJobsPerHour { get; set; } = 10;
        public int MaxAnalysisPerHour { get; set; } = 20;
        public bool IsEnabled { get; set; } = true;
        public string? Notes { get; set; }
    }

    public sealed class GlossaryEditorInput
    {
        public Guid? Id { get; set; }
        public Guid? LicenseId { get; set; }
        public string? Name { get; set; }
        public TranslationGlossaryScope Scope { get; set; } = TranslationGlossaryScope.Game;
        public string? SourceLanguage { get; set; } = "en";
        public string? TargetLanguage { get; set; } = "ru";
        public int Priority { get; set; } = 100;
        public bool IsActive { get; set; } = true;
        public string? Description { get; set; }
        public string? TermsText { get; set; }
        public string? FrozenTermsText { get; set; }
        public string? SkipTermsText { get; set; }
    }

    public sealed class ProviderToggleInput
    {
        public Guid ProviderId { get; set; }
        public bool IsEnabled { get; set; }
    }
}
