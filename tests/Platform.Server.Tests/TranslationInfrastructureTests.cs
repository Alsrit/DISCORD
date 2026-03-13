using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.Api.Extensions;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Auditing;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Translations;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Services.Translations;
using Xunit;

namespace Platform.Server.Tests;

public sealed class TranslationInfrastructureTests
{
    [Fact]
    public void PathSanitizer_BlocksPathTraversal()
    {
        var sanitizer = new TranslationPathSanitizer();
        Assert.Throws<TranslationRequestValidationException>(() => sanitizer.SanitizeRelativePath("../mods/evil.yml"));
    }

    [Fact]
    public void PlaceholderProtector_PreservesTokens()
    {
        var protector = new TranslationPlaceholderProtector();
        var glossary = new EffectiveGlossary(
            new Dictionary<string, string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        var protectedResult = protector.Protect("Hello $NAME$ and [Root.GetName]", glossary);
        var restored = protector.Restore("Привет " + protectedResult.Text["Hello ".Length..], protectedResult.PlaceholderMap);

        Assert.True(restored.Succeeded);
        Assert.Contains("$NAME$", restored.Data!);
        Assert.Contains("[Root.GetName]", restored.Data!);
    }

    [Fact]
    public async Task ModAnalysis_ParsesLocalizationEntries()
    {
        var sanitizer = new TranslationPathSanitizer();
        var service = new ModAnalysisService(
            sanitizer,
            Options.Create(new TranslationOptions()));

        const string content = """
        l_english:
        greeting:0 "Hello $NAME$"
        ship_name:0 "Starbase"
        """;

        var file = new UploadedLocalizationFileDto(
            "localisation/english/test_l_english.yml",
            content,
            "en",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))),
            System.Text.Encoding.UTF8.GetByteCount(content));

        var result = await service.AnalyzeAsync(new AnalyzeModRequest("Test Mod", "1.0", "mod/test", "en", [file]), CancellationToken.None);

        Assert.Equal(1, result.FileCount);
        Assert.Equal(2, result.SegmentCount);
        Assert.Equal("l_english", result.Files.Single().HeaderKey);
    }

    [Fact]
    public async Task QuotaService_RejectsWhenDailyLimitExceeded()
    {
        await using var db = CreateDbContext();
        var license = new License { CustomerName = "Tester", CustomerEmail = "tester@example.com", LicenseKeyHash = "hash", LicenseKeyMasked = "mask", LookupPrefix = "SLP" };
        var device = new Device { License = license, DeviceFingerprintHash = "fp", InstallationId = "inst", DeviceName = "PC", MachineName = "PC", OperatingSystem = "Windows" };
        db.Licenses.Add(license);
        db.Devices.Add(device);
        db.TranslationQuotas.Add(new TranslationQuota
        {
            License = license,
            MaxFilesPerJob = 5,
            MaxSegmentsPerJob = 10,
            MaxCharactersPerJob = 50,
            MaxCharactersPerDay = 100,
            MaxConcurrentJobs = 2,
            MaxJobsPerHour = 10,
            MaxAnalysisPerHour = 10
        });
        db.TranslationUsages.Add(new TranslationUsage
        {
            License = license,
            Device = device,
            UsageDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ReservedCharacters = 80,
            ConsumedCharacters = 10
        });
        await db.SaveChangesAsync();

        var service = new QuotaService(db, new FakeClock(), Options.Create(new TranslationOptions()));
        var result = await service.ReserveAsync(license.Id, device.Id, 20, 1, 1, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("quota_characters_per_day_exceeded", result.ErrorCode);
    }

    [Fact]
    public void ProblemDetailsMapping_Uses429ForRateLimit()
    {
        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.TraceIdentifier = "corr-1";

        var result = controller.ToProblemResult("Too many requests", "rate_limited");
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
        Assert.Equal("corr-1", problem.Extensions["correlationId"]);
    }

    [Fact]
    public async Task TranslationLifecycle_CreatesAndProcessesJob()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "secure-platform-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "releases"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "translations"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "translations-temp"));

        try
        {
            await using var db = CreateDbContext();
            var license = new License
            {
                CustomerName = "Tester",
                CustomerEmail = "tester@example.com",
                LicenseKeyHash = "hash",
                LicenseKeyMasked = "SLP-TEST",
                LookupPrefix = "SLP",
                State = LicenseState.Active
            };
            var device = new Device
            {
                License = license,
                DeviceFingerprintHash = "fp",
                InstallationId = "inst-1",
                DeviceName = "PC",
                MachineName = "PC",
                OperatingSystem = "Windows",
                State = DeviceState.Active
            };
            var session = new ClientSession
            {
                License = license,
                Device = device,
                AccessTokenHash = "a",
                RefreshTokenHash = "r",
                AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
                RefreshTokenExpiresUtc = DateTimeOffset.UtcNow.AddDays(1),
                State = SessionState.Active
            };

            db.Licenses.Add(license);
            db.Devices.Add(device);
            db.ClientSessions.Add(session);
            db.TranslationQuotas.Add(new TranslationQuota
            {
                License = license,
                MaxFilesPerJob = 10,
                MaxSegmentsPerJob = 50,
                MaxCharactersPerJob = 500,
                MaxCharactersPerDay = 5000,
                MaxConcurrentJobs = 5,
                MaxJobsPerHour = 10,
                MaxAnalysisPerHour = 10
            });
            db.TranslationProviderSettings.Add(new TranslationProviderSettings
            {
                ProviderCode = "yandex",
                DisplayName = "Fake Yandex",
                IsEnabled = true,
                Endpoint = "https://example.test/translate",
                LanguagesEndpoint = "https://example.test/languages",
                SecretReference = "test"
            });
            await db.SaveChangesAsync();

            var queue = new FakeQueueService();
            var options = Options.Create(new TranslationOptions());
            var requestContext = new RequestContext("127.0.0.1", "tests", "corr-job");
            var analysisService = new ModAnalysisService(new TranslationPathSanitizer(), options);
            var quotaService = new QuotaService(db, new FakeClock(), options);
            var glossaryService = new GlossaryService(db);
            var resultService = new TranslationResultService();
            var manifestService = new SubmodManifestService();
            var packagingService = new TranslationPackagingService(
                new TranslationPathSanitizer(),
                manifestService,
                Options.Create(new StorageOptions
                {
                    ReleaseStorageRoot = Path.Combine(tempRoot, "releases"),
                    TranslationStorageRoot = Path.Combine(tempRoot, "translations"),
                    TranslationTempRoot = Path.Combine(tempRoot, "translations-temp")
                }));

            var jobService = new TranslationJobService(
                db,
                new FakeClock(),
                new FakeRateLimitService(),
                new FakeAuditTrailService(),
                new FakeSecurityIncidentService(),
                queue,
                analysisService,
                new FakeTranslationProvider(),
                quotaService,
                glossaryService,
                resultService,
                manifestService,
                options,
                NullLogger<TranslationJobService>.Instance);

            const string content = """
            l_english:
            greeting:0 "Hello $NAME$"
            """;
            var file = new UploadedLocalizationFileDto(
                "localisation/english/test_l_english.yml",
                content,
                "en",
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))),
                System.Text.Encoding.UTF8.GetByteCount(content));

            var createResult = await jobService.CreateJobAsync(
                session.Id,
                new CreateTranslationJobRequest(null, "My Mod", "mod/test", "en", "ru", "[RU] My Mod", "yandex", [file]),
                "idem-1",
                requestContext,
                CancellationToken.None);

            Assert.True(createResult.Succeeded);

            var processor = new TranslationJobProcessor(
                db,
                new FakeClock(),
                queue,
                new FakeTranslationProvider(),
                glossaryService,
                new TranslationPlaceholderProtector(),
                packagingService,
                quotaService,
                new FakeSecurityIncidentService(),
                options,
                NullLogger<TranslationJobProcessor>.Instance);

            await processor.ProcessJobAsync(createResult.Data!.JobId, CancellationToken.None);

            var job = await db.TranslationJobs.Include(x => x.Files).Include(x => x.Artifacts).FirstAsync(x => x.Id == createResult.Data.JobId);
            Assert.Equal(TranslationJobState.Completed, job.State);
            Assert.Single(job.Artifacts);
            Assert.Contains("Привет", job.Files.Single().TranslatedContent);
            Assert.Contains("$NAME$", job.Files.Single().TranslatedContent);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private sealed class TestController : ControllerBase;

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;
    }

    private sealed class FakeRateLimitService : IRateLimitService
    {
        public Task<bool> ConsumeAsync(string bucket, string key, int limit, TimeSpan window, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FakeAuditTrailService : IAuditTrailService
    {
        public Task WriteAsync(Guid? licenseId, Guid? deviceId, Guid? adminUserId, string category, string eventType, string message, AuditSeverity severity, RequestContext context, object? payload, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeSecurityIncidentService : ISecurityIncidentService
    {
        public Task CaptureAsync(SecurityIncidentType type, string description, AuditSeverity severity, RequestContext context, Guid? licenseId, Guid? deviceId, Guid? sessionId, object? payload, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeQueueService : ITranslationQueueService
    {
        private readonly Queue<Guid> _items = new();

        public Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
        {
            _items.Enqueue(jobId);
            return Task.CompletedTask;
        }

        public Task<Guid?> TryDequeueAsync(CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(_items.Count == 0 ? null : _items.Dequeue());

        public Task<long> GetQueuedCountAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)_items.Count);
    }

    private sealed class FakeTranslationProvider : ITranslationProvider
    {
        public string ProviderCode => "yandex";

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IReadOnlyCollection<LanguageOptionDto>> GetLanguagesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<LanguageOptionDto>>([new("en", "English", true, true), new("ru", "Russian", true, true)]);

        public Task<IReadOnlyCollection<string>> TranslateAsync(string sourceLanguage, string targetLanguage, IReadOnlyCollection<string> texts, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>(texts.Select(x => x.Replace("Hello", "Привет", StringComparison.Ordinal)).ToArray());
    }
}
