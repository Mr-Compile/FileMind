using System.Text.Json;
using FileMind.Application.Search;
using FileMind.Domain.Errors;

namespace FileMind.Intelligence.AI;

/// <summary>
/// NL → rule draft. Produces a JSON rule *draft* — validated by the caller
/// through `RuleValidator` and always user-confirmed before import.
/// </summary>
public static class AiRuleDrafter
{
    private const string SystemPrompt =
        "Draft a FileMind organization rule from the user's sentence. Reply with ONLY a JSON object: " +
        "{\"name\":string,\"conditions\":string (human-readable summary),\"actions\":string,\"extensions\":[string],\"confidence\":number 0-100}";

    public static async Task<Result<JsonDocument>> DraftAsync(
        GroqProvider provider, string naturalLanguage, CancellationToken ct = default) =>
        await provider.CompleteJsonAsync(SystemPrompt,
            JsonSerializer.Serialize(new { request = naturalLanguage }), "rule-draft", ct);
}

/// <summary>
/// NL → structured SearchQuery. Advisory interpretation only — retrieval is
/// always local FTS; a failed/absent response degrades to plain text search.
/// </summary>
public static class AiQueryInterpreter
{
    private const string SystemPrompt =
        "Interpret the user's file search. Reply with ONLY a JSON object: " +
        "{\"text\":string|null,\"nameContains\":string|null,\"extension\":string|null," +
        "\"kind\":string|null (Document|Image|Video|Audio|Archive|Code|Executable|Font|Other)," +
        "\"olderThanDays\":number|null,\"newerThanDays\":number|null}";

    public static async Task<Result<SearchQuery>> InterpretAsync(
        GroqProvider provider, string naturalLanguage, CancellationToken ct = default)
    {
        var r = await provider.CompleteJsonAsync(SystemPrompt,
            JsonSerializer.Serialize(new { query = naturalLanguage }), "search-interpret", ct);
        if (!r.IsSuccess) return Result<SearchQuery>.Fail(r.Error!);
        try
        {
            var e = r.Value!.RootElement;
            string? S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;
            int? N(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32() : null;
            var q = new SearchQuery(
                Text: S("text"), NameContains: S("nameContains"), Extension: S("extension"),
                Kind: S("kind"), OlderThanDays: N("olderThanDays"), NewerThanDays: N("newerThanDays"));
            return Result<SearchQuery>.Ok(q);
        }
        catch (Exception ex)
        {
            return Result<SearchQuery>.Fail(
                new FileMindError(ErrorCode.AiInvalidResponse, ex.Message, false, false));
        }
    }
}
