using System.Net.Http.Json;
using System.Text.Json;
using Platform.Application.Models;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class ClientTranslationApiService(
    ClientSettingsStore settingsStore,
    SecureTokenStore tokenStore,
    ClientPathService pathService,
    PinnedHttpClientFactory httpClientFactory,
    ClientApiService clientApiService,
    ClientLogService logService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TranslationApiResult<IReadOnlyCollection<LanguageOptionDto>>> GetLanguagesAsync(string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<IReadOnlyCollection<LanguageOptionDto>>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.GetAsync("api/client/v1/languages", cancellationToken);
            return await ReadCollectionAsync<LanguageOptionDto>(response, "Не удалось получить список языков.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<TranslationQuotaStatusDto>> GetCurrentQuotaAsync(string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<TranslationQuotaStatusDto>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.GetAsync("api/client/v1/quotas/current", cancellationToken);
            return await ReadPayloadAsync<TranslationQuotaStatusDto>(response, "Не удалось получить текущую квоту.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<IReadOnlyCollection<ActiveGlossaryDto>>> GetActiveGlossariesAsync(string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<IReadOnlyCollection<ActiveGlossaryDto>>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.GetAsync("api/client/v1/glossaries/active", cancellationToken);
            return await ReadCollectionAsync<ActiveGlossaryDto>(response, "Не удалось загрузить активные словари.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<AnalyzeModResponse>> AnalyzeAsync(AnalyzeModRequest request, string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<AnalyzeModResponse>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.PostAsJsonAsync("api/client/v1/mods/analyze", request, cancellationToken);
            return await ReadPayloadAsync<AnalyzeModResponse>(response, "Не удалось выполнить анализ мода.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<TranslationJobCreatedResponse>> CreateJobAsync(
        CreateTranslationJobRequest request,
        string clientVersion,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<TranslationJobCreatedResponse>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "api/client/v1/translations/jobs")
            {
                Content = JsonContent.Create(request)
            };
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                message.Headers.Add("Idempotency-Key", idempotencyKey);
            }

            using var response = await client.SendAsync(message, cancellationToken);
            return await ReadPayloadAsync<TranslationJobCreatedResponse>(response, "Не удалось создать задание перевода.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<TranslationJobStatusDto>> GetJobAsync(Guid jobId, string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<TranslationJobStatusDto>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.GetAsync($"api/client/v1/translations/jobs/{jobId}", cancellationToken);
            return await ReadPayloadAsync<TranslationJobStatusDto>(response, "Не удалось получить статус задания.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<IReadOnlyCollection<TranslationFileResultDto>>> GetJobFilesAsync(Guid jobId, string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<IReadOnlyCollection<TranslationFileResultDto>>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.GetAsync($"api/client/v1/translations/jobs/{jobId}/files", cancellationToken);
            return await ReadCollectionAsync<TranslationFileResultDto>(response, "Не удалось получить список файлов результата.", cancellationToken);
        }
    }

    public async Task<TranslationApiResult<string>> DownloadJobResultAsync(Guid jobId, string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult<string>.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.GetAsync($"api/client/v1/translations/jobs/{jobId}/download", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await ReadProblemAsync<string>(response, "Не удалось скачать архив результата.", cancellationToken);
            }

            pathService.EnsureFolders();
            var destinationPath = Path.Combine(pathService.TranslationDownloadsPath, $"{jobId:N}.zip");
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(destinationPath);
            await source.CopyToAsync(target, cancellationToken);
            return TranslationApiResult<string>.Success(destinationPath, "Архив результата скачан.");
        }
    }

    public async Task<TranslationApiResult> CancelJobAsync(Guid jobId, string clientVersion, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(clientVersion, cancellationToken);
        if (client is null)
        {
            return TranslationApiResult.Failure("Сессия клиента недоступна.", "session_not_found", 401);
        }

        using (client)
        {
            using var response = await client.PostAsync($"api/client/v1/translations/jobs/{jobId}/cancel", content: null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<MessageEnvelope>(JsonOptions, cancellationToken);
                return TranslationApiResult.Success(payload?.Message ?? "Запрос на отмену отправлен.");
            }

            var failure = await ReadProblemAsync<MessageEnvelope>(response, "Не удалось отменить задание.", cancellationToken);
            return TranslationApiResult.Failure(failure.Message, failure.ErrorCode, failure.StatusCode);
        }
    }

    private async Task<HttpClient?> CreateAuthorizedClientAsync(string clientVersion, CancellationToken cancellationToken)
    {
        if (!await clientApiService.EnsureAuthorizedAsync(clientVersion, cancellationToken))
        {
            return null;
        }

        var session = tokenStore.Load();
        if (session is null)
        {
            return null;
        }

        var settings = settingsStore.Load();
        return httpClientFactory.Create(settings, session.AccessToken);
    }

    private async Task<TranslationApiResult<T>> ReadPayloadAsync<T>(
        HttpResponseMessage response,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return await ReadProblemAsync<T>(response, fallbackMessage, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload is null
            ? TranslationApiResult<T>.Failure(fallbackMessage, "empty_payload", (int)response.StatusCode)
            : TranslationApiResult<T>.Success(payload, fallbackMessage);
    }

    private async Task<TranslationApiResult<IReadOnlyCollection<T>>> ReadCollectionAsync<T>(
        HttpResponseMessage response,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return await ReadProblemAsync<IReadOnlyCollection<T>>(response, fallbackMessage, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<T>>(JsonOptions, cancellationToken);
        return TranslationApiResult<IReadOnlyCollection<T>>.Success(payload ?? Array.Empty<T>(), fallbackMessage);
    }

    private async Task<TranslationApiResult<T>> ReadProblemAsync<T>(
        HttpResponseMessage response,
        string fallbackMessage,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ClientProblemDetails? problem = null;

        try
        {
            problem = JsonSerializer.Deserialize<ClientProblemDetails>(body, JsonOptions);
        }
        catch
        {
            // Keep fallback text below.
        }

        var message = problem?.Detail ?? problem?.Title ?? fallbackMessage;
        logService.Write("Warning", "Перевод", message, new { statusCode = (int)response.StatusCode, body });
        return TranslationApiResult<T>.Failure(message, problem?.ErrorCode, (int)response.StatusCode);
    }

    private sealed record ClientProblemDetails(
        string? Title,
        string? Detail,
        int? Status,
        string? ErrorCode);

    private sealed record MessageEnvelope(
        bool Success,
        string Message);
}
