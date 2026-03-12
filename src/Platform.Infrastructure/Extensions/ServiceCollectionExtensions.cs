using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Abstractions;
using Platform.Application.Services;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Services;

namespace Platform.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<ServerIdentityOptions>(configuration.GetSection(ServerIdentityOptions.SectionName));
        services.Configure<UpdateSigningOptions>(configuration.GetSection(UpdateSigningOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        var connectionString = configuration.GetConnectionString("PlatformDb")
            ?? throw new InvalidOperationException("Не задана строка подключения PlatformDb.");

        services.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(connectionString, builder => builder.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName)));

        services.AddMemoryCache();
        services.AddSingleton<IRedisConnectionAccessor, RedisConnectionAccessor>();

        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<ILicenseKeyProtector, LicenseKeyProtector>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRateLimitService, RateLimitService>();
        services.AddScoped<IUpdateSignatureService, UpdateSignatureService>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ISecurityIncidentService, SecurityIncidentService>();
        services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
        services.AddScoped<IClientPlatformService, ClientPlatformService>();
        services.AddScoped<IAdminPlatformService, AdminPlatformService>();
        services.AddScoped<IPlatformSeeder, PlatformSeeder>();

        return services;
    }
}
