using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Models;
using Platform.Application.Services;
using Platform.Infrastructure.Configuration;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Services.Translations;

internal sealed class YandexTranslateProvider(
    HttpClient httpClient,
    PlatformDbContext dbContext,
    IRedisConnectionAccessor redisAccessor,
    IOptions<YandexTranslateOptions> options,
    ILogger<YandexTranslateProvider> logger) : ITranslationProvider
{
    public string ProviderCode => "yandex";

    private readonly YandexTranslateOptions _options = options.Value;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var provider = await dbContext.TranslationProviderSettings
            .FirstOrDefaultAsync(x => x.ProviderCode == ProviderCode, cancellationToken);

        if (!(_options.Enabled || provider?.IsEnabled == true))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(ResolveApiKey()) && !IsCircuitOpen();
    }

    public async Task<IReadOnlyCollection<LanguageOptionDto>> GetLanguagesAsync(CancellationToken cancellationToken)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            return
            [
                new("en", "English", true, true),
                new("ru", "Russian", true, true)
            ];
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.LanguagesEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", ResolveApiKey());
        request.Content = JsonContent.Create(new YandexLanguagesRequest(_options.FolderId));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Yandex language list request failed with {StatusCode}", response.StatusCode);
            return
            [
                new("en", "English", true, true),
                new("ru", "Russian", true, true)
            ];
        }

        var payload = await response.Content.ReadFromJsonAsync<YandexLanguagesResponse>(cancellationToken: cancellationToken);
        if (payload?.Languages is null || payload.Languages.Count == 0)
        {
            return
            [
                new("en", "English", true, true),
                new("ru", "Russian", true, true)
            ];
        }

        return payload.Languages
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new LanguageOptionDto(x.Code, x.Name, true, true))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<string>> TranslateAsync(
        string sourceLanguage,
        string targetLanguage,
        IReadOnlyCollection<string> texts,
        CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (!await IsAvailableAsync(cancellationToken))
        {
            throw new TranslationProviderException("Провайдер Yandex Translate недоступен или отключён.", "provider_unavailable");
        }

        var totalLength = texts.Sum(x => x.Length);
        if (totalLength > 10_000)
        {
            throw new TranslationProviderException("Провайдер Yandex Translate принимает не более 10000 символов в одном батче.", "provider_batch_too_large");
        }

        if (!await ConsumeThrottleAsync(cancellationToken))
        {
            throw new TranslationProviderException("Достигнут лимит запросов к провайдеру перевода.", "provider_throttled");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", ResolveApiKey());
        request.Content = JsonContent.Create(new YandexTranslateRequest(
            sourceLanguage,
            targetLanguage,
            "PLAIN_TEXT",
            texts,
            _options.FolderId));

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await RegisterFailureAsync(cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TranslationProviderException(
                    $"Yandex Translate вернул ошибку {(int)response.StatusCode}.",
                    "provider_http_error",
                    new HttpRequestException(body));
            }

            var payload = await response.Content.ReadFromJsonAsync<YandexTranslateResponse>(cancellationToken: cancellationToken);
            var translations = payload?.Translations?.Select(x => x.Text).ToArray() ?? Array.Empty<string>();
            if (translations.Length != texts.Count)
            {
                await RegisterFailureAsync(cancellationToken);
                throw new TranslationProviderException("Провайдер вернул неполный набор переведённых сегментов.", "provider_invalid_response");
            }

            await RegisterSuccessAsync(cancellationToken);
            return translations;
        }
        catch (TranslationProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RegisterFailureAsync(cancellationToken);
            throw new TranslationProviderException("Ошибка обращения к Yandex Translate.", "provider_request_failed", ex);
        }
    }

    private async Task<bool> ConsumeThrottleAsync(CancellationToken cancellationToken)
    {
        var bucket = $"translation-provider:{ProviderCode}:{DateTimeOffset.UtcNow:yyyyMMddHHmm}";
        var redis = redisAccessor.GetDatabase();
        if (redis is not null)
        {
            var value = await redis.StringIncrementAsync(bucket);
            if (value == 1)
            {
                await redis.KeyExpireAsync(bucket, TimeSpan.FromMinutes(1));
            }

            return value <= 120;
        }

        return true;
    }

    private bool IsCircuitOpen()
    {
        if (redisAccessor.MemoryCache.TryGetValue(GetCircuitKey(), out var cached) && cached is DateTimeOffset until)
        {
            return until > DateTimeOffset.UtcNow;
        }

        return false;
    }

    private async Task RegisterFailureAsync(CancellationToken cancellationToken)
    {
        var failureCountKey = $"{GetCircuitKey()}:count";
        var cache = redisAccessor.MemoryCache;
        var failures = cache.GetOrCreate(failureCountKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return 0;
        });

        failures++;
        cache.Set(failureCountKey, failures, TimeSpan.FromMinutes(10));
        if (failures >= _options.FailureThreshold)
        {
            cache.Set(GetCircuitKey(), DateTimeOffset.UtcNow.AddSeconds(_options.CircuitBreakSeconds), TimeSpan.FromSeconds(_options.CircuitBreakSeconds));
        }

        var provider = await dbContext.TranslationProviderSettings.FirstOrDefaultAsync(x => x.ProviderCode == ProviderCode, cancellationToken);
        if (provider is not null)
        {
            provider.LastKnownStatus = IsCircuitOpen() ? "circuit-open" : "error";
            provider.LastError = "Последний запрос к Yandex Translate завершился ошибкой.";
            provider.LastHealthCheckUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RegisterSuccessAsync(CancellationToken cancellationToken)
    {
        var cache = redisAccessor.MemoryCache;
        cache.Remove(GetCircuitKey());
        cache.Remove($"{GetCircuitKey()}:count");

        var provider = await dbContext.TranslationProviderSettings.FirstOrDefaultAsync(x => x.ProviderCode == ProviderCode, cancellationToken);
        if (provider is not null)
        {
            provider.LastKnownStatus = "ok";
            provider.LastError = string.Empty;
            provider.LastHealthCheckUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return _options.ApiKey;
        }

        return Environment.GetEnvironmentVariable(_options.ApiKeyEnvVar) ?? string.Empty;
    }

    private string GetCircuitKey() => $"provider-circuit:{ProviderCode}";

    private sealed record YandexTranslateRequest(
        [property: JsonPropertyName("sourceLanguageCode")] string SourceLanguageCode,
        [property: JsonPropertyName("targetLanguageCode")] string TargetLanguageCode,
        [property: JsonPropertyName("format")] string Format,
        [property: JsonPropertyName("texts")] IReadOnlyCollection<string> Texts,
        [property: JsonPropertyName("folderId")] string FolderId);

    private sealed record YandexTranslateResponse(
        [property: JsonPropertyName("translations")] IReadOnlyCollection<YandexTranslatedItem> Translations);

    private sealed record YandexTranslatedItem(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("detectedLanguageCode")] string? DetectedLanguageCode);

    private sealed record YandexLanguagesRequest(
        [property: JsonPropertyName("folderId")] string FolderId);

    private sealed record YandexLanguagesResponse(
        [property: JsonPropertyName("languages")] IReadOnlyCollection<YandexLanguageItem> Languages);

    private sealed record YandexLanguageItem(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("name")] string Name);
}
