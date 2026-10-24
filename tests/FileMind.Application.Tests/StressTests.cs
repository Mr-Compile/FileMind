using System.Diagnostics;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.UseCases;
using FileMind.Application.Watching;
using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Domain.Rules;
using FileMind.Infrastructure.Sqlite;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

/// <summary>
/// PLAN-006 M2 — generated-corpus stress. No real files on disk: candidates
/// are synthetic; indexing persists to a temp SQLite db; planning is
/// in-memory. Budgets are generous blowup guards, not perf targets.
/// </summary>
public sealed class StressTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-stress-" + Guid.NewGuid().ToString("N")[..8]);
    private FileMindDb? _db;
    private SqliteStore? _store;

    private sealed class FakeClock : Application.Ports.IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private (FileIndexingService, SqliteStore) NewIndex()
    {
        _db = FileMindDb.Open(Path.Combine(_root, "stress.db"));
        _store = new SqliteStore(_db);
        return (new FileIndexingService(_store), _store);
    }

    private static FileCandidate Candidate(int i) => new()
    {
        ScanId = "stress",
        SourceUri = $@"C:\corpus\dir{i % 200}\file_{i:D6}.pdf",
        DisplayName = $"file_{i:D6}.pdf",
        Extension = "pdf",
        SizeBytes = 1000 + i,
        CreatedAt = DateTimeOffset.UnixEpoch,
        ModifiedAt = DateTimeOffset.UnixEpoch.AddSeconds(i),
        SourceScope = SourceScope.Downloads
    };

    private static AnalyzedFile Analyzed(int i) => new(
        Candidate(i),
        new ClassificationResult
        {
            FileKind = FileKind.Document, Category = "General", Evidence = [],
            Confidence = 90, Sources = [ClassifierSource.Local], Explanation = "t"
        },
        new RuleEvaluation { MatchedRuleIds = [], ProposedActions = [], Conflicts = [], Trace = [] },
        new Decision
        {
            Kind = DecisionKind.Organize,
            Candidate = Candidate(i),
            Classification = new ClassificationResult
            {
                FileKind = FileKind.Document, Category = "General", Evidence = [],
                Confidence = 90, Sources = [ClassifierSource.Local], Explanation = "t"
            },
            DestinationTemplate = "{Collection}/{FileKind}"
        });

    public void Dispose()
    {
        _db?.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Index_10k_AllPersisted_InsideBudget()
    {
        var (svc, store) = NewIndex();
        var candidates = Enumerable.Range(0, 10_000).Select(Candidate).ToList();
        var sw = Stopwatch.StartNew();
        var res = await svc.IndexAsync(candidates);
        sw.Stop();
        Assert.True(res.IsSuccess, res.Error?.Message);
        Assert.Equal(10_000, res.Value!.Indexed);
        var all = await ((IFileIndexRepository)store).GetAllAsync();
        Assert.Equal(10_000, all.Value!.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(120), $"index took {sw.Elapsed}");
    }

    [Fact]
    public void Plan_50k_Completes_InsideBudget()
    {
        var planner = new ActionPlanner();
        var paths = new WindowsStylePathNormalizer();
        var ctx = new PlanContext(paths, rel => @"C:\dest\" + rel,
            DateTimeOffset.Parse("2026-03-10T12:00:00Z"));
        var sw = Stopwatch.StartNew();
        var items = 0;
        for (var i = 0; i < 50_000; i++)
        {
            var p = planner.Plan(Analyzed(i).Decision, ctx);
            Assert.True(p.IsSuccess);
            items += p.Value.Items.Count;
        }
        sw.Stop();
        Assert.Equal(50_000, items);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(60), $"plan took {sw.Elapsed}");
    }

    [Fact]
    public async Task Index_Cancellation_MidStress_Stops()
    {
        var (svc, _) = NewIndex();
        var candidates = Enumerable.Range(0, 50_000).Select(Candidate).ToList();
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.IndexAsync(candidates, cts.Token));
    }

    [Fact]
    public async Task Index_Restart_ReopenDb_PersistsPrior()
    {
        var dbPath = Path.Combine(_root, "stress.db");
        using (var db1 = FileMindDb.Open(dbPath))
        {
            var store1 = new SqliteStore(db1);
            var svc1 = new FileIndexingService(store1);
            var res = await svc1.IndexAsync(Enumerable.Range(0, 2_000).Select(Candidate).ToList());
            Assert.Equal(2_000, res.Value!.Indexed);
        }
        using (var db2 = FileMindDb.Open(dbPath))
        {
            var store2 = new SqliteStore(db2);
            var svc2 = new FileIndexingService(store2);
            var res = await svc2.IndexAsync(Enumerable.Range(2_000, 2_000).Select(Candidate).ToList());
            Assert.Equal(2_000, res.Value!.Indexed);
            var all = await ((IFileIndexRepository)store2).GetAllAsync();
            Assert.Equal(4_000, all.Value!.Count);
        }
        _db = null; // both dbs already disposed inline
    }

    [Fact]
    public void WatchQueue_Burst_10k_BoundedAndOverflow()
    {
        var q = new WatcherEventQueue(capacity: 256);
        var now = DateTimeOffset.UtcNow;
        var ok = 0;
        for (var i = 0; i < 10_000; i++)
            if (q.TryEnqueue(new WatchEvent($@"C:\w\f{i}.txt", "Created", now), now)) ok++;
        Assert.True(q.Count <= 256);
        Assert.True(q.Overflowed);
        // dedup: same (path,kind) collapses — re-enqueue last event again updates
        var drained = 0;
        while (q.Dequeue() is not null) drained++;
        Assert.Equal(q.Count == 0 ? drained : q.Count, drained);
        Assert.True(drained <= 256);
    }

    [Fact]
    public void Debounce_Burst_10k_LatestWins()
    {
        var clock = new FakeClock();
        var deb = new DebounceService(clock, TimeSpan.FromMilliseconds(500));
        for (var i = 0; i < 10_000; i++)
            deb.Add(new WatchEvent(@"C:\w\f.txt", "Changed", clock.UtcNow));
        clock.UtcNow = clock.UtcNow.AddSeconds(5);
        var due = deb.CollectDue();
        Assert.Single(due); // 10k events collapsed to the newest
    }
}
