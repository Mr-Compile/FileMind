using System.Text.Json;
using FileMind.Application.Decisions;
using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Profiles;

public enum ProfileImportPolicy { Merge, Skip }

public sealed record ProfilePreferences(string? Mode, double? HighThreshold, double? MediumThreshold, string? ConflictPolicy);

public sealed record ProfileDiff(string Section, string Id, string Kind); // Kind: added | changed | same

public sealed record ProfileImportPreview(
    int SchemaVersion,
    IReadOnlyList<ProfileDiff> Diff,
    IReadOnlyList<string> Warnings);

public sealed record ProfileBackup(string Path, DateTimeOffset At);

public sealed record ProfileImportResult(int Applied, int Skipped, IReadOnlyList<string> Warnings);

/// <summary>
/// PLAN-012 portable profile orchestration. Exports only portable configuration
/// (rules, collections, subjects, projects, exclusions, portable preferences);
/// platform-only settings (paths, watched folders, per-folder modes, AI keys)
/// never enter organizer-profile.json. Import never touches user files.
/// </summary>
public sealed class ProfileService(
    IRuleRepository rules,
    ICollectionRepository collections,
    ISubjectRepository subjects,
    IProjectRepository projects,
    IExclusionRepository exclusions,
    ISettingsRepository settings,
    IProfileRepository profileDump,
    Func<string, string, CancellationToken, Task<Result<bool>>>? writeText = null,
    Func<string, CancellationToken, Task<Result<string>>>? readText = null)
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static FileMindError V(string m) => FileMindError.Validation(m);

    // ---------- Export ----------

    /// <summary>Builds the portable profile document (no platform-only keys).</summary>
    public async Task<Result<string>> ExportAsync(CancellationToken ct = default)
    {
        var r = await rules.GetAllAsync(ct); if (!r.IsSuccess) return Result<string>.Fail(r.Error);
        var c = await collections.GetAllAsync(ct); if (!c.IsSuccess) return Result<string>.Fail(c.Error);
        var s = await subjects.GetAllAsync(ct); if (!s.IsSuccess) return Result<string>.Fail(s.Error);
        var p = await projects.GetAllAsync(ct); if (!p.IsSuccess) return Result<string>.Fail(p.Error);
        var x = await exclusions.GetPatternsAsync(ct); if (!x.IsSuccess) return Result<string>.Fail(x.Error);

        var mode = await settings.GetAsync(ModePolicy.SettingsKey, ct);
        // Canonical schema form ("AI-Assisted"), not the lowercase storage form.
        var modeName = mode is { IsSuccess: true, Value: not null }
            && ModePolicy.TryParse(mode.Value, out var parsed)
            ? parsed switch
            {
                OrganizationMode.AiAssisted => "AI-Assisted",
                OrganizationMode.LocalOnly => "Local-Only",
                _ => parsed.ToString()
            }
            : null;

        var doc = new
        {
            schemaVersion = CurrentSchemaVersion,
            rules = r.Value,
            collections = c.Value,
            subjects = s.Value,
            projects = p.Value,
            namingTemplates = new Dictionary<string, string>(),
            exclusions = x.Value,
            preferences = modeName is not null
                ? new ProfilePreferences(modeName, null, null, null)
                : null
        };
        return Result<string>.Ok(JsonSerializer.Serialize(doc, Json));
    }

    // ---------- Validate / parse ----------

    /// <summary>Typed-error parse: malformed JSON, wrong schemaVersion, missing required fields.</summary>
    public static Result<JsonDocument> ParseDocument(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException e) { return Result<JsonDocument>.Fail(V($"Malformed JSON: {e.Message}")); }

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return Result<JsonDocument>.Fail(V("root must be an object"));
        if (!root.TryGetProperty("schemaVersion", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var ver) || ver < 1)
            return Result<JsonDocument>.Fail(V("schemaVersion must be an integer >= 1"));
        if (ver > CurrentSchemaVersion)
            return Result<JsonDocument>.Fail(V($"schemaVersion {ver} is newer than supported {CurrentSchemaVersion}"));

        foreach (var (prop, kind) in new (string, JsonValueKind)[] {
            ("rules", JsonValueKind.Array), ("collections", JsonValueKind.Array), ("subjects", JsonValueKind.Array),
            ("projects", JsonValueKind.Array), ("exclusions", JsonValueKind.Array), ("namingTemplates", JsonValueKind.Object),
            ("preferences", JsonValueKind.Object) })
            if (root.TryGetProperty(prop, out var el) && el.ValueKind != kind)
                return Result<JsonDocument>.Fail(V($"{prop} must be {kind}"));

        foreach (var section in new[] { "collections", "subjects", "projects" })
            if (root.TryGetProperty(section, out var arr))
                foreach (var item in arr.EnumerateArray())
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                        !item.TryGetProperty("name", out var nm) || nm.ValueKind != JsonValueKind.String)
                        return Result<JsonDocument>.Fail(V($"{section} entries require string id and name"));
        return Result<JsonDocument>.Ok(doc);
    }

    // ---------- Preview ----------

    public async Task<Result<ProfileImportPreview>> PreviewAsync(string json, CancellationToken ct = default)
    {
        var parsed = ParseDocument(json);
        if (!parsed.IsSuccess) return Result<ProfileImportPreview>.Fail(parsed.Error);
        using var doc = parsed.Value;
        var root = doc.RootElement;
        var warnings = new List<string>();
        var diff = new List<ProfileDiff>();

        async Task Section<T>(string name, Func<Task<Result<IReadOnlyList<T>>>> local, Func<T, string> idOf) where T : notnull
        {
            if (!root.TryGetProperty(name, out var arr)) return;
            var cur = await local();
            if (!cur.IsSuccess) { warnings.Add($"{name}: local read failed"); return; }
            var byId = cur.Value.ToDictionary(idOf);
            foreach (var el in arr.EnumerateArray())
            {
                T? incoming;
                try { incoming = el.Deserialize<T>(Json); }
                catch (JsonException) { warnings.Add($"{name}: skipped unreadable entry"); continue; }
                if (incoming is null) { warnings.Add($"{name}: skipped unreadable entry"); continue; }
                var id = idOf(incoming);
                diff.Add(!byId.TryGetValue(id, out var cur2)
                    ? new(name, id, "added")
                    : new(name, id, JsonSerializer.Serialize(cur2, Json) == JsonSerializer.Serialize(incoming, Json) ? "same" : "changed"));
            }
        }

        await Section<Rule>("rules", () => rules.GetAllAsync(ct), x => x.Id);
        await Section<CollectionEntry>("collections", () => collections.GetAllAsync(ct), x => x.Id);
        await Section<SubjectLike>("subjects", () => subjects.GetAllAsync(ct), x => x.Id);
        await Section<ProjectLike>("projects", () => projects.GetAllAsync(ct), x => x.Id);
        if (root.TryGetProperty("exclusions", out var ex))
        {
            var cur = await exclusions.GetPatternsAsync(ct);
            if (cur.IsSuccess)
            {
                var set = cur.Value.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var e in ex.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String)
                        diff.Add(new("exclusions", e.GetString()!, set.Contains(e.GetString()!) ? "same" : "added"));
            }
        }
        return Result<ProfileImportPreview>.Ok(new(root.GetProperty("schemaVersion").GetInt32(), diff, warnings));
    }

    // ---------- Import ----------

    /// <summary>Apply a validated profile doc. Never touches user files. policy: Merge = upsert all, Skip = only new ids.</summary>
    public async Task<Result<ProfileImportResult>> ImportAsync(string json, ProfileImportPolicy policy, CancellationToken ct = default)
    {
        var parsed = ParseDocument(json);
        if (!parsed.IsSuccess) return Result<ProfileImportResult>.Fail(parsed.Error);
        using var doc = parsed.Value;
        var root = doc.RootElement;
        var warnings = new List<string>();
        var applied = 0;
        var skipped = 0;

        async Task UpsertSection<T>(string name, Func<Task<Result<IReadOnlyList<T>>>> local,
            Func<T, Task<Result<T>>> upsert, Func<T, string> idOf) where T : notnull
        {
            if (!root.TryGetProperty(name, out var arr)) return;
            var cur = await local();
            var existing = cur.IsSuccess ? cur.Value.Select(idOf).ToHashSet() : [];
            foreach (var el in arr.EnumerateArray())
            {
                T? item;
                try { item = el.Deserialize<T>(Json); }
                catch (JsonException) { warnings.Add($"{name}: skipped malformed entry"); continue; }
                if (item is null) { warnings.Add($"{name}: skipped malformed entry"); continue; }
                if (policy == ProfileImportPolicy.Skip && existing.Contains(idOf(item))) { skipped++; continue; }
                var res = await upsert(item);
                if (res.IsSuccess) applied++;
                else warnings.Add($"{name}:{idOf(item)}: {res.Error!.Message}");
            }
        }

        await UpsertSection<Rule>("rules", () => rules.GetAllAsync(ct), x => rules.UpsertAsync(x, ct), x => x.Id);
        await UpsertSection<CollectionEntry>("collections", () => collections.GetAllAsync(ct), x => collections.UpsertAsync(x, ct), x => x.Id);
        await UpsertSection<SubjectLike>("subjects", () => subjects.GetAllAsync(ct), x => subjects.UpsertAsync(x, ct), x => x.Id);
        await UpsertSection<ProjectLike>("projects", () => projects.GetAllAsync(ct), x => projects.UpsertAsync(x, ct), x => x.Id);

        if (root.TryGetProperty("exclusions", out var ex))
        {
            var cur = await exclusions.GetPatternsAsync(ct);
            var set = cur.IsSuccess ? cur.Value.ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in ex.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String) { warnings.Add("exclusions: skipped non-string"); continue; }
                var pat = e.GetString()!;
                if (policy == ProfileImportPolicy.Skip && set.Contains(pat)) { skipped++; continue; }
                var res = await exclusions.AddAsync(pat, ct);
                if (res.IsSuccess) applied++; else warnings.Add($"exclusions:{pat}: {res.Error!.Message}");
            }
        }

        // Portable preferences only — never platform keys. Stored in the
        // canonical enum-name form used by the `mode` verb.
        if (root.TryGetProperty("preferences", out var prefs) && prefs.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String)
        {
            var raw = m.GetString()!;
            if (!ModePolicy.TryParse(raw, out var parsedMode))
                warnings.Add($"preferences.mode: unknown mode '{raw}' — skipped");
            else
            {
                var res = await settings.SetAsync(ModePolicy.SettingsKey, parsedMode.ToString(), ct);
                if (!res.IsSuccess) warnings.Add($"preferences.mode: {res.Error!.Message}");
            }
        }

        return Result<ProfileImportResult>.Ok(new(applied, skipped, warnings));
    }

    // ---------- Backup / restore ----------

    /// <summary>Full-fidelity snapshot (portable doc + settings) for same-device restore.</summary>
    public async Task<Result<ProfileBackup>> BackupAsync(string dir, CancellationToken ct = default)
    {
        var dump = await profileDump.ExportJsonAsync(ct);
        if (!dump.IsSuccess) return Result<ProfileBackup>.Fail(dump.Error);
        var at = DateTimeOffset.UtcNow;
        var path = Path.Combine(dir, $"profile-backup-{at:yyyyMMddHHmmss}.json");
        if (writeText is null) return Result<ProfileBackup>.Fail(V("no file writer configured"));
        var w = await writeText(path, dump.Value, ct);
        if (!w.IsSuccess) return Result<ProfileBackup>.Fail(w.Error);
        return Result<ProfileBackup>.Ok(new(path, at));
    }

    /// <summary>Restore a backup or import a profile document; portable sections only.</summary>
    public async Task<Result<ProfileImportResult>> RestoreAsync(string path, CancellationToken ct = default)
    {
        if (readText is null) return Result<ProfileImportResult>.Fail(V("no file reader configured"));
        var r = await readText(path, ct);
        if (!r.IsSuccess) return Result<ProfileImportResult>.Fail(r.Error);
        return await ImportAsync(r.Value, ProfileImportPolicy.Merge, ct);
    }
}
