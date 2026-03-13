using Microsoft.Extensions.Options;
using Platform.Infrastructure.Configuration;

namespace Platform.Infrastructure.Services.Translations;

internal sealed class TranslationQueueService(
    IRedisConnectionAccessor redisAccessor,
    IOptions<TranslationOptions> options) : ITranslationQueueService
{
    private readonly TranslationOptions _options = options.Value;

    public async Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var redis = redisAccessor.GetDatabase();
        if (redis is null)
        {
            return;
        }

        await redis.ListRightPushAsync(GetQueueKey(), jobId.ToString("N"));
    }

    public async Task<Guid?> TryDequeueAsync(CancellationToken cancellationToken)
    {
        var redis = redisAccessor.GetDatabase();
        if (redis is null)
        {
            return null;
        }

        var value = await redis.ListLeftPopAsync(GetQueueKey());
        return value.IsNullOrEmpty ? null : Guid.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    public async Task<long> GetQueuedCountAsync(CancellationToken cancellationToken)
    {
        var redis = redisAccessor.GetDatabase();
        if (redis is null)
        {
            return -1;
        }

        return await redis.ListLengthAsync(GetQueueKey());
    }

    private string GetQueueKey() => $"queue:{_options.QueueName}";
}
