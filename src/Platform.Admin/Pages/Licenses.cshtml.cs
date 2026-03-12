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
public sealed class LicensesModel(IAdminPlatformService adminPlatformService) : PageModel
{
    [TempData]
    public string? CreatedLicenseKey { get; set; }

    [BindProperty]
    public CreateLicenseRequest CreateRequest { get; set; } = new(
        string.Empty,
        string.Empty,
        LicenseType.Subscription,
        1,
        72,
        DateTimeOffset.UtcNow.AddMonths(1),
        UpdateChannelCode.Stable,
        string.Empty);

    public IReadOnlyCollection<LicenseListItemDto> Licenses { get; private set; } = [];

    public IReadOnlyCollection<SelectListItem> LicenseTypeOptions { get; } =
    [
        new("Пробная", LicenseType.Trial.ToString()),
        new("Подписка", LicenseType.Subscription.ToString()),
        new("Бессрочная", LicenseType.Perpetual.ToString())
    ];

    public IReadOnlyCollection<SelectListItem> ChannelOptions { get; } =
    [
        new("Стабильный", UpdateChannelCode.Stable.ToString()),
        new("Бета", UpdateChannelCode.Beta.ToString()),
        new("Внутренний", UpdateChannelCode.Internal.ToString())
    ];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Licenses = await adminPlatformService.GetLicensesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.CreateLicenseAsync(
            CreateRequest,
            HttpContext.ToRequestContext(),
            cancellationToken);

        if (result.Succeeded)
        {
            CreatedLicenseKey = result.Data;
            TempData["StatusMessage"] = result.Message;
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExtendAsync(Guid licenseId, DateTimeOffset? newExpiryUtc, CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.ExtendLicenseAsync(
            new ExtendLicenseRequest(licenseId, newExpiryUtc, "Продление из админ-панели"),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid licenseId, string reason, CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.RevokeLicenseAsync(
            new RevokeLicenseRequest(licenseId, reason),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.DeleteLicenseAsync(
            new DeleteLicenseRequest(licenseId),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
