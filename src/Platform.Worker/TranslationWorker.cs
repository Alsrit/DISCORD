using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Services.Translations;

namespace Platform.Worker;

public sealed class TranslationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<TranslationOptions> options,
    ILogger<TranslationWorker> logger) : BackgroundService
{
    private readonly TranslationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.QueuePollIntervalSeconds)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка worker обработки translation jobs.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task ProcessCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<ITranslationJobProcessor>();
        await processor.ProcessNextAsync(cancellationToken);
    }
}
