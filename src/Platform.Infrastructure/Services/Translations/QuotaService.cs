using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Common;
using Platform.Domain.Translations;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services.Translations;

public sealed class QuotaService(
    PlatformDbContext dbContext,
    IClock clock,
    IOptions<TranslationOptions> options) : IQuotaService
{
    private readonly TranslationOptions _options = options.Value;

    public async Task<OperationResult<TranslationQuotaStatusDto>> GetCurrentQuotaAsync(
        Guid licenseId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var quota = await GetOrCreateQuotaAsync(licenseId, cancellationToken);
        var usage = await GetOrCreateUsageAsync(licenseId, deviceId, cancellationToken);
        var now = clock.UtcNow;
        var hourThreshold = now.AddHours(-1);

        var activeJobs = await dbContext.TranslationJobs.CountAsync(
            x => x.LicenseId == licenseId &&
                 (x.State == TranslationJobState.Pending ||
                  x.State == TranslationJobState.Queued ||
                  x.State == TranslationJobState.Processing ||
                  x.State == TranslationJobState.CancelRequested),
            cancellationToken);

        var jobsThisHour = await dbContext.TranslationJobs.CountAsync(
            x => x.LicenseId == licenseId && x.RequestedUtc >= hourThreshold,
            cancellationToken);

        var analysisThisHour = await dbContext.ModAnalysisSnapshots.CountAsync(
            x => x.LicenseId == licenseId && x.CreatedUtc >= hourThreshold,
            cancellationToken);

        var consumedToday = usage.ReservedCharacters + usage.ConsumedCharacters;
        return OperationResult<TranslationQuotaStatusDto>.Success(
            BuildStatusDto(quota, consumedToday, activeJobs, jobsThisHour, analysisThisHour));
    }

    public async Task<OperationResult<TranslationQuotaStatusDto>> ReserveAsync(
        Guid licenseId,
        Guid deviceId,
        int characterCount,
        int fileCount,
        int segmentCount,
        CancellationToken cancellationToken)
    {
        var quota = await GetOrCreateQuotaAsync(licenseId, cancellationToken);
        if (!quota.IsEnabled)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Перевод для этой лицензии отключён.", "translation_disabled");
        }

        if (fileCount > quota.MaxFilesPerJob)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Превышен лимит файлов в одном задании.", "quota_files_exceeded");
        }

        if (segmentCount > quota.MaxSegmentsPerJob)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Превышен лимит сегментов в одном задании.", "quota_segments_exceeded");
        }

        if (characterCount > quota.MaxCharactersPerJob)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Превышен лимит символов в одном задании.", "quota_characters_per_job_exceeded");
        }

        var usage = await GetOrCreateUsageAsync(licenseId, deviceId, cancellationToken);
        var now = clock.UtcNow;
        var hourThreshold = now.AddHours(-1);

        var activeJobs = await dbContext.TranslationJobs.CountAsync(
            x => x.LicenseId == licenseId &&
                 (x.State == TranslationJobState.Pending ||
                  x.State == TranslationJobState.Queued ||
                  x.State == TranslationJobState.Processing ||
                  x.State == TranslationJobState.CancelRequested),
            cancellationToken);

        if (activeJobs >= quota.MaxConcurrentJobs)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Достигнут лимит параллельных заданий перевода.", "quota_concurrent_jobs_exceeded");
        }

        var jobsThisHour = await dbContext.TranslationJobs.CountAsync(
            x => x.LicenseId == licenseId && x.RequestedUtc >= hourThreshold,
            cancellationToken);

        if (jobsThisHour >= quota.MaxJobsPerHour)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Достигнут почасовой лимит на создание заданий перевода.", "quota_jobs_per_hour_exceeded");
        }

        var consumedToday = usage.ReservedCharacters + usage.ConsumedCharacters;
        if (consumedToday + characterCount > quota.MaxCharactersPerDay)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Дневная квота символов исчерпана.", "quota_characters_per_day_exceeded");
        }

        usage.ReservedCharacters += characterCount;
        usage.JobsCreated += 1;
        await dbContext.SaveChangesAsync(cancellationToken);

        var analysisThisHour = await dbContext.ModAnalysisSnapshots.CountAsync(
            x => x.LicenseId == licenseId && x.CreatedUtc >= hourThreshold,
            cancellationToken);

        return OperationResult<TranslationQuotaStatusDto>.Success(
            BuildStatusDto(quota, consumedToday + characterCount, activeJobs + 1, jobsThisHour + 1, analysisThisHour));
    }

    public async Task ReleaseReservationAsync(
        Guid licenseId,
        Guid deviceId,
        int characterCount,
        CancellationToken cancellationToken)
    {
        var usage = await GetOrCreateUsageAsync(licenseId, deviceId, cancellationToken);
        usage.ReservedCharacters = Math.Max(0, usage.ReservedCharacters - characterCount);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CommitUsageAsync(
        Guid licenseId,
        Guid deviceId,
        int reservedCharacterCount,
        int consumedCharacterCount,
        TranslationJobState terminalState,
        CancellationToken cancellationToken)
    {
        var usage = await GetOrCreateUsageAsync(licenseId, deviceId, cancellationToken);
        usage.ReservedCharacters = Math.Max(0, usage.ReservedCharacters - reservedCharacterCount);

        switch (terminalState)
        {
            case TranslationJobState.Completed:
                usage.ConsumedCharacters += consumedCharacterCount;
                usage.JobsCompleted += 1;
                break;
            case TranslationJobState.Cancelled:
                usage.JobsCancelled += 1;
                break;
            case TranslationJobState.Failed:
                usage.ConsumedCharacters += consumedCharacterCount;
                usage.JobsFailed += 1;
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TranslationQuota> GetOrCreateQuotaAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        var quota = await dbContext.TranslationQuotas.FirstOrDefaultAsync(x => x.LicenseId == licenseId, cancellationToken);
        if (quota is not null)
        {
            return quota;
        }

        quota = new TranslationQuota
        {
            LicenseId = licenseId,
            MaxFilesPerJob = _options.MaxFilesPerRequest,
            MaxSegmentsPerJob = _options.MaxSegmentsPerRequest,
            MaxCharactersPerJob = _options.MaxCharactersPerRequest,
            MaxCharactersPerDay = _options.MaxCharactersPerRequest * 4,
            MaxConcurrentJobs = 2,
            MaxJobsPerHour = _options.JobCreateLimitPerHour,
            MaxAnalysisPerHour = _options.AnalyzeLimitPerHour,
            Notes = "Автоматически созданная базовая квота."
        };

        dbContext.TranslationQuotas.Add(quota);
        await dbContext.SaveChangesAsync(cancellationToken);
        return quota;
    }

    private async Task<TranslationUsage> GetOrCreateUsageAsync(Guid licenseId, Guid deviceId, CancellationToken cancellationToken)
    {
        var usageDate = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var usage = await dbContext.TranslationUsages
            .FirstOrDefaultAsync(x => x.LicenseId == licenseId && x.DeviceId == deviceId && x.UsageDate == usageDate, cancellationToken);

        if (usage is not null)
        {
            return usage;
        }

        usage = new TranslationUsage
        {
            LicenseId = licenseId,
            DeviceId = deviceId,
            UsageDate = usageDate
        };

        dbContext.TranslationUsages.Add(usage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return usage;
    }

    private static TranslationQuotaStatusDto BuildStatusDto(
        TranslationQuota quota,
        int reservedAndConsumedToday,
        int activeJobs,
        int jobsThisHour,
        int analysisThisHour) =>
        new(
            quota.MaxFilesPerJob,
            quota.MaxSegmentsPerJob,
            quota.MaxCharactersPerJob,
            quota.MaxCharactersPerDay,
            Math.Max(0, quota.MaxCharactersPerDay - reservedAndConsumedToday),
            quota.MaxConcurrentJobs,
            activeJobs,
            quota.MaxJobsPerHour,
            Math.Max(0, quota.MaxJobsPerHour - jobsThisHour),
            quota.MaxAnalysisPerHour,
            Math.Max(0, quota.MaxAnalysisPerHour - analysisThisHour));
}
