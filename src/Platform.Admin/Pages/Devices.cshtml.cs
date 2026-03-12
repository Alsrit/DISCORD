using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.Admin.Infrastructure;
using Platform.Application.Models;
using Platform.Application.Services;

namespace Platform.Admin.Pages;

[Authorize]
public sealed class DevicesModel(IAdminPlatformService adminPlatformService) : PageModel
{
    public IReadOnlyCollection<DeviceListItemDto> Devices { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Devices = await adminPlatformService.GetDevicesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid deviceId, string reason, CancellationToken cancellationToken)
    {
        var result = await adminPlatformService.RevokeDeviceAsync(
            new RevokeDeviceRequest(deviceId, reason),
            HttpContext.ToRequestContext(),
            cancellationToken);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToPage();
    }
}
