using System.Text.Json;
using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Intelligence.AI;

/// <summary>
/// <see cref="IAIProvider"/> orchestration over <see cref="GroqChatClient"/>:
/// payload-hash cache (hits spend zero quota) → pre-flight
/// <see cref="AiQuotaGate"/> → transport → per-request response shape
/// validation. Advisory only: every failure path returns a typed error —
/// never throws into the pipeline.
/// </summary>
public sealed class GroqProvider(
    GroqOptions options,
    GroqChatClient client,
    AiQuotaGate? quota = null,
    AiSuggestionCache? cache = null,
    IAiAuditRepository? audit = null,
    IClock? clock = null) : IAIProvider
{
    public bool IsAvailable => options.Enabled && client.IsAvailable;

    public async Task<Result<AiSuggestion>> SuggestAsync(AiRequestContext context, CancellationToken ct = default)
    {
        var requestHash = AiSuggestionCache.HashOf(context);
        if (cache?.TryGet(requestHash, out var hit) == true)
        {
            await AuditAsync(requestHash, "cache-hit", ct);
            return Result<AiSuggestion>.Ok(hit);
        }

        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            name = context.DisplayName,
            ext = context.Extension,
            metadata = context.Metadata,
            snippet = context.ExtractedSnippet,
        });
        const string system =
            "You are a file-organization classifier. Reply with ONLY a JSON object: " +
            "{\"collection\":string|null,\"category\":string|null,\"subject\":string|null," +
            "\"project\":string|null,\"suggestedName\":string|null,\"confidenceHint\":number 0-100," +
            "\"explanation\":string}. Never invent certainty; low confidence is honest.";

        var raw = await CompleteJsonAsync(system, payload, "classify", ct, requestHash);
        if (!raw.IsSuccess) return Result<AiSuggestion>.Fail(raw.Error!);
        try
        {
            var suggestion = MapSuggestion(raw.Value!.RootElement);
            if (suggestion is null)
                return Result<AiSuggestion>.Fail(new FileMindError(
                    ErrorCode.AiInvalidResponse, "Suggestion failed schema validation.", false, false));
            cache?.Store(requestHash, suggestion);
            return Result<AiSuggestion>.Ok(suggestion);
        }
        finally { raw.Value!.Dispose(); }
    }

    /// <summary>General structured completion — used by rule-draft and query-interpretation use cases.</summary>
    public async Task<Result<JsonDocument>> CompleteJsonAsync(
        string systemPrompt, string userPayload, string purpose,
        CancellationToken ct = default, string? requestHash = null)
    {
        if (!options.Enabled)
            return Result<JsonDocument>.Fail(new FileMindError(
                ErrorCode.AiOffline, "AI disabled (ai.enabled not set).", false, false));

        var hash = requestHash ?? Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(purpose + "|" + userPayload)))[..16];

        if (quota is not null)
        {
            var allowed = await quota.TryAcquireAsync(
                client.EstimateTokens(userPayload.Length), ct);
            if (!allowed.IsSuccess)
            {
                await AuditAsync(hash, "quota-blocked", ct);
                return Result<JsonDocument>.Fail(allowed.Error!);
            }
        }

        var r = await client.CompleteJsonAsync(systemPrompt, userPayload, purpose, hash, ct);
        if (!r.IsSuccess)
        {
            await AuditAsync(hash, r.Error!.Code.ToString(), ct);
            return Result<JsonDocument>.Fail(r.Error!);
        }
        if (quota is not null && clock is not null)
            quota.RecordCall(r.Value.PromptTokens, r.Value.CompletionTokens, clock.UtcNow);

        try
        {
            var doc = JsonDocument.Parse(r.Value.Content);
            await AuditAsync(hash, "ok", ct);
            return Result<JsonDocument>.Ok(doc);
        }
        catch (Exception ex)
        {
            await AuditAsync(hash, "invalid-response", ct);
            return Result<JsonDocument>.Fail(new FileMindError(
                ErrorCode.AiInvalidResponse, $"Response was not JSON: {ex.Message}", false, false));
        }
    }

    private static AiSuggestion? MapSuggestion(JsonElement r) =>
        r.ValueKind != JsonValueKind.Object ? null : new AiSuggestion(
            Str(r, "collection"), Str(r, "category"), Str(r, "subject"), Str(r, "project"),
            Str(r, "suggestedName"),
            r.TryGetProperty("confidenceHint", out var c) && c.ValueKind == JsonValueKind.Number
                ? Math.Clamp(c.GetDouble(), 0, 100) : 0,
            Str(r, "explanation") ?? "(no explanation)");

    private static string? Str(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private async Task AuditAsync(string hash, string outcome, CancellationToken ct)
    {
        if (audit is null) return;
        try
        {
            await audit.RecordAsync(hash, outcome, clock?.UtcNow ?? DateTimeOffset.UtcNow, ct);
        }
        catch { /* audit must never break the pipeline */ }
    }
}
