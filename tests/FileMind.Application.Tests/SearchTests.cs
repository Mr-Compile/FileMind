using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Search;
using FileMind.Domain.Contracts;
using FileMind.Infrastructure.Sqlite;
using FileMind.Testing;

namespace FileMind.Application.Tests;

public sealed class SearchTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-search-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FileMindDb _db;
    private readonly SqliteStore _store;
    private readonly ISearchIndex _fts;
    private readonly SearchUseCase _useCase;

    public SearchTests()
    {
        Directory.CreateDirectory(_root);
        _db = FileMindDb.Open(Path.Combine(_root, "test.db"));
        _store = new SqliteStore(_db);
        _fts = (ISearchIndex)_store;
        _useCase = new SearchUseCase(_fts, (IFileIndexRepository)_store);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private FileCandidate Cand(string path, string ext = "txt", long size = 100,
        DateTimeOffset? modified = null, string? text = null) =>
        Candidates.Create(path, extension: ext, size: size) with
        {
            ModifiedAt = modified ?? DateTimeOffset.UtcNow.AddDays(-10),
            ExtractedTextRef = text,
        };

    private async Task IndexAllAsync(params FileCandidate[] files) =>
        await new FileIndexingService((IFileIndexRepository)_store, searchIndex: _fts)
            .IndexAsync(files.ToList());

    [Fact]
    public void Migration_V3_CreatesFtsTable()
    {
        var names = _db.Query("SELECT name FROM sqlite_master WHERE name LIKE 'file_content%'",
            r => r.GetString(0)).ToList();
        Assert.Contains("file_content_index", names);
        var version = _db.Query("SELECT MAX(version) FROM schema_migrations", r => r.GetInt64(0)).Single();
        Assert.True(version >= 3);
    }

    [Fact]
    public async Task Index_ThenSearch_ByNameAndContent()
    {
        await IndexAllAsync(
            Cand("d:/f/quarterly report.pdf", "pdf"),
            Cand("d:/f/vacation.jpg", "jpg"),
            Cand("d:/f/notes.txt", text: "meeting agenda for the networks class"));

        var byName = (await _useCase.RunAsync(new SearchQuery(Text: "quarterly"))).Value!;
        Assert.Single(byName);
        Assert.Contains("quarterly report.pdf", byName[0].Path);

        var byContent = (await _useCase.RunAsync(new SearchQuery(Text: "agenda"))).Value!;
        Assert.Single(byContent);
        Assert.Contains("notes.txt", byContent[0].Path);
    }

    [Fact]
    public async Task Search_Filters_ExtKindAgeName()
    {
        var old = DateTimeOffset.UtcNow.AddDays(-200);
        await IndexAllAsync(
            Cand("d:/f/a report.txt", "txt", modified: old),
            Cand("d:/f/a report.pdf", "pdf"),
            Cand("d:/f/b report.txt", "txt"));

        var onlyTxt = (await _useCase.RunAsync(new SearchQuery(Text: "report", Extension: "txt"))).Value!;
        Assert.Equal(2, onlyTxt.Count);
        Assert.All(onlyTxt, i => Assert.EndsWith(".txt", i.Path));

        var onlyDocs = (await _useCase.RunAsync(new SearchQuery(Text: "report", Kind: "Document"))).Value!;
        Assert.Equal(3, onlyDocs.Count);

        var oldOnly = (await _useCase.RunAsync(new SearchQuery(Text: "report", OlderThanDays: 100))).Value!;
        Assert.Single(oldOnly);

        var named = (await _useCase.RunAsync(new SearchQuery(Text: "report", NameContains: "a report"))).Value!;
        Assert.Equal(2, named.Count);
    }

    [Fact]
    public async Task Search_NoText_FiltersStillWork()
    {
        await IndexAllAsync(Cand("d:/f/a.mp4", "mp4"), Cand("d:/f/b.txt", "txt"));
        var videos = (await _useCase.RunAsync(new SearchQuery(Kind: "Video"))).Value!;
        Assert.Single(videos);
        Assert.Equal("Video", videos[0].Kind);
    }

    [Fact]
    public async Task Search_UnsafeInput_EscapesSafely()
    {
        await IndexAllAsync(Cand("d:/f/x.txt"));
        var res = await _useCase.RunAsync(new SearchQuery(Text: "it's [weird] (NEAR) \"quoted\" OR"));
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value!); // escaped terms match nothing, never a syntax error
    }

    [Fact]
    public async Task Remove_ClearsBothIndexAndFts()
    {
        await IndexAllAsync(Cand("d:/f/doomed.txt", text: "ephemeral content"));
        var before = (await _useCase.RunAsync(new SearchQuery(Text: "ephemeral"))).Value!;
        Assert.Single(before);

        await ((IFileIndexRepository)_store).RemoveAsync("d:/f/doomed.txt");
        var after = (await _useCase.RunAsync(new SearchQuery(Text: "ephemeral"))).Value!;
        Assert.Empty(after);
    }

    [Fact]
    public async Task EmptyIndex_TruthfulEmpty()
    {
        var res = await _useCase.RunAsync(new SearchQuery(Text: "anything"));
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Value!);
    }

    [Fact]
    public async Task Indexing_WithoutSearchIndex_StillWorks()
    {
        var svc = new FileIndexingService((IFileIndexRepository)_store);
        var res = await svc.IndexAsync([Cand("d:/f/plain.txt")]);
        Assert.True(res.IsSuccess);
    }
}
