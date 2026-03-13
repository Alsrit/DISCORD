using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Services;
using Platform.Domain.Common;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Worker;

public sealed class MaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<StorageOptions> storageOptions,
    IOptions<TranslationOptions> translationOptions,
    ILogger<MaintenanceWorker> logger) : BackgroundService
{
    private const long SeedLockId = 5_837_219_114_203_317_921;

    private readonly StorageOptions _storage = storageOptions.Value;
    private readonly TranslationOptions _translation = translationOptions.Value;

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
        var seeder = scope.ServiceProvider.GetRequiredService<IPlatformSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    private async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

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

        var expiredSnapshots = await dbContext.ModAnalysisSnapshots
            .Where(x => x.ExpiresUtc <= utcNow)
            .ToListAsync(cancellationToken);

        if (expiredSnapshots.Count > 0)
        {
            dbContext.ModAnalysisSnapshots.RemoveRange(expiredSnapshots);
        }

        var staleJobs = await dbContext.TranslationJobs
            .Include(x => x.Files)
            .Where(x =>
                x.State == TranslationJobState.Processing &&
                x.StartedUtc.HasValue &&
                x.StartedUtc.Value <= utcNow.AddMinutes(-_translation.JobTimeoutMinutes))
            .ToListAsync(cancellationToken);

        foreach (var job in staleJobs)
        {
            job.State = TranslationJobState.Failed;
            job.CompletedUtc = utcNow;
            job.FailureCode = "job_timeout";
            job.FailureReason = "Задание было принудительно остановлено по таймауту worker.";

            foreach (var file in job.Files.Where(x => x.State == TranslationFileState.Processing))
            {
                file.State = TranslationFileState.Failed;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        CleanupDirectory(_storage.TranslationTempRoot, utcNow.AddHours(-6));
        CleanupDirectory(_storage.TranslationStorageRoot, utcNow.AddHours(-_translation.ArtifactRetentionHours));

        logger.LogInformation(
            "Worker выполнил обслуживание: expiredSessions={ExpiredSessions}, trimmedTelemetry={TelemetryCount}, expiredSnapshots={SnapshotCount}, staleJobs={StaleJobs}",
            expiredSessions.Count,
            oldTelemetry.Count,
            expiredSnapshots.Count,
            staleJobs.Count);
    }

    private void CleanupDirectory(string root, DateTimeOffset thresholdUtc)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(root))
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (lastWrite <= thresholdUtc.UtcDateTime)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось удалить устаревший файл {FilePath}", file);
            }
        }
    }
}
