using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Domain.Common;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services;

public sealed class SessionTokenValidator(
    PlatformDbContext dbContext,
    ITokenService tokenService,
    IClock clock) : ISessionTokenValidator
{
    public async Task<AuthenticatedSessionContext?> ValidateAccessTokenAsync(string rawAccessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawAccessToken))
        {
            return null;
        }

        var hash = tokenService.HashToken(rawAccessToken);
        var session = await dbContext.ClientSessions
            .Include(x => x.License)
            .Include(x => x.Device)
            .FirstOrDefaultAsync(x => x.AccessTokenHash == hash, cancellationToken);

        if (session is null || !session.IsAccessTokenValid(clock.UtcNow))
        {
            return null;
        }

        if (!session.Device.IsUsable() || !session.License.IsUsable(clock.UtcNow))
        {
            session.State = SessionState.Revoked;
            session.RevokedUtc = clock.UtcNow;
            session.RevocationReason = "Сессия отозвана из-за состояния устройства или лицензии.";
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        return new AuthenticatedSessionContext(
            session.Id,
            session.LicenseId,
            session.DeviceId,
            session.License.CustomerName,
            session.License.GetDisplayState(clock.UtcNow),
            session.CurrentClientVersion);
    }
}
