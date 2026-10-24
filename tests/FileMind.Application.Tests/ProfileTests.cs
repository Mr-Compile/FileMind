using FileMind.Application.Decisions;
using FileMind.Application.Ports;
using FileMind.Application.Profiles;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Infrastructure.Sqlite;

namespace FileMind.Application.Tests;

public sealed class ProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-profile-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FileMindDb _db;
    private readonly SqliteStore _store;
    private readonly ProfileService _svc;
    private readonly List<(string Path, string Content)> _writes = new();

    public ProfileTests()
    {
        Directory.CreateDirectory(_root);
        _db = FileMindDb.Open(Path.Combine(_root, "test.db"));
        _store = new SqliteStore(_db);
        _svc = new ProfileService(
            (IRuleRepository)_store, (ICollectionRepository)_store, (ISubjectRepository)_store,
            (IProjectRepository)_store, (IExclusionRepository)_store, (ISettingsRepository)_store,
            (IProfileRepository)_store,
            writeText: (p, c, ct) =>
            {
                _writes.Add((p, c));
                return Task.FromResult(Result<bool>.Ok(true));
            },
            readText: (p, ct) =>
            {
                var w = _writes.FirstOrDefault(x => x.Path == p);
                return Task.FromResult(w.Content is null
                    ? Result<string>.Fail(FileMindError.NotFound(p))
                    : Result<string>.Ok(w.Content));
            });
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ISubjectRepository Subjects => (ISubjectRepository)_store;
    private IExclusionRepository Exclusions => (IExclusionRepository)_store;

    private async Task SeedAsync()
    {
        await Subjects.UpsertAsync(new SubjectLike("s1", "Math", "M101", ["maths"], ["algebra"]));
        await Exclusions.AddAsync("*.tmp");
        await ((ISettingsRepository)_store).SetAsync(ModePolicy.SettingsKey, "Smart");
        // platform-only setting — must NOT appear in export
        await ((ISettingsRepository)_store).SetAsync("watch.mode.C:/watched", "Automatic");
    }

    [Fact]
    public async Task Export_SchemaV1_PortableOnly_NoPlatformKeys()
    {
        await SeedAsync();
        var res = await _svc.ExportAsync();
        Assert.True(res.IsSuccess, res.Error?.Message);
        Assert.Contains("\"schemaVersion\": 1", res.Value!);
        Assert.Contains("Math", res.Value);
        Assert.Contains("*.tmp", res.Value);
        Assert.Contains("\"mode\": \"Smart\"", res.Value);
        Assert.DoesNotContain("watch.mode", res.Value);
        Assert.DoesNotContain("C:/watched", res.Value);
        var parsed = ProfileService.ParseDocument(res.Value!);
        Assert.True(parsed.IsSuccess);
    }

    [Fact]
    public void Parse_MalformedJson_Fails() =>
        Assert.False(ProfileService.ParseDocument("{ not json").IsSuccess);

    [Fact]
    public void Parse_NewerSchemaVersion_FailsSafe() =>
        Assert.False(ProfileService.ParseDocument("{\"schemaVersion\": 999}").IsSuccess);

    [Fact]
    public void Parse_MissingSchemaVersion_Fails() =>
        Assert.False(ProfileService.ParseDocument("{\"subjects\": []}").IsSuccess);

    [Fact]
    public async Task Preview_DiffsAddedChangedSame()
    {
        await SeedAsync();
        var json = """
        {"schemaVersion":1,
         "subjects":[
           {"id":"s1","name":"Math","code":"M101","aliases":["maths"],"keywords":["algebra"]},
           {"id":"s2","name":"Physics","code":null,"aliases":[],"keywords":["quantum"]},
           {"id":"s1b","name":"Math Renamed","code":"M101","aliases":[],"keywords":[]}],
         "exclusions":["*.tmp","*.bak"]}
        """;
        // s1 same, s2 added; s1b added (new id). exclusions: *.tmp same, *.bak added
        var prev = await _svc.PreviewAsync(json);
        Assert.True(prev.IsSuccess, prev.Error?.Message);
        var d = prev.Value!.Diff;
        Assert.Contains(d, x => x is { Section: "subjects", Id: "s1", Kind: "same" });
        Assert.Contains(d, x => x is { Section: "subjects", Id: "s2", Kind: "added" });
        Assert.Contains(d, x => x is { Section: "exclusions", Id: "*.tmp", Kind: "same" });
        Assert.Contains(d, x => x is { Section: "exclusions", Id: "*.bak", Kind: "added" });
        Assert.Empty(_writes); // preview never writes
    }

    [Fact]
    public async Task Preview_ChangedSubject_ShowsChanged()
    {
        await SeedAsync();
        var json = """{"schemaVersion":1,"subjects":[{"id":"s1","name":"Mathematics","code":null,"aliases":[],"keywords":[]}]}""";
        var prev = await _svc.PreviewAsync(json);
        Assert.Contains(prev.Value!.Diff, x => x is { Section: "subjects", Id: "s1", Kind: "changed" });
    }

    [Fact]
    public async Task Import_Merge_UpsertsExisting()
    {
        await SeedAsync();
        var json = """{"schemaVersion":1,"subjects":[{"id":"s1","name":"Mathematics","code":null,"aliases":[],"keywords":[]},{"id":"s2","name":"New","code":null,"aliases":[],"keywords":[]}]}""";
        var res = await _svc.ImportAsync(json, ProfileImportPolicy.Merge);
        Assert.True(res.IsSuccess, res.Error?.Message);
        Assert.Equal(2, res.Value!.Applied);
        var all = await Subjects.GetAllAsync();
        Assert.Equal(2, all.Value!.Count);
        Assert.Contains(all.Value, s => s is { Id: "s1", Name: "Mathematics" });
    }

    [Fact]
    public async Task Import_Skip_PreservesExisting()
    {
        await SeedAsync();
        var json = """{"schemaVersion":1,"subjects":[{"id":"s1","name":"Changed","code":null,"aliases":[],"keywords":[]},{"id":"s2","name":"New","code":null,"aliases":[],"keywords":[]}]}""";
        var res = await _svc.ImportAsync(json, ProfileImportPolicy.Skip);
        Assert.Equal(1, res.Value!.Applied);
        Assert.Equal(1, res.Value.Skipped);
        var s1 = (await Subjects.GetAllAsync()).Value!.Single(s => s.Id == "s1");
        Assert.Equal("Math", s1.Name);
    }

    [Fact]
    public async Task Import_PreferencesMode_UpdatesSetting_NotPlatformKeys()
    {
        await SeedAsync();
        var json = """{"schemaVersion":1,"preferences":{"mode":"Automatic"},"settings":{"watch.mode.X":"Smart"}}""";
        var res = await _svc.ImportAsync(json, ProfileImportPolicy.Merge);
        Assert.True(res.IsSuccess);
        var mode = await ((ISettingsRepository)_store).GetAsync(ModePolicy.SettingsKey);
        Assert.Equal("Automatic", mode.Value);
        var platformKey = await ((ISettingsRepository)_store).GetAsync("watch.mode.X");
        Assert.Null(platformKey.Value); // never applied
    }

    [Fact]
    public async Task Import_Malformed_NothingApplied()
    {
        await SeedAsync();
        var res = await _svc.ImportAsync("{ bad", ProfileImportPolicy.Merge);
        Assert.False(res.IsSuccess);
        var all = await Subjects.GetAllAsync();
        Assert.Single(all.Value!); // unchanged
    }

    [Fact]
    public async Task Import_NewerVersion_RejectedBeforeApply()
    {
        await SeedAsync();
        var res = await _svc.ImportAsync("{\"schemaVersion\":2,\"subjects\":[]}", ProfileImportPolicy.Merge);
        Assert.False(res.IsSuccess);
    }

    [Fact]
    public async Task Backup_WritesTimestampedDump_RestoreRoundTrips()
    {
        await SeedAsync();
        var b = await _svc.BackupAsync(_root);
        Assert.True(b.IsSuccess, b.Error?.Message);
        Assert.Contains("profile-backup-", b.Value!.Path);
        Assert.Single(_writes);

        // mutate, then restore
        await Subjects.UpsertAsync(new SubjectLike("s1", "Mutated", null, [], []));
        var r = await _svc.RestoreAsync(b.Value.Path);
        Assert.True(r.IsSuccess, r.Error?.Message);
        var s1 = (await Subjects.GetAllAsync()).Value!.Single(x => x.Id == "s1");
        Assert.Equal("Math", s1.Name);
    }

    [Fact]
    public async Task Import_TouchesNoUserFiles()
    {
        // The service only receives write/read delegates for backup I/O;
        // import itself performs no file operations at all.
        await SeedAsync();
        var res = await _svc.ImportAsync("""{"schemaVersion":1,"exclusions":["*.log"]}""", ProfileImportPolicy.Merge);
        Assert.True(res.IsSuccess);
        Assert.Empty(_writes);
        Assert.Contains("*.log", (await Exclusions.GetPatternsAsync()).Value!);
    }
}
