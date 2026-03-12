using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Services;
using Platform.Domain.Administration;
using Platform.Domain.Common;
using Platform.Domain.Licensing;
using Platform.Domain.Updates;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services;

public sealed class PlatformSeeder(
    PlatformDbContext dbContext,
    ILicenseKeyProtector licenseKeyProtector,
    IOptions<SeedOptions> options,
    ILogger<PlatformSeeder> logger) : IPlatformSeeder
{
    private const long SeedLockId = 5_837_219_114_203_317_921;

    private readonly SeedOptions _options = options.Value;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // A single advisory lock keeps API, admin panel and worker from seeding in parallel.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({SeedLockId})",
            cancellationToken);

        if (!await dbContext.UpdateChannels.AnyAsync(cancellationToken))
        {
            dbContext.UpdateChannels.AddRange(
                new UpdateChannelDefinition { Code = "stable", DisplayName = "Стабильный", Description = "Продакшн-канал", IsDefault = true },
                new UpdateChannelDefinition { Code = "beta", DisplayName = "Бета", Description = "Предварительные релизы" },
                new UpdateChannelDefinition { Code = "internal", DisplayName = "Внутренний", Description = "Внутреннее тестирование" });
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

            logger.LogInformation("Добавлена демо-лицензия для {CustomerEmail}", demo.CustomerEmail);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
