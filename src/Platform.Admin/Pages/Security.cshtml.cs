using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Platform.Application.Models;
using Platform.Application.Services;

namespace Platform.Admin.Pages;

[Authorize]
public sealed class SecurityModel(IAdminPlatformService adminPlatformService) : PageModel
{
    public IReadOnlyCollection<SecurityIncidentDto> Incidents { get; private set; } = [];

    public IReadOnlyCollection<AuditEventDto> AuditEvents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Incidents = await adminPlatformService.GetSecurityIncidentsAsync(cancellationToken);
        AuditEvents = await adminPlatformService.GetAuditEventsAsync(cancellationToken);
    }
}
