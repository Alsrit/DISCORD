using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Domain.Administration;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Translations;
using Platform.Domain.Updates;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Services.Translations;

namespace Platform.Infrastructure.Services;

public sealed class PlatformSeeder(
    PlatformDbContext dbContext,
    ILicenseKeyProtector licenseKeyProtector,
    IOptions<SeedOptions> options,
    IOptions<YandexTranslateOptions> yandexOptions,
    ILogger<PlatformSeeder> logger) : IPlatformSeeder
{
    private const long SeedLockId = 5_837_219_114_203_317_921;

    private readonly SeedOptions _options = options.Value;
    private readonly YandexTranslateOptions _yandex = yandexOptions.Value;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({SeedLockId})", cancellationToken);

        if (!await dbContext.UpdateChannels.AnyAsync(cancellationToken))
        {
            dbContext.UpdateChannels.AddRange(
                new UpdateChannelDefinition { Code = "stable", DisplayName = "Стабильный", Description = "Продакшн-канал", IsDefault = true },
                new UpdateChannelDefinition { Code = "beta", DisplayName = "Бета", Description = "Предварительные релизы" },
                new UpdateChannelDefinition { Code = "internal", DisplayName = "Внутренний", Description = "Внутреннее тестирование" });
        }

        if (!await dbContext.TranslationProviderSettings.AnyAsync(x => x.ProviderCode == "yandex", cancellationToken))
        {
            dbContext.TranslationProviderSettings.Add(new TranslationProviderSettings
            {
                ProviderCode = "yandex",
                DisplayName = "Yandex Translate",
                IsEnabled = _yandex.Enabled,
                Endpoint = _yandex.Endpoint,
                LanguagesEndpoint = _yandex.LanguagesEndpoint,
                FolderId = _yandex.FolderId,
                SecretReference = _yandex.ApiKeyEnvVar,
                TimeoutSeconds = _yandex.TimeoutSeconds,
                MaxBatchCharacters = _yandex.MaxBatchCharacters,
                FailureThreshold = _yandex.FailureThreshold,
                CircuitBreakSeconds = _yandex.CircuitBreakSeconds,
                LastKnownStatus = _yandex.Enabled ? "configured" : "disabled"
            });
        }

        if (!await dbContext.TranslationGlossaries.AnyAsync(x => x.Scope == TranslationGlossaryScope.Game && x.Name == "Stellaris Core RU", cancellationToken))
        {
            dbContext.TranslationGlossaries.Add(new TranslationGlossary
            {
                Scope = TranslationGlossaryScope.Game,
                Name = "Stellaris Core RU",
                Description = "Базовые термины Stellaris для серверного pipeline перевода.",
                SourceLanguage = "en",
                TargetLanguage = "ru",
                IsActive = true,
                Priority = 50,
                TermsJson = TranslationJson.Serialize(new[]
                {
                    new TranslationGlossaryTermDto("Empire", "Империя"),
                    new TranslationGlossaryTermDto("Fleet", "Флот"),
                    new TranslationGlossaryTermDto("Starbase", "Звёздная база"),
                    new TranslationGlossaryTermDto("Ascension Perk", "Путь возвышения")
                }),
                FrozenTermsJson = TranslationJson.Serialize(new[] { "Stellaris", "Paradox" }),
                SkipTermsJson = TranslationJson.Serialize(new[] { "$NAME$", "[Root.GetName]" })
            });
        }

        var admin = await dbContext.AdminUsers.FirstOrDefaultAsync(x => x.UserName == _options.AdminUserName, cancellationToken);
        if (admin is null)
        {
            admin = new AdminUser
            {
                UserName = _options.AdminUserName,
                DisplayName = _options.AdminDisplayName,
                Email = _options.AdminEmail,
                Role = AdminRole.Administrator,
                IsActive = true
            };

            admin.PasswordHash = _passwordHasher.HashPassword(admin, _options.AdminPassword);
            dbContext.AdminUsers.Add(admin);
        }

        foreach (var demo in _options.DemoLicenses)
        {
            if (string.IsNullOrWhiteSpace(demo.RawLicenseKey))
            {
                continue;
            }

            var hash = licenseKeyProtector.Hash(demo.RawLicenseKey);
            var exists = await dbContext.Licenses.AnyAsync(x => x.LicenseKeyHash == hash, cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.Licenses.Add(new License
            {
                LicenseKeyHash = hash,
                LicenseKeyMasked = licenseKeyProtector.Mask(demo.RawLicenseKey),
                LookupPrefix = licenseKeyProtector.GetLookupPrefix(demo.RawLicenseKey),
                CustomerName = demo.CustomerName,
                CustomerEmail = demo.CustomerEmail,
                Type = Enum.TryParse<LicenseType>(demo.Type, true, out var type) ? type : LicenseType.Subscription,
                State = LicenseState.Active,
                MaxDevices = demo.MaxDevices,
                OfflineGracePeriodHours = demo.OfflineGracePeriodHours,
                ExpiresUtc = demo.ExpiresUtc,
                UpdateChannel = Enum.TryParse<UpdateChannelCode>(demo.UpdateChannel, true, out var channel) ? channel : UpdateChannelCode.Stable,
                Notes = "Демо-лицензия из seed-настроек."
            });

            logger.LogInformation("Добавлена demo license для {CustomerEmail}", demo.CustomerEmail);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var existingLicenseIds = await dbContext.Licenses.Select(x => x.Id).ToListAsync(cancellationToken);
        var quotaLicenseIds = await dbContext.TranslationQuotas.Select(x => x.LicenseId).ToListAsync(cancellationToken);
        foreach (var licenseId in existingLicenseIds.Except(quotaLicenseIds))
        {
            dbContext.TranslationQuotas.Add(new TranslationQuota
            {
                LicenseId = licenseId,
                MaxFilesPerJob = 64,
                MaxSegmentsPerJob = 4000,
                MaxCharactersPerJob = 120000,
                MaxCharactersPerDay = 480000,
                MaxConcurrentJobs = 2,
                MaxJobsPerHour = 10,
                MaxAnalysisPerHour = 20,
                IsEnabled = true,
                Notes = "Seed quota"
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
