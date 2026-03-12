using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.Application.Models;
using Platform.Application.Services;

namespace Platform.Admin.Pages;

[Authorize]
public sealed class TelemetryModel(IAdminPlatformService adminPlatformService) : PageModel
{
    public IReadOnlyCollection<TelemetryListItemDto> Telemetry { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Telemetry = await adminPlatformService.GetTelemetryAsync(cancellationToken);
    }
}
