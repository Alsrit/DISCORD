using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Platform.Infrastructure.Services;

public interface IRedisConnectionAccessor
{
    IDatabase? GetDatabase();

    IMemoryCache MemoryCache { get; }

    Task<bool> PingAsync(CancellationToken cancellationToken);
}

public sealed class RedisConnectionAccessor : IRedisConnectionAccessor
{
    private readonly Lazy<ConnectionMultiplexer?> _lazyConnection;
    private readonly ILogger<RedisConnectionAccessor> _logger;

    public RedisConnectionAccessor(
        IMemoryCache memoryCache,
        IOptions<RedisOptions> options,
        ILogger<RedisConnectionAccessor> logger)
    {
        MemoryCache = memoryCache;
        _logger = logger;

        _lazyConnection = new Lazy<ConnectionMultiplexer?>(() =>
        {
            if (!options.Value.Enabled || string.IsNullOrWhiteSpace(options.Value.Configuration))
            {
                return null;
            }

            try
            {
                return ConnectionMultiplexer.Connect(options.Value.Configuration);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось подключиться к Redis. Будет использован локальный fallback.");
                return null;
            }
        });
    }

    public IMemoryCache MemoryCache { get; }

    public IDatabase? GetDatabase() => _lazyConnection.Value?.GetDatabase();

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        var db = GetDatabase();
        if (db is null)
        {
            return false;
        }

        try
        {
            await db.PingAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Проверка Redis завершилась ошибкой.");
            return false;
        }
    }
}
