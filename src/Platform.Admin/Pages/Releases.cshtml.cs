using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Admin.Infrastructure;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Common;

namespace Platform.Admin.Pages;

[Authorize]
public sealed class ReleasesModel(IAdminPlatformService adminPlatformService) : PageModel
{
    [BindProperty]
    public PublishReleaseRequest PublishRequest { get; set; } = new(
        string.Empty,
        UpdateChannelCode.Stable,
        false,
        "1.0.0",
        string.Empty);

    [BindProperty]
    public IFormFile Package { get; set; } = default!;

    public IReadOnlyCollection<ReleaseListItemDto> Releases { get; private set; } = [];

    public IReadOnlyCollection<SelectListItem> ChannelOptions { get; } =
    [
        new("Стабильный", UpdateChannelCode.Stable.ToString()),
        new("Бета", UpdateChannelCode.Beta.ToString()),
        new("Внутренний", UpdateChannelCode.Internal.ToString())
    ];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Releases = await adminPlatformService.GetReleasesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.PublishReleaseAsync(PublishRequest, Package, HttpContext.ToRequestContext(), cancellationToken);
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
