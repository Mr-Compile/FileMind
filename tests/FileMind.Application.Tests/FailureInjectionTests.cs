using FileMind.Application.Ports;
using FileMind.Application.Recovery;
using FileMind.Application.Watching;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Operations;
using FileMind.Domain.Ports;
using FileMind.Domain.Safety;
using FileMind.Infrastructure.Sqlite;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

/// <summary>
/// PLAN-006 M3 — injected failures must surface as typed errors,
/// blockers, or NeedsReview — never silent and never unhandled.
/// </summary>
public sealed class FailureInjectionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-fi-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static (FakeFileSystemAdapter fs, InMemoryOperationStore store,
        TransactionEngine engine, SafetyEngine safety) Setup()
    {
        var fs = new FakeFileSystemAdapter();
        var store = new InMemoryOperationStore();
        var safety = new SafetyEngine(fs, new WindowsStylePathNormalizer());
        var engine = new TransactionEngine(fs, store, safety);
        return (fs, store, engine, safety);
    }

    private static ActionItem MoveItem(string src, string dest) => new()
    {
        Operation = OperationType.Move,
        Source = src,
        Destination = dest,
        Reason = "fi"
    };

    private static ActionPlan Plan(params ActionItem[] items) => new()
    {
        PlanId = $"p-{Guid.NewGuid():N}",
        CreatedAt = DateTimeOffset.UtcNow,
        SourceDecision = "fi",
        Items = items
    };

    // ---- permission denied ----

    [Fact]
    public async Task PermissionDenied_CapabilityOverride_BlocksWithReason()
    {
        var (fs, _, engine, _) = Setup();
        fs.SeedFile("D:/in/locked-acl.txt");
        var denyMove = new PermissionCapabilities(true, false, false, false, false, false);
        var ctx = new SafetyContext(new StaticProtectedPaths(),
            CapabilityOverrides: new Dictionary<string, PermissionCapabilities>
            { ["D:/in/locked-acl.txt"] = denyMove });
        var result = await engine.ExecuteAsync(
            Plan(MoveItem("D:/in/locked-acl.txt", "D:/out/x.txt")), ctx);
        Assert.Equal(OperationStatus.Failed, result.Value!.Status);
        Assert.NotNull(result.Value!.Error);
        Assert.Equal("data", fs.ReadText("D:/in/locked-acl.txt")); // untouched
    }

    // ---- locked file ----

    [Fact]
    public async Task Locked_AtValidation_SafetyBlocks()
    {
        var (fs, _, _, safety) = Setup();
        fs.SeedFile("D:/in/a.txt");
        var plan = Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt"));
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var before = await safety.ValidateAsync(plan, ctx);
        Assert.True(before.Allowed);
        fs.LockPaths = true;
        var during = await safety.ValidateAsync(plan, ctx);
        Assert.False(during.Allowed);
        Assert.Contains(during.Blockers, b => b.Contains("locked"));
    }

    [Fact]
    public async Task Locked_WholeFs_ExecuteFails_Typed()
    {
        var (fs, _, engine, ctx0) = Setup();
        _ = ctx0;
        fs.SeedFile("D:/in/a.txt");
        fs.LockPaths = true;
        var result = await engine.ExecuteAsync(
            Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt")),
            new SafetyContext(new StaticProtectedPaths()));
        Assert.Equal(OperationStatus.Failed, result.Value!.Status);
        Assert.NotNull(result.Value!.Error);
    }

    // ---- source disappears mid-operation ----

    [Fact]
    public async Task SourceVanishes_MidMove_ItemFailed_SourceGone()
    {
        var (fs, _, engine, _) = Setup();
        fs.SeedFile("D:/in/a.txt");
        var statCalls = 0;
        // survive the two safety stats, vanish at the move call
        fs.VanishOn = p => p.Contains("a.txt") && ++statCalls > 2;
        var result = await engine.ExecuteAsync(
            Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt")),
            new SafetyContext(new StaticProtectedPaths()));
        Assert.Equal(OperationStatus.NeedsAttention, result.Value!.Status);
        Assert.Equal(OperationStatus.Failed, result.Value!.Items[0].Status);
        Assert.Null(fs.ReadBytes("D:/out/a.txt")); // never created
    }

    [Fact]
    public async Task SourceVanishes_BeforeSafety_TypedBlocker()
    {
        var (fs, _, engine, _) = Setup();
        fs.SeedFile("D:/in/a.txt");
        fs.VanishOn = p => p.Contains("a.txt");
        var result = await engine.ExecuteAsync(
            Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt")),
            new SafetyContext(new StaticProtectedPaths()));
        Assert.Equal(OperationStatus.Failed, result.Value!.Status);
    }

    // ---- disk full ----

    [Fact]
    public async Task DiskFull_SafetyBlocks_NothingWritten()
    {
        var (fs, _, engine, _) = Setup();
        fs.SeedFile("D:/in/big.bin", new string('x', 4));
        var ctx = new SafetyContext(new StaticProtectedPaths(),
            FreeSpaceOverrides: new Dictionary<string, long> { ["D:/out/big.bin"] = 0 });
        var result = await engine.ExecuteAsync(
            Plan(MoveItem("D:/in/big.bin", "D:/out/big.bin")), ctx);
        Assert.Equal(OperationStatus.Failed, result.Value!.Status);
        Assert.NotNull(fs.ReadBytes("D:/in/big.bin")); // source untouched
    }

    // ---- DB failure ----

    [Fact]
    public async Task Db_Disposed_SaveFails_TypedPersistence_NoThrow()
    {
        var dbPath = Path.Combine(_root, "fi.db");
        var db = FileMindDb.Open(dbPath);
        var store = new SqliteStore(db);
        db.Dispose();
        var record = new OperationRecord
        {
            OperationId = "op-x", PlanId = "p", Status = OperationStatus.Planned,
            CreatedAt = DateTimeOffset.UtcNow, Items = []
        };
        var res = await ((IOperationStore)store).SaveAsync(record);
        Assert.False(res.IsSuccess);
        Assert.Equal(ErrorCode.PersistenceFailure, res.Error!.Code);
    }

    // ---- crash boundaries ----

    [Fact]
    public async Task Crash_ExecutingRecord_ReconcilesToNeedsAttention()
    {
        var dbPath = Path.Combine(_root, "crash1.db");
        using var db = FileMindDb.Open(dbPath);
        var store = new SqliteStore(db);
        var stale = new OperationRecord
        {
            OperationId = "crash-exec", PlanId = "p",
            Status = OperationStatus.Executing,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = [new OperationItemState
            {
                ItemId = "i1", Operation = OperationType.Move,
                OriginalLocation = "D:/in/a.txt", Status = OperationStatus.Executing
            }]
        };
        await ((IOperationStore)store).SaveAsync(stale);
        var recovery = new StartupRecoveryService(store);
        var report = await recovery.ReconcileAsync();
        Assert.True(report.IsSuccess);
        Assert.Equal(1, report.Value!.NeedsAttention);
        var loaded = await ((IOperationStore)store).LoadAsync("crash-exec");
        Assert.Equal(OperationStatus.NeedsAttention, loaded.Value!.Status);
    }

    [Fact]
    public async Task Crash_PristinePlannedRecord_ReconcilesToFailed()
    {
        var dbPath = Path.Combine(_root, "crash2.db");
        using var db = FileMindDb.Open(dbPath);
        var store = new SqliteStore(db);
        var stale = new OperationRecord
        {
            OperationId = "crash-planned", PlanId = "p",
            Status = OperationStatus.Planned,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = [new OperationItemState
            {
                ItemId = "i1", Operation = OperationType.Move,
                OriginalLocation = "D:/in/a.txt", Status = OperationStatus.Planned
            }]
        };
        await ((IOperationStore)store).SaveAsync(stale);
        var recovery = new StartupRecoveryService(store);
        var report = await recovery.ReconcileAsync();
        Assert.Equal(1, report.Value!.FailedPristine);
        var loaded = await ((IOperationStore)store).LoadAsync("crash-planned");
        Assert.Equal(OperationStatus.Failed, loaded.Value!.Status);
    }

    // ---- USB removal (simulated: root vanishes between events) ----

    [Fact]
    public async Task UsbRemoval_StabilityCheck_ReportsMissing()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("E:/usb/doc.pdf");
        var checker = new FileStabilityChecker(fs, pollCount: 1, pollInterval: TimeSpan.Zero);
        var ok = await checker.CheckAsync("E:/usb/doc.pdf");
        Assert.NotEqual(StabilityVerdict.Missing, ok.Verdict);
        fs.VanishOn = p => p.Contains("doc.pdf");
        var gone = await checker.CheckAsync("E:/usb/doc.pdf");
        Assert.Equal(StabilityVerdict.Missing, gone.Verdict);
    }

    [Fact]
    public async Task UsbRemoval_EnumerateVanishedRoot_EmptyNoThrow()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("E:/usb/doc.pdf");
        fs.RemovePath("E:/usb/doc.pdf");
        fs.RemovePath("E:/usb");
        var entries = new List<FileEntry>();
        await foreach (var e in fs.EnumerateAsync("E:/usb", recursive: true))
            entries.Add(e);
        Assert.Empty(entries);
    }
}
