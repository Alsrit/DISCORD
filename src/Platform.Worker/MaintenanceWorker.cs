using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Common;
using Platform.Infrastructure.Persistence;

namespace Platform.Worker;

public sealed class MaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SeedAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunMaintenanceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка фонового обслуживания платформы.");
            }
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<Platform.Application.Services.IPlatformSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    private async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<Platform.Application.Abstractions.IClock>();

        var utcNow = clock.UtcNow;
        var expiredSessions = await dbContext.ClientSessions
            .Where(x => x.State == SessionState.Active && x.RefreshTokenExpiresUtc <= utcNow)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
        {
            session.State = SessionState.Expired;
            session.RevokedUtc = utcNow;
            session.RevocationReason = "Refresh token истёк.";
        }

        var telemetryThreshold = utcNow.AddDays(-30);
        var oldTelemetry = await dbContext.TelemetryEvents
            .Where(x => x.ReceivedUtc < telemetryThreshold)
            .ToListAsync(cancellationToken);

        if (oldTelemetry.Count > 0)
        {
            dbContext.TelemetryEvents.RemoveRange(oldTelemetry);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Worker выполнил обслуживание: expiredSessions={ExpiredSessions}, trimmedTelemetry={TelemetryCount}", expiredSessions.Count, oldTelemetry.Count);
    }
}
