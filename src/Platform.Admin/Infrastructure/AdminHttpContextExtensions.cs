using Platform.Application.Abstractions;

namespace Platform.Admin.Infrastructure;

public static class AdminHttpContextExtensions
{
    public static RequestContext ToRequestContext(this HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        return new RequestContext(ipAddress, userAgent, context.TraceIdentifier, context.User.Identity?.Name);
    }
}
