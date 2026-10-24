using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.WinUI.Services;

namespace FileMind.WinUI.Tests;

/// <summary>
/// PLAN-014 M4 verification: the UI composition root must open the real
/// backend (SQLite, adapters, pipeline) end-to-end. XAML render is
/// verified separately on the desktop; these tests prove the wiring.
/// </summary>
public class BackendSmokeTests : IDisposable
{
    private readonly string _tmp =
        Path.Combine(Path.GetTempPath(), "fm-winui-tests-" + Guid.NewGuid().ToString("N")[..8]);

    public BackendSmokeTests()
    {
        Directory.CreateDirectory(_tmp);
        Environment.SetEnvironmentVariable("FILEMIND_DB",
            Path.Combine(_tmp, "filemind.db"));
    }

    [Fact]
    public async Task Backend_Opens_Real_Store_And_Pipeline()
    {
        await using var be = await FmBackend.OpenAsync();
        Assert.NotNull(be.Pipeline);
        Assert.NotNull(be.Search);
        Assert.NotNull(be.Store);
        Assert.NotNull(be.ModePolicy);
    }

    [Fact]
    public async Task Pipeline_Scan_Analyze_Plan_Execute_Undo_Roundtrip()
    {
        var src = Path.Combine(_tmp, "src");
        var dest = Path.Combine(_tmp, "dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dest);
        var file = Path.Combine(src, "IT421_notes.txt");
        await File.WriteAllTextAsync(file, "hello world");

        // Seed subject + collection BEFORE the backend opens: the classifier's
        // subject catalog is built in-memory at OpenAsync (mirrors seed-demo).
        var dbPath = Environment.GetEnvironmentVariable("FILEMIND_DB")!;
        using (var db = FileMind.Infrastructure.Sqlite.FileMindDb.Open(dbPath))
        {
            var seed = new FileMind.Infrastructure.Sqlite.SqliteStore(db);
            await ((FileMind.Application.Ports.ISubjectRepository)seed).UpsertAsync(
                new SubjectLike("IT421", "IT421 Networks", "IT421",
                    ["IT 421", "IT-421", "IT_421"], ["networks", "lecture"]));
            await ((FileMind.Application.Ports.ICollectionRepository)seed).UpsertAsync(
                new CollectionEntry("school", "School", "School/{Subject}", ["IT421"]));
        }

        await using var be = await FmBackend.OpenAsync();

        var candidates = new List<FileCandidate>();
        await foreach (var c in be.Pipeline.ScanAsync(src, recursive: true))
            candidates.Add(c);
        Assert.Single(candidates);

        var analyzed = await be.Pipeline.AnalyzeAsync(candidates);
        Assert.True(analyzed.IsSuccess, analyzed.Error?.Message);

        var plan = be.Pipeline.Plan(analyzed.Value!, dryRun: false,
            destinationRoot: dest, sourceRoot: src);
        Assert.True(plan.IsSuccess, plan.Error?.Message);

        var safety = await be.Pipeline.DryRunAsync(plan.Value!);
        Assert.True(safety.IsSuccess, safety.Error?.Message);
        Assert.True(safety.Value!.Allowed,
            string.Join("; ", safety.Value.Blockers));

        var record = await be.Pipeline.ExecuteAsync(plan.Value!);
        Assert.True(record.IsSuccess, record.Error?.Message);
        Assert.Equal(OperationStatus.Committed, record.Value!.Status);

        var undone = await be.Pipeline.UndoAsync(record.Value.OperationId);
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task Backend_Search_And_Recent_Operations_Surface()
    {
        var src = Path.Combine(_tmp, "src");
        Directory.CreateDirectory(src);
        await File.WriteAllTextAsync(Path.Combine(src, "alpha.txt"), "x");

        await using var be = await FmBackend.OpenAsync();
        var candidates = new List<FileCandidate>();
        await foreach (var c in be.Pipeline.ScanAsync(src, recursive: true))
            candidates.Add(c);
        var indexed = await be.Pipeline.IndexAsync(candidates);
        Assert.True(indexed.IsSuccess, indexed.Error?.Message);
        Assert.True(indexed.Value!.Indexed >= 1);

        var ops = await be.Pipeline.RecentOperationsAsync(5);
        Assert.True(ops.IsSuccess, ops.Error?.Message);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("FILEMIND_DB", null);
        try { Directory.Delete(_tmp, recursive: true); } catch { }
    }
}
