using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Operations;
using FileMind.Domain.Safety;
using FileMind.Testing;
using Xunit;

namespace FileMind.Domain.Tests;

public class TransactionTests
{
    private static (FakeFileSystemAdapter fs, InMemoryOperationStore store, TransactionEngine engine, SafetyContext ctx) Setup()
    {
        var fs = new FakeFileSystemAdapter();
        var store = new InMemoryOperationStore();
        var safety = new SafetyEngine(fs, new WindowsStylePathNormalizer());
        var engine = new TransactionEngine(fs, store, safety);
        var ctx = new SafetyContext(new StaticProtectedPaths());
        return (fs, store, engine, ctx);
    }

    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    private static ActionItem MoveItem(string src, string dest, string? fingerprint = null) => new()
    {
        Operation = OperationType.Move,
        Source = src,
        Destination = dest,
        Reason = "t",
        UndoRecipe = fingerprint is null
            ? null
            : new UndoRecipe(OperationType.Move, dest, src, fingerprint, [])
    };

    private static ActionPlan Plan(params ActionItem[] items) => new()
    {
        PlanId = $"p-{Guid.NewGuid():N}",
        CreatedAt = DateTimeOffset.UtcNow,
        SourceDecision = "test",
        Items = items
    };

    [Fact]
    public async Task Execute_MoveCommitsAndVerifies()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt", "hello", T0);
        var fp = FileIdentity.ComputeFingerprint(5, T0);
        var plan = Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt", fp));
        var result = await engine.ExecuteAsync(plan, ctx);
        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Committed, result.Value!.Status);
        Assert.Null(fs.ReadBytes("D:/in/a.txt"));
        Assert.Equal("hello", fs.ReadText("D:/out/a.txt"));
        Assert.Equal($"verified:{fp}", result.Value!.Items[0].Verification);
    }

    [Fact]
    public async Task Execute_PersistsTransitionsToStore()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt", "data", T0);
        var plan = Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt", FileIdentity.ComputeFingerprint(4, T0)));
        var result = await engine.ExecuteAsync(plan, ctx);
        var loaded = await store.LoadAsync(result.Value!.OperationId);
        Assert.True(loaded.IsSuccess);
        Assert.Equal(OperationStatus.Committed, loaded.Value!.Status);
    }

    [Fact]
    public async Task Execute_SafetyBlock_Fails()
    {
        var (fs, store, engine, _) = Setup();
        fs.SeedFile("C:/sys/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths("C:\\sys"));
        var plan = Plan(MoveItem("C:/sys/x.txt", "D:/out/x.txt"));
        var result = await engine.ExecuteAsync(plan, ctx);
        Assert.Equal(OperationStatus.Failed, result.Value!.Status);
        Assert.NotNull(result.Value!.Error);
    }

    [Fact]
    public async Task Execute_MoveFailure_ItemFailed()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt");
        fs.FailNextMove = true;
        var plan = Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt"));
        var result = await engine.ExecuteAsync(plan, ctx);
        Assert.Equal(OperationStatus.NeedsAttention, result.Value!.Status);
        Assert.Equal(OperationStatus.Failed, result.Value!.Items[0].Status);
    }

    [Fact]
    public async Task Undo_CommittedOp_Reverses()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt", "hello", T0);
        var undo = new UndoService(fs);
        var plan = Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt", FileIdentity.ComputeFingerprint(5, T0)));

        var record = await engine.ExecuteAsync(plan, ctx);
        var reverse = await undo.BuildReversePlanAsync(record.Value!);
        Assert.True(reverse.IsSuccess);
        Assert.Equal("D:/out/a.txt", reverse.Value!.Items[0].Source);
        Assert.Equal("D:/in/a.txt", reverse.Value!.Items[0].Destination);
    }

    [Fact]
    public async Task Undo_NonCommittedOp_Fails()
    {
        var (fs, store, _, _) = Setup();
        var undo = new UndoService(fs);
        var record = new OperationRecord
        {
            OperationId = "op1", PlanId = "p", Status = OperationStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow, Items = []
        };
        var reverse = await undo.BuildReversePlanAsync(record);
        Assert.False(reverse.IsSuccess);
    }

    [Fact]
    public async Task Execute_BatchPartialFailure_Visible()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/ok.txt");
        fs.SeedFile("D:/in/bad.txt");
        fs.FailMoveMatching = p => p.Contains("bad");
        var plan = Plan(MoveItem("D:/in/ok.txt", "D:/out/ok.txt"), MoveItem("D:/in/bad.txt", "D:/out/bad.txt"));
        var result = await engine.ExecuteAsync(plan, ctx);
        Assert.Equal(OperationStatus.NeedsAttention, result.Value!.Status);
        Assert.Equal(OperationStatus.Committed, result.Value!.Items[0].Status);
        Assert.Equal(OperationStatus.Failed, result.Value!.Items[1].Status);
    }

    [Fact]
    public async Task Undo_ChangedTarget_RefusesBlindOverwrite()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt", "hello");
        var undo = new UndoService(fs);
        var plan = Plan(MoveItem("D:/in/a.txt", "D:/out/a.txt"));
        var record = await engine.ExecuteAsync(plan, ctx);

        var reverse = await undo.BuildReversePlanAsync(record.Value!);
        Assert.False(reverse.IsSuccess);
    }

    // ---- M4: per-OperationType undo coverage audit ----

    [Fact]
    public async Task Undo_Rename_RecipeReverses()
    {
        var (fs, store, engine, ctx) = Setup();
        fs.SeedFile("D:/in/old.txt", "hello", T0);
        var fp = FileIdentity.ComputeFingerprint(5, T0);
        var undo = new UndoService(fs);
        // RenameAsync combines dir(source) + newName; the recipe's reverse
        // destination is therefore the original file NAME, not a path.
        var item = new ActionItem
        {
            Operation = OperationType.Rename,
            Source = "D:/in/old.txt",
            Destination = "new.txt",
            Reason = "t",
            UndoRecipe = new UndoRecipe(OperationType.Rename, "D:/in/new.txt", "old.txt", fp, [])
        };
        var record = await engine.ExecuteAsync(Plan(item), ctx);
        Assert.Equal(OperationStatus.Committed, record.Value!.Status);
        Assert.Equal("hello", fs.ReadText("D:/in/new.txt"));

        var reverse = await undo.BuildReversePlanAsync(record.Value!);
        Assert.True(reverse.IsSuccess);
        Assert.Equal(OperationType.Rename, reverse.Value!.Items[0].Operation);
        var back = await engine.ExecuteAsync(reverse.Value!, ctx);
        Assert.Equal(OperationStatus.Committed, back.Value!.Status);
        Assert.Equal("hello", fs.ReadText("D:/in/old.txt"));
        Assert.Null(fs.ReadBytes("D:/in/new.txt"));
    }

    [Fact]
    public async Task Copy_WithoutRecipe_Executes_UndoNeedsReview()
    {
        // Audit finding: no Delete op exists, so a committed Copy can only
        // be undone if a recipe was attached — planner never emits Copy.
        var (fs, _, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt", "data", T0);
        var item = new ActionItem
        {
            Operation = OperationType.Copy,
            Source = "D:/in/a.txt",
            Destination = "D:/out/a.txt",
            Reason = "t"
        };
        var record = await engine.ExecuteAsync(Plan(item), ctx);
        Assert.Equal(OperationStatus.Committed, record.Value!.Status);
        Assert.NotNull(fs.ReadBytes("D:/in/a.txt"));
        Assert.NotNull(fs.ReadBytes("D:/out/a.txt"));

        var undo = new UndoService(fs);
        var reverse = await undo.BuildReversePlanAsync(record.Value!);
        Assert.False(reverse.IsSuccess); // no reversible items — surfaced, not silent
    }

    [Fact]
    public async Task NonMutatingOps_Commit_RequireNoRecipe()
    {
        var (fs, _, engine, ctx) = Setup();
        fs.SeedFile("D:/in/a.txt");
        var items = new[] { OperationType.Tag, OperationType.Ignore, OperationType.Notify, OperationType.Ask }
            .Select(op => new ActionItem
            {
                Operation = op,
                Source = "D:/in/a.txt",
                Reason = "t"
            }).ToArray();
        var record = await engine.ExecuteAsync(Plan(items), ctx);
        Assert.Equal(OperationStatus.Committed, record.Value!.Status);
        Assert.All(record.Value!.Items, i => Assert.Equal(OperationStatus.Committed, i.Status));
        Assert.NotNull(fs.ReadBytes("D:/in/a.txt")); // untouched
    }

    [Fact]
    public async Task Rollback_SourceIntact_RolledBack()
    {
        var (fs, store, engine, ctx) = Setup();
        var rollback = new RollbackService(fs, store);
        fs.SeedFile("D:/in/a.txt");

        var stale = new OperationRecord
        {
            OperationId = "stale-1",
            PlanId = "p",
            Status = OperationStatus.Executing,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = [new OperationItemState
            {
                ItemId = "i1",
                Operation = OperationType.Move,
                OriginalLocation = "D:/in/a.txt",
                Status = OperationStatus.Executing
            }]
        };
        await store.SaveAsync(stale);

        var recovered = await rollback.RecoverAsync();
        Assert.True(recovered.IsSuccess);
        Assert.Equal(OperationStatus.RolledBack, recovered.Value![0].Status);
    }
}
