using Microsoft.Extensions.Caching.Memory;
using Platform.Application.Abstractions;
using StackExchange.Redis;

namespace Platform.Infrastructure.Services;

public sealed class RateLimitService(IRedisConnectionAccessor redisAccessor) : IRateLimitService
{
    public async Task<bool> ConsumeAsync(string bucket, string key, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        var normalizedKey = $"rate:{bucket}:{key}";
        var redis = redisAccessor.GetDatabase();

        if (redis is not null)
        {
            var value = await redis.StringIncrementAsync(normalizedKey);
            if (value == 1)
            {
                await redis.KeyExpireAsync(normalizedKey, window);
            }

            return value <= limit;
        }

        var cache = redisAccessor.MemoryCache;
        var count = cache.GetOrCreate(normalizedKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return 0;
        });

        count++;
        cache.Set(normalizedKey, count, window);
        return count <= limit;
    }
}
