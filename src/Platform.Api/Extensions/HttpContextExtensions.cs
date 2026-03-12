using System.Security.Claims;
using Platform.Application.Abstractions;

namespace Platform.Api.Extensions;

public static class HttpContextExtensions
{
    public static RequestContext ToRequestContext(this HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var correlationId = context.TraceIdentifier;
        var admin = context.User.Identity?.Name;

        return new RequestContext(ipAddress, userAgent, correlationId, admin);
    }

    public static Guid GetSessionId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
    }
}
