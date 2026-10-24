using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Intelligence.AI;

public sealed record GroqChatResult(
    string Content,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    string Model);

/// <summary>
/// Low-level Groq chat-completions client (PLAN-009 M1.1): bounded timeouts,
/// retry honoring retry-after, 429 circuit breaker, x-ratelimit header
/// reconciliation sink, and usage-ledger writes. Never throws into callers —
/// all failures return typed Result errors.
/// </summary>
public sealed class GroqChatClient(
    HttpClient http,
    GroqOptions options,
    ISecretStore? secrets,
    IClock clock,
    IAiUsageLedger? ledger = null,
    Action<RateLimitSnapshot>? rateLimitSink = null)
{
    private DateTimeOffset _breakerUntil = DateTimeOffset.MinValue;
    private bool _authFailed;
    private string _keySource = "none";

    public bool IsAvailable => !_authFailed && clock.UtcNow >= _breakerUntil;
    public string Model => options.Model;
    public string KeySource => _keySource;
    public DateTimeOffset? BreakerUntil => _breakerUntil == DateTimeOffset.MinValue ? null : _breakerUntil;

    /// <summary>Resolves the API key without making a network call; reports the source used.</summary>
    public async Task<(string? Key, string Source)> ResolveKeyAsync(CancellationToken ct = default)
    {
        if (options.HasKey) return (options.ApiKey, "options");
        if (Environment.GetEnvironmentVariable(GroqOptions.ApiKeyEnvVar) is { Length: > 0 } env)
            return (env, "env/.env");
        if (secrets is not null)
        {
            var stored = await secrets.GetAsync(GroqOptions.SecretStoreKey, ct);
            if (stored.IsSuccess && stored.Value is { Length: > 0 }) return (stored.Value, "secret-store");
        }
        return (null, "none");
    }

    /// <summary>Conservative per-request token estimate for budget pre-flight.</summary>
    public int EstimateTokens(int payloadChars) =>
        Math.Min(options.MaxCompletionTokens + payloadChars / 4 + 64, 8192);

    public async Task<Result<GroqChatResult>> CompleteJsonAsync(
        string systemPrompt, string userPayload, string purpose, string requestHash,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
            return Fail(ErrorCode.AiOffline,
                _authFailed ? "Groq API key was rejected; update it via .env or `filemind ai key set`."
                            : $"AI circuit breaker open until {_breakerUntil:HH:mm:ss}Z.");

        var (key, source) = await ResolveKeyAsync(ct);
        if (key is null)
            return Fail(ErrorCode.AiOffline,
                "No Groq API key — set GROQ_API_KEY (env or .env) or `filemind ai key set <key>`.");
        _keySource = source;

        var body = JsonSerializer.Serialize(new GroqRequest(
            options.Model,
            options.Temperature,
            options.MaxCompletionTokens,
            new ResponseFormat("json_object"),
            [new GroqMessage("system", systemPrompt), new GroqMessage("user", userPayload)]),
            GroqJson.Options);

        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(options.Timeout);
            HttpResponseMessage? response = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                response = await http.SendAsync(request, linked.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Result<GroqChatResult>.Fail(FileMindError.Cancelled());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                await RecordUsage(requestHash, purpose, 0, 0, "offline", ct);
                if (attempt < options.MaxRetries)
                {
                    await Task.Delay(Backoff(attempt), ct);
                    continue;
                }
                return Fail(ErrorCode.AiOffline,
                    $"Groq unreachable: {ex.Message}", retrySafe: true);
            }

            var snapshot = ReadRateLimits(response);
            if (snapshot is not null) rateLimitSink?.Invoke(snapshot);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _authFailed = true;
                await RecordUsage(requestHash, purpose, 0, 0, "auth-failed", ct);
                return Fail(ErrorCode.AiOffline,
                    $"Groq rejected the API key ({(int)response.StatusCode}). Update it via .env or `filemind ai key set`.");
            }

            if (response.StatusCode is HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500)
            {
                var retryAfter = ReadRetryAfter(response);
                var isRateLimit = response.StatusCode is HttpStatusCode.TooManyRequests;
                await RecordUsage(requestHash, purpose, 0, 0, isRateLimit ? "rate-limited" : "server-error", ct);
                if (attempt < options.MaxRetries && retryAfter <= options.RetryAfterCeiling)
                {
                    response.Dispose();
                    await Task.Delay(retryAfter ?? Backoff(attempt), ct);
                    continue;
                }
                if (isRateLimit) _breakerUntil = clock.UtcNow + (retryAfter ?? TimeSpan.FromMinutes(1));
                response.Dispose();
                return isRateLimit
                    ? Fail(ErrorCode.AiRateLimited,
                        $"Groq rate-limited the request; retry after {retryAfter ?? TimeSpan.FromMinutes(1)}.")
                    : Fail(ErrorCode.AiOffline,
                        $"Groq server error ({(int)response.StatusCode}).", retrySafe: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode;
                response.Dispose();
                await RecordUsage(requestHash, purpose, 0, 0, $"http-{code}", ct);
                return Fail(ErrorCode.AiOffline, $"Groq returned HTTP {code}.", retrySafe: false);
            }

            GroqResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<GroqResponse>(GroqJson.Options, ct);
            }
            catch (JsonException ex)
            {
                await RecordUsage(requestHash, purpose, 0, 0, "invalid-response", ct);
                return Fail(ErrorCode.AiInvalidResponse,
                    $"Groq response was not valid JSON: {ex.Message}");
            }
            finally { response.Dispose(); }

            var choice = parsed?.Choices?.FirstOrDefault();
            var usage = parsed?.Usage;
            if (choice?.Message?.Content is not { Length: > 0 } content)
            {
                await RecordUsage(requestHash, purpose, usage?.PromptTokens ?? 0, usage?.CompletionTokens ?? 0, "invalid-response", ct);
                return Fail(ErrorCode.AiInvalidResponse,
                    "Groq response contained no message content.");
            }

            await RecordUsage(requestHash, purpose,
                usage?.PromptTokens ?? 0, usage?.CompletionTokens ?? 0, "ok", ct);
            return Result<GroqChatResult>.Ok(new GroqChatResult(
                content, usage?.PromptTokens ?? 0, usage?.CompletionTokens ?? 0,
                usage?.TotalTokens ?? 0, parsed!.Model ?? options.Model));
        }

        return Fail(ErrorCode.AiOffline, "Groq request exhausted retries.", retrySafe: true);
    }

    private async Task RecordUsage(string hash, string purpose, int prompt, int completion, string outcome, CancellationToken ct)
    {
        if (ledger is null) return;
        try
        {
            await ledger.RecordAsync(new AiUsageRecord(
                hash, options.Model, purpose, prompt, completion, outcome, clock.UtcNow), ct);
        }
        catch { /* ledger failures never break the caller */ }
    }

    private TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(500 * (attempt + 1));

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response) =>
        response.Headers.RetryAfter?.Delta
        ?? (response.Headers.RetryAfter?.Date is { } d ? d - DateTimeOffset.UtcNow : null);

    private static RateLimitSnapshot? ReadRateLimits(HttpResponseMessage response)
    {
        int? reqDay = HeaderInt(response, "x-ratelimit-remaining-requests");
        int? tokMin = HeaderInt(response, "x-ratelimit-remaining-tokens");
        return reqReqOrNull(reqDay, tokMin);

        static RateLimitSnapshot? reqReqOrNull(int? r, int? t) =>
            r is null && t is null ? null : new RateLimitSnapshot(r, t, DateTimeOffset.UtcNow);
    }

    private static int? HeaderInt(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) &&
        int.TryParse(values.FirstOrDefault(), out var v) ? v : null;

    private static Result<GroqChatResult> Fail(ErrorCode code, string message, bool retrySafe = false) =>
        Result<GroqChatResult>.Fail(new FileMindError(code, message, retrySafe, RequiresReview: false));

    // ---- wire DTOs ----
    private sealed record GroqMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ResponseFormat(
        [property: JsonPropertyName("type")] string Type);

    private sealed record GroqRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_completion_tokens")] int MaxCompletionTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat,
        [property: JsonPropertyName("messages")] GroqMessage[] Messages);

    private sealed class GroqResponse
    {
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
        [JsonPropertyName("usage")] public Usage? Usage { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public Msg? Message { get; set; }
    }

    private sealed class Msg
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }
}

internal static class GroqJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
