using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;

namespace Platform.Api.Authentication;

public sealed class OpaqueTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionTokenValidator sessionTokenValidator) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var value = authorizationHeader.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = value["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.Fail("Токен доступа не передан.");
        }

        var session = await sessionTokenValidator.ValidateAccessTokenAsync(token, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("Токен доступа недействителен.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.SessionId.ToString()),
            new("license_id", session.LicenseId.ToString()),
            new("device_id", session.DeviceId.ToString()),
            new("customer_name", session.CustomerName),
            new("license_status", session.LicenseStatus),
            new("client_version", session.ClientVersion)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
