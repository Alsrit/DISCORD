using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.Application.Models;
using Platform.Application.Services;

namespace Platform.Admin.Pages;

[Authorize]
public sealed class IndexModel(IAdminPlatformService adminPlatformService) : PageModel
{
    public DashboardSummaryDto Summary { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Summary = await adminPlatformService.GetDashboardAsync(cancellationToken);
    }
}
