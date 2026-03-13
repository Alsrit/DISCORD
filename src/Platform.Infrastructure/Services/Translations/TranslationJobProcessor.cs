using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Services;
using Platform.Domain.Common;
using Platform.Domain.Translations;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services.Translations;

public sealed class TranslationJobProcessor(
    PlatformDbContext dbContext,
    IClock clock,
    ITranslationQueueService queueService,
    ITranslationProvider translationProvider,
    IGlossaryService glossaryService,
    ITranslationPlaceholderProtector placeholderProtector,
    ITranslationPackagingService packagingService,
    IQuotaService quotaService,
    ISecurityIncidentService securityIncidentService,
    IOptions<TranslationOptions> options,
    ILogger<TranslationJobProcessor> logger) : ITranslationJobProcessor
{
    private readonly TranslationOptions _options = options.Value;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var dequeuedJobId = await queueService.TryDequeueAsync(cancellationToken);
        Guid? jobId = dequeuedJobId;
        if (jobId is null)
        {
            jobId = await dbContext.TranslationJobs
                .Where(x => x.State == TranslationJobState.Queued || x.State == TranslationJobState.CancelRequested)
                .OrderBy(x => x.RequestedUtc)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (jobId is null)
        {
            return false;
        }

        await ProcessJobAsync(jobId.Value, cancellationToken);
        return true;
    }

    public async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.TranslationJobs
            .Include(x => x.License)
            .Include(x => x.Device)
            .Include(x => x.Files)
                .ThenInclude(x => x.Segments)
            .Include(x => x.Items)
            .Include(x => x.Artifacts)
            .FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);

        if (job is null)
        {
            return;
        }

        if (job.State == TranslationJobState.CancelRequested)
        {
            await FinalizeCancelledAsync(job, cancellationToken);
            return;
        }

        if (job.State != TranslationJobState.Queued && job.State != TranslationJobState.Processing)
        {
            return;
        }

        if (!job.License.IsUsable(clock.UtcNow) || !job.Device.IsUsable())
        {
            job.FailureCode = "license_unavailable";
            job.FailureReason = "Лицензия или устройство больше не доступны для перевода.";
            await FinalizeFailedAsync(job, cancellationToken);
            return;
        }

        job.State = TranslationJobState.Processing;
        job.StartedUtc ??= clock.UtcNow;
        job.LastHeartbeatUtc = clock.UtcNow;
        job.LeaseExpiresUtc = clock.UtcNow.AddSeconds(_options.ProcessingLeaseSeconds);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var glossary = await glossaryService.GetEffectiveGlossaryAsync(job.LicenseId, job.SourceLanguage, job.TargetLanguage, cancellationToken);
            foreach (var file in job.Files.OrderBy(x => x.SanitizedPath, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (job.State == TranslationJobState.CancelRequested)
                {
                    await FinalizeCancelledAsync(job, cancellationToken);
                    return;
                }

                file.State = TranslationFileState.Processing;
                await dbContext.SaveChangesAsync(cancellationToken);

                var batches = BuildBatches(file.Segments.OrderBy(x => x.Sequence).ToArray(), _options.WorkerBatchCharacters);
                var batchNumber = file.TranslationJob.Items.Count + 1;
                foreach (var batch in batches)
                {
                    if (job.State == TranslationJobState.CancelRequested)
                    {
                        await FinalizeCancelledAsync(job, cancellationToken);
                        return;
                    }

                    var item = new TranslationJobItem
                    {
                        TranslationJobId = job.Id,
                        BatchNumber = batchNumber++,
                        ProviderCode = job.ProviderCode,
                        CharacterCount = batch.Sum(x => x.CharacterCount),
                        AttemptCount = job.RetryCount + 1,
                        State = TranslationJobItemState.Processing,
                        SegmentIdsJson = TranslationJson.Serialize(batch.Select(x => x.Id).ToArray()),
                        StartedUtc = clock.UtcNow
                    };

                    var protectedTexts = new List<string>(batch.Count);
                    foreach (var segment in batch)
                    {
                        var protectedResult = placeholderProtector.Protect(segment.SourceText, glossary);
                        segment.ProtectedSourceText = protectedResult.Text;
                        segment.PlaceholderMapJson = TranslationJson.Serialize(protectedResult.PlaceholderMap);
                        segment.State = TranslationSegmentState.Protected;
                        protectedTexts.Add(protectedResult.Text);
                    }

                    dbContext.TranslationJobItems.Add(item);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    var translatedTexts = await translationProvider.TranslateAsync(
                        job.SourceLanguage,
                        job.TargetLanguage,
                        protectedTexts,
                        cancellationToken);

                    item.RequestPayloadJson = TranslationJson.Serialize(new { protectedTexts, job.SourceLanguage, job.TargetLanguage });
                    item.ResponsePayloadJson = TranslationJson.Serialize(translatedTexts);
                    item.State = TranslationJobItemState.Completed;
                    item.CompletedUtc = clock.UtcNow;

                    for (var index = 0; index < batch.Count; index++)
                    {
                        var segment = batch[index];
                        var translatedText = translatedTexts.ElementAt(index);
                        segment.ProtectedTranslationText = translatedText;
                        var restoreResult = placeholderProtector.Restore(
                            translatedText,
                            TranslationJson.Deserialize(segment.PlaceholderMapJson, new Dictionary<string, string>()));

                        if (!restoreResult.Succeeded || restoreResult.Data is null)
                        {
                            segment.State = TranslationSegmentState.Failed;
                            segment.ValidationMessage = restoreResult.Message;
                            throw new TranslationProviderException(restoreResult.Message, restoreResult.ErrorCode ?? "placeholder_restore_failed");
                        }

                        segment.FinalText = NormalizeTranslationText(ApplyGlossary(restoreResult.Data, glossary));
                        segment.State = TranslationSegmentState.Validated;
                        segment.ValidationMessage = string.Empty;
                        job.ProcessedSegments += 1;
                        job.ProcessedCharacters += segment.FinalText.Length;
                    }

                    job.LastHeartbeatUtc = clock.UtcNow;
                    job.LeaseExpiresUtc = clock.UtcNow.AddSeconds(_options.ProcessingLeaseSeconds);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                file.TranslatedContent = BuildTranslatedFile(file);
                file.State = TranslationFileState.Completed;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var packagedResult = await packagingService.PackageAsync(job, job.Files.ToArray(), cancellationToken);
            var artifact = new SubmodBuildArtifact
            {
                TranslationJobId = job.Id,
                ArtifactType = TranslationArtifactType.ResultPackage,
                IsPrimary = true,
                FileName = packagedResult.FileName,
                StoragePath = packagedResult.StoragePath,
                ContentType = packagedResult.ContentType,
                SizeBytes = packagedResult.SizeBytes,
                Sha256 = packagedResult.Sha256,
                ManifestJson = packagedResult.ManifestJson
            };

            dbContext.SubmodBuildArtifacts.Add(artifact);
            job.State = TranslationJobState.Completed;
            job.CompletedUtc = clock.UtcNow;
            job.ResultStoragePath = packagedResult.StoragePath;
            job.FailureCode = string.Empty;
            job.FailureReason = string.Empty;

            await quotaService.CommitUsageAsync(job.LicenseId, job.DeviceId, job.TotalCharacters, job.ProcessedCharacters, TranslationJobState.Completed, cancellationToken);
            dbContext.TranslationAuditEvents.Add(CreateAuditEvent(job, AuditSeverity.Information, "job_completed", "Перевод задания успешно завершён."));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (TranslationProviderException ex)
        {
            logger.LogWarning(ex, "Translation provider error for job {JobId}", job.Id);
            await securityIncidentService.CaptureAsync(
                SecurityIncidentType.TranslationProviderFailure,
                ex.Message,
                AuditSeverity.Warning,
                new RequestContext("worker", "worker", job.CorrelationId),
                job.LicenseId,
                job.DeviceId,
                job.SessionId,
                new { job.Id, ex.ErrorCode },
                cancellationToken);

            await HandleFailureAsync(job, ex.Message, ex.ErrorCode, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled translation processing error for job {JobId}", job.Id);
            await HandleFailureAsync(job, ex.Message, "translation_processing_failed", cancellationToken);
        }
    }

    private async Task HandleFailureAsync(TranslationJob job, string message, string errorCode, CancellationToken cancellationToken)
    {
        job.FailureCode = errorCode;
        job.FailureReason = message;

        if (job.State == TranslationJobState.CancelRequested)
        {
            await FinalizeCancelledAsync(job, cancellationToken);
            return;
        }

        if (job.RetryCount < job.MaxRetryCount)
        {
            job.RetryCount += 1;
            job.State = TranslationJobState.Queued;
            job.LeaseExpiresUtc = clock.UtcNow.AddSeconds(_options.ProcessingLeaseSeconds);
            dbContext.TranslationAuditEvents.Add(CreateAuditEvent(job, AuditSeverity.Warning, "job_retry_scheduled", "Задание перевода будет повторно поставлено в очередь."));
            await dbContext.SaveChangesAsync(cancellationToken);
            await queueService.EnqueueAsync(job.Id, cancellationToken);
            return;
        }

        await FinalizeFailedAsync(job, cancellationToken);
    }

    private async Task FinalizeFailedAsync(TranslationJob job, CancellationToken cancellationToken)
    {
        job.State = TranslationJobState.Failed;
        job.CompletedUtc = clock.UtcNow;

        foreach (var file in job.Files.Where(x => x.State == TranslationFileState.Pending || x.State == TranslationFileState.Processing))
        {
            file.State = TranslationFileState.Failed;
        }

        await quotaService.CommitUsageAsync(job.LicenseId, job.DeviceId, job.TotalCharacters, job.ProcessedCharacters, TranslationJobState.Failed, cancellationToken);
        dbContext.TranslationAuditEvents.Add(CreateAuditEvent(job, AuditSeverity.Error, "job_failed", job.FailureReason));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task FinalizeCancelledAsync(TranslationJob job, CancellationToken cancellationToken)
    {
        job.State = TranslationJobState.Cancelled;
        job.CancelledUtc = clock.UtcNow;
        job.CompletedUtc = clock.UtcNow;

        foreach (var file in job.Files.Where(x => x.State == TranslationFileState.Pending || x.State == TranslationFileState.Processing))
        {
            file.State = TranslationFileState.Cancelled;
        }

        await quotaService.CommitUsageAsync(job.LicenseId, job.DeviceId, job.TotalCharacters, 0, TranslationJobState.Cancelled, cancellationToken);
        dbContext.TranslationAuditEvents.Add(CreateAuditEvent(job, AuditSeverity.Warning, "job_cancelled", "Задание перевода отменено."));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<List<TranslationSegment>> BuildBatches(IReadOnlyCollection<TranslationSegment> segments, int maxCharacters)
    {
        var batches = new List<List<TranslationSegment>>();
        var currentBatch = new List<TranslationSegment>();
        var currentChars = 0;

        foreach (var segment in segments)
        {
            if (currentBatch.Count > 0 && currentChars + segment.CharacterCount > maxCharacters)
            {
                batches.Add(currentBatch);
                currentBatch = new List<TranslationSegment>();
                currentChars = 0;
            }

            currentBatch.Add(segment);
            currentChars += segment.CharacterCount;
        }

        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch);
        }

        return batches;
    }

    private static string BuildTranslatedFile(TranslationFile file)
    {
        var lines = file.OriginalContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var segment in file.Segments.Where(x => x.State == TranslationSegmentState.Validated).OrderBy(x => x.LineNumber))
        {
            var lineIndex = segment.LineNumber - 1;
            if (lineIndex < 0 || lineIndex >= lines.Length)
            {
                continue;
            }

            lines[lineIndex] = $"{segment.Prefix}{segment.FinalText}{segment.Suffix}";
        }

        return string.Join('\n', lines);
    }

    private static string ApplyGlossary(string text, EffectiveGlossary glossary)
    {
        var output = text;
        foreach (var term in glossary.Terms.OrderByDescending(x => x.Key.Length))
        {
            if (!string.IsNullOrWhiteSpace(term.Key))
            {
                output = output.Replace(term.Key, term.Value, StringComparison.Ordinal);
            }
        }

        return output;
    }

    private static string NormalizeTranslationText(string text) =>
        text.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static TranslationAuditEvent CreateAuditEvent(TranslationJob job, AuditSeverity severity, string eventType, string message) =>
        new()
        {
            TranslationJobId = job.Id,
            LicenseId = job.LicenseId,
            DeviceId = job.DeviceId,
            ActorType = AuditActorType.System,
            ActorIdentifier = "worker",
            Severity = severity,
            Category = "translation",
            EventType = eventType,
            Message = message,
            PayloadJson = "{}",
            OccurredUtc = DateTimeOffset.UtcNow
        };
}
