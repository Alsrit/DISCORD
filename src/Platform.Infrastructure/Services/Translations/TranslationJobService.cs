using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Translations;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationJobService(
    PlatformDbContext dbContext,
    IClock clock,
    IRateLimitService rateLimitService,
    IAuditTrailService auditTrailService,
    ISecurityIncidentService securityIncidentService,
    ITranslationQueueService queueService,
    IModAnalysisService modAnalysisService,
    ITranslationProvider translationProvider,
    IQuotaService quotaService,
    IGlossaryService glossaryService,
    ITranslationResultService translationResultService,
    ISubmodManifestService submodManifestService,
    IOptions<TranslationOptions> options,
    ILogger<TranslationJobService> logger) : ITranslationJobService
{
    private readonly TranslationOptions _options = options.Value;

    public async Task<OperationResult<AnalyzeModResponse>> AnalyzeAsync(
        Guid sessionId,
        AnalyzeModRequest request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<AnalyzeModResponse>.Failure("Сессия клиента не найдена.", "session_not_found");
        }

        if (!await rateLimitService.ConsumeAsync("mods-analyze", sessionId.ToString("N"), _options.AnalyzeLimitPerHour, TimeSpan.FromHours(1), cancellationToken))
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.TranslationRateLimited,
                "Превышен лимит запросов анализа mod payload.",
                AuditSeverity.Warning,
                context,
                session.LicenseId,
                session.DeviceId,
                session.Id,
                new { request.ModName },
                cancellationToken);

            return OperationResult<AnalyzeModResponse>.Failure("Слишком много запросов анализа. Попробуйте позже.", "rate_limited");
        }

        try
        {
            var analysis = await modAnalysisService.AnalyzeAsync(request, cancellationToken);
            var snapshot = new ModAnalysisSnapshot
            {
                LicenseId = session.LicenseId,
                DeviceId = session.DeviceId,
                SessionId = session.Id,
                ModName = request.ModName,
                ModVersion = request.ModVersion,
                OriginalModReference = request.OriginalModReference,
                SourceLanguage = request.SourceLanguage,
                TargetLanguage = "ru",
                PayloadSha256 = analysis.PayloadSha256,
                FileCount = analysis.FileCount,
                SegmentCount = analysis.SegmentCount,
                CharacterCount = analysis.CharacterCount,
                FilesJson = TranslationJson.Serialize(analysis.Files.Select(MapAnalysisFileDto).ToArray()),
                WarningsJson = TranslationJson.Serialize(analysis.Warnings),
                MetadataJson = TranslationJson.Serialize(new { request.ModVersion }),
                ExpiresUtc = clock.UtcNow.AddHours(_options.SnapshotRetentionHours)
            };

            dbContext.ModAnalysisSnapshots.Add(snapshot);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new AnalyzeModResponse(
                snapshot.Id,
                request.ModName,
                request.SourceLanguage,
                analysis.FileCount,
                analysis.SegmentCount,
                analysis.CharacterCount,
                analysis.Files.Select(MapAnalysisFileDto).ToArray(),
                analysis.Warnings.ToArray(),
                snapshot.ExpiresUtc);

            return OperationResult<AnalyzeModResponse>.Success(response, "Анализ мода завершён.");
        }
        catch (TranslationRequestValidationException ex)
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.TranslationPayloadRejected,
                ex.Message,
                AuditSeverity.Warning,
                context,
                session.LicenseId,
                session.DeviceId,
                session.Id,
                new { request.ModName, request.SourceLanguage },
                cancellationToken);

            return OperationResult<AnalyzeModResponse>.Failure(ex.Message, ex.ErrorCode);
        }
    }

    public async Task<OperationResult<TranslationJobCreatedResponse>> CreateJobAsync(
        Guid sessionId,
        CreateTranslationJobRequest request,
        string? idempotencyKey,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<TranslationJobCreatedResponse>.Failure("Сессия клиента не найдена.", "session_not_found");
        }

        if (!await rateLimitService.ConsumeAsync("translation-create", sessionId.ToString("N"), _options.JobCreateLimitPerHour, TimeSpan.FromHours(1), cancellationToken))
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.TranslationRateLimited,
                "Превышен лимит создания заданий перевода.",
                AuditSeverity.Warning,
                context,
                session.LicenseId,
                session.DeviceId,
                session.Id,
                new { request.ModName },
                cancellationToken);

            return OperationResult<TranslationJobCreatedResponse>.Failure("Слишком много заданий перевода за короткое время.", "rate_limited");
        }

        if (!string.Equals(request.ProviderCode, translationProvider.ProviderCode, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<TranslationJobCreatedResponse>.Failure("Запрошенный provider не поддерживается текущей конфигурацией сервера.", "provider_not_supported");
        }

        if (!await translationProvider.IsAvailableAsync(cancellationToken))
        {
            return OperationResult<TranslationJobCreatedResponse>.Failure("Провайдер перевода сейчас недоступен.", "provider_unavailable");
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await dbContext.TranslationJobs
                .Include(x => x.Artifacts)
                .Include(x => x.Files)
                .FirstOrDefaultAsync(
                    x => x.LicenseId == session.LicenseId &&
                         x.DeviceId == session.DeviceId &&
                         x.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                var quota = await quotaService.GetCurrentQuotaAsync(session.LicenseId, session.DeviceId, cancellationToken);
                return OperationResult<TranslationJobCreatedResponse>.Success(
                    new TranslationJobCreatedResponse(
                        existing.Id,
                        existing.State.ToString(),
                        existing.RequestedUtc,
                        quota.Data!,
                        "Возвращено ранее созданное задание по Idempotency-Key."),
                    "Задание уже существует.");
            }
        }

        try
        {
            var analysisRequest = new AnalyzeModRequest(
                request.ModName,
                string.Empty,
                request.OriginalModReference,
                request.SourceLanguage,
                request.Files);

            var analysis = await modAnalysisService.AnalyzeAsync(analysisRequest, cancellationToken);
            var quotaReservation = await quotaService.ReserveAsync(
                session.LicenseId,
                session.DeviceId,
                analysis.CharacterCount,
                analysis.FileCount,
                analysis.SegmentCount,
                cancellationToken);

            if (!quotaReservation.Succeeded || quotaReservation.Data is null)
            {
                await securityIncidentService.CaptureAsync(
                    SecurityIncidentType.TranslationQuotaExceeded,
                    quotaReservation.Message,
                    AuditSeverity.Warning,
                    context,
                    session.LicenseId,
                    session.DeviceId,
                    session.Id,
                    new { analysis.FileCount, analysis.SegmentCount, analysis.CharacterCount },
                    cancellationToken);

                return OperationResult<TranslationJobCreatedResponse>.Failure(quotaReservation.Message, quotaReservation.ErrorCode ?? "quota_exceeded");
            }

            ModAnalysisSnapshot? snapshot = null;
            if (request.AnalysisSnapshotId.HasValue)
            {
                snapshot = await dbContext.ModAnalysisSnapshots.FirstOrDefaultAsync(
                    x => x.Id == request.AnalysisSnapshotId.Value && x.LicenseId == session.LicenseId && x.DeviceId == session.DeviceId,
                    cancellationToken);

                if (snapshot is not null && !string.Equals(snapshot.PayloadSha256, analysis.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<TranslationJobCreatedResponse>.Failure(
                        "Содержимое mod payload не совпадает с ранее сохранённым анализом.",
                        "analysis_snapshot_mismatch");
                }
            }

            snapshot ??= new ModAnalysisSnapshot
            {
                LicenseId = session.LicenseId,
                DeviceId = session.DeviceId,
                SessionId = session.Id,
                ModName = request.ModName,
                OriginalModReference = request.OriginalModReference,
                SourceLanguage = request.SourceLanguage,
                TargetLanguage = request.TargetLanguage,
                PayloadSha256 = analysis.PayloadSha256,
                FileCount = analysis.FileCount,
                SegmentCount = analysis.SegmentCount,
                CharacterCount = analysis.CharacterCount,
                FilesJson = TranslationJson.Serialize(analysis.Files.Select(MapAnalysisFileDto).ToArray()),
                WarningsJson = TranslationJson.Serialize(analysis.Warnings),
                MetadataJson = "{}",
                ExpiresUtc = clock.UtcNow.AddHours(_options.SnapshotRetentionHours)
            };

            if (snapshot.CreatedUtc == default)
            {
                dbContext.ModAnalysisSnapshots.Add(snapshot);
            }

            var job = new TranslationJob
            {
                LicenseId = session.LicenseId,
                DeviceId = session.DeviceId,
                SessionId = session.Id,
                AnalysisSnapshot = snapshot,
                IdempotencyKey = idempotencyKey?.Trim() ?? string.Empty,
                CorrelationId = context.CorrelationId,
                QueueName = _options.QueueName,
                ProviderCode = request.ProviderCode,
                ModName = request.ModName,
                OriginalModReference = request.OriginalModReference,
                RequestedSubmodName = request.RequestedSubmodName,
                SourceLanguage = request.SourceLanguage,
                TargetLanguage = request.TargetLanguage,
                State = TranslationJobState.Queued,
                TotalFiles = analysis.FileCount,
                TotalSegments = analysis.SegmentCount,
                TotalCharacters = analysis.CharacterCount,
                RequestedUtc = clock.UtcNow,
                LeaseExpiresUtc = clock.UtcNow.AddMinutes(1),
                MaxRetryCount = _options.MaxRetryAttempts,
                MetadataJson = TranslationJson.Serialize(new { analysis.PayloadSha256 })
            };

            foreach (var file in analysis.Files)
            {
                var translationFile = new TranslationFile
                {
                    RelativePath = file.RelativePath,
                    SanitizedPath = file.SanitizedPath,
                    HeaderKey = file.HeaderKey,
                    SourceLanguage = file.SourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    OriginalSha256 = file.OriginalSha256,
                    OriginalSizeBytes = file.OriginalSizeBytes,
                    SegmentCount = file.Entries.Count,
                    CharacterCount = file.Entries.Sum(x => x.SourceText.Length),
                    WarningJson = TranslationJson.Serialize(file.Warnings),
                    OriginalContent = file.OriginalContent,
                    State = TranslationFileState.Pending,
                    Segments = file.Entries.Select((entry, index) => new TranslationSegment
                    {
                        Sequence = index + 1,
                        LineNumber = entry.LineNumber,
                        LocalizationKey = entry.LocalizationKey,
                        Prefix = entry.Prefix,
                        SourceText = entry.SourceText,
                        Suffix = entry.Suffix,
                        CharacterCount = entry.SourceText.Length,
                        State = TranslationSegmentState.Pending
                    }).ToList()
                };

                job.Files.Add(translationFile);
            }

            dbContext.TranslationJobs.Add(job);
            dbContext.TranslationAuditEvents.Add(new TranslationAuditEvent
            {
                TranslationJob = job,
                LicenseId = session.LicenseId,
                DeviceId = session.DeviceId,
                ActorType = AuditActorType.Client,
                ActorIdentifier = context.IpAddress,
                Severity = AuditSeverity.Information,
                Category = "translation",
                EventType = "job_created",
                Message = "Создано новое задание перевода.",
                PayloadJson = TranslationJson.Serialize(new { request.ModName, request.TargetLanguage, analysis.FileCount, analysis.SegmentCount }),
                OccurredUtc = clock.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await queueService.EnqueueAsync(job.Id, cancellationToken);

            await auditTrailService.WriteAsync(
                session.LicenseId,
                session.DeviceId,
                null,
                "translation",
                "job_created",
                "Создано новое задание перевода.",
                AuditSeverity.Information,
                context,
                new { job.Id, job.ModName, job.TargetLanguage },
                cancellationToken);

            logger.LogInformation("Created translation job {JobId} for license {LicenseId}", job.Id, session.LicenseId);

            return OperationResult<TranslationJobCreatedResponse>.Success(
                new TranslationJobCreatedResponse(
                    job.Id,
                    job.State.ToString(),
                    job.RequestedUtc,
                    quotaReservation.Data,
                    "Задание поставлено в очередь."),
                "Задание на перевод создано.");
        }
        catch (TranslationRequestValidationException ex)
        {
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.TranslationPayloadRejected,
                ex.Message,
                AuditSeverity.Warning,
                context,
                session.LicenseId,
                session.DeviceId,
                session.Id,
                new { request.ModName, request.SourceLanguage, request.TargetLanguage },
                cancellationToken);

            return OperationResult<TranslationJobCreatedResponse>.Failure(ex.Message, ex.ErrorCode);
        }
    }

    public async Task<OperationResult<TranslationJobStatusDto>> GetJobAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var job = await LoadOwnedJobAsync(sessionId, jobId, cancellationToken);
        if (job is null)
        {
            return OperationResult<TranslationJobStatusDto>.Failure("Задание перевода не найдено.", "job_not_found");
        }

        var manifest = job.Artifacts
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => TranslationJson.Deserialize<SubmodManifestPreviewDto?>(x.ManifestJson, null))
            .FirstOrDefault(x => x is not null)
            ?? submodManifestService.BuildPreview(job, job.Files.ToArray());

        return OperationResult<TranslationJobStatusDto>.Success(MapJobStatus(job, manifest), "Статус задания получен.");
    }

    public async Task<OperationResult<IReadOnlyCollection<TranslationFileResultDto>>> GetJobFilesAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var job = await LoadOwnedJobAsync(sessionId, jobId, cancellationToken);
        if (job is null)
        {
            return OperationResult<IReadOnlyCollection<TranslationFileResultDto>>.Failure("Задание перевода не найдено.", "job_not_found");
        }

        var files = await translationResultService.GetFilesAsync(job, cancellationToken);
        return OperationResult<IReadOnlyCollection<TranslationFileResultDto>>.Success(files, "Файлы задания получены.");
    }

    public async Task<OperationResult<TranslationDownloadInfoDto>> GetDownloadInfoAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var job = await LoadOwnedJobAsync(sessionId, jobId, cancellationToken);
        if (job is null)
        {
            return OperationResult<TranslationDownloadInfoDto>.Failure("Задание перевода не найдено.", "job_not_found");
        }

        var info = await translationResultService.GetDownloadInfoAsync(job, cancellationToken);
        return info is null
            ? OperationResult<TranslationDownloadInfoDto>.Failure("Результат перевода ещё не готов.", "result_not_ready")
            : OperationResult<TranslationDownloadInfoDto>.Success(info, "Информация о результате получена.");
    }

    public async Task<OperationResult<(Stream Stream, string FileName, string ContentType)>> OpenDownloadAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var job = await LoadOwnedJobAsync(sessionId, jobId, cancellationToken);
        if (job is null)
        {
            return OperationResult<(Stream Stream, string FileName, string ContentType)>.Failure("Задание перевода не найдено.", "job_not_found");
        }

        var result = await translationResultService.OpenDownloadAsync(job, cancellationToken);
        return result is null
            ? OperationResult<(Stream Stream, string FileName, string ContentType)>.Failure("Результат перевода ещё не готов.", "result_not_ready")
            : OperationResult<(Stream Stream, string FileName, string ContentType)>.Success(result.Value, "Архив результата открыт.");
    }

    public async Task<OperationResult> CancelJobAsync(
        Guid sessionId,
        Guid jobId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var job = await LoadOwnedJobAsync(sessionId, jobId, cancellationToken);
        if (job is null)
        {
            return OperationResult.Failure("Задание перевода не найдено.", "job_not_found");
        }

        if (job.State is TranslationJobState.Completed or TranslationJobState.Failed or TranslationJobState.Cancelled)
        {
            return OperationResult.Failure("Это задание уже завершено и не может быть отменено.", "job_already_finished");
        }

        job.State = TranslationJobState.CancelRequested;
        job.CancelRequestedUtc = clock.UtcNow;
        dbContext.TranslationAuditEvents.Add(new TranslationAuditEvent
        {
            TranslationJobId = job.Id,
            LicenseId = job.LicenseId,
            DeviceId = job.DeviceId,
            ActorType = AuditActorType.Client,
            ActorIdentifier = context.IpAddress,
            Severity = AuditSeverity.Warning,
            Category = "translation",
            EventType = "job_cancel_requested",
            Message = "Клиент запросил отмену задания перевода.",
            PayloadJson = "{}",
            OccurredUtc = clock.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success("Запрос на отмену задания перевода сохранён.");
    }

    public Task<IReadOnlyCollection<LanguageOptionDto>> GetLanguagesAsync(CancellationToken cancellationToken) =>
        translationProvider.GetLanguagesAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ActiveGlossaryDto>> GetActiveGlossariesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Array.Empty<ActiveGlossaryDto>();
        }

        return await glossaryService.GetActiveGlossariesAsync(session.LicenseId, cancellationToken);
    }

    public async Task<OperationResult<TranslationQuotaStatusDto>> GetCurrentQuotaAsync(
        Guid sessionId,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return OperationResult<TranslationQuotaStatusDto>.Failure("Сессия клиента не найдена.", "session_not_found");
        }

        return await quotaService.GetCurrentQuotaAsync(session.LicenseId, session.DeviceId, cancellationToken);
    }

    private async Task<ClientSession?> LoadAuthorizedSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            return null;
        }

        var session = await dbContext.ClientSessions
            .Include(x => x.License)
            .Include(x => x.Device)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null || session.State != SessionState.Active)
        {
            return null;
        }

        return session.License.IsUsable(clock.UtcNow) && session.Device.IsUsable()
            ? session
            : null;
    }

    private async Task<TranslationJob?> LoadOwnedJobAsync(Guid sessionId, Guid jobId, CancellationToken cancellationToken)
    {
        var session = await LoadAuthorizedSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        return await dbContext.TranslationJobs
            .Include(x => x.Files)
                .ThenInclude(x => x.Segments)
            .Include(x => x.Artifacts)
            .FirstOrDefaultAsync(x => x.Id == jobId && x.LicenseId == session.LicenseId && x.DeviceId == session.DeviceId, cancellationToken);
    }

    private static ModFileAnalysisDto MapAnalysisFileDto(ParsedLocalizationFile file) =>
        new(
            file.RelativePath,
            file.SanitizedPath,
            file.HeaderKey,
            file.SourceLanguage,
            file.OriginalSha256,
            file.Entries.Count,
            file.Entries.Sum(x => x.SourceText.Length),
            file.Entries.Count > 0,
            file.Warnings);

    private static TranslationJobStatusDto MapJobStatus(TranslationJob job, SubmodManifestPreviewDto? manifest) =>
        new(
            job.Id,
            job.State.ToString(),
            job.ProviderCode,
            job.ModName,
            job.SourceLanguage,
            job.TargetLanguage,
            job.RequestedSubmodName,
            job.TotalFiles,
            job.TotalSegments,
            job.TotalCharacters,
            job.ProcessedSegments,
            job.ProcessedCharacters,
            job.RetryCount,
            job.Artifacts.Any(x => x.IsPrimary),
            job.FailureCode,
            job.FailureReason,
            job.RequestedUtc,
            job.StartedUtc,
            job.CompletedUtc,
            job.CancelRequestedUtc,
            manifest);
}
