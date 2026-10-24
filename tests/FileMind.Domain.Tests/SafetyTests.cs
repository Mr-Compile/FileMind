using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Safety;
using FileMind.Testing;
using Xunit;

namespace FileMind.Domain.Tests;

public class SafetyTests
{
    private static SafetyEngine Engine(FakeFileSystemAdapter fs) =>
        new(fs, new WindowsStylePathNormalizer());

    private static ActionPlan PlanWith(ActionItem item) => new()
    {
        PlanId = "p1",
        CreatedAt = DateTimeOffset.UtcNow,
        SourceDecision = "test",
        Items = [item]
    };

    private static ActionItem Move(string src, string dest, ConflictPolicy policy = ConflictPolicy.KeepBoth) =>
        new() { Operation = OperationType.Move, Source = src, Destination = dest, ConflictPolicy = policy, Reason = "t" };

    [Fact]
    public async Task ProtectedSource_Blocked()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("C:/Windows/system32/x.dll");
        var ctx = new SafetyContext(new StaticProtectedPaths("C:\\Windows"));
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("C:/Windows/system32/x.dll", "D:/out/x.dll")), ctx);
        Assert.False(result.Allowed);
        Assert.Contains(result.Blockers, b => b.Contains("protected"));
    }

    [Fact]
    public async Task ProtectedDestination_Blocked()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths("C:\\Windows"));
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/x.txt", "C:/Windows/x.txt")), ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task MissingSource_Blocked()
    {
        var fs = new FakeFileSystemAdapter();
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/gone.txt", "D:/out/x.txt")), ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task DestinationConflict_KeepBothWarns()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/x.txt");
        fs.SeedFile("D:/out/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/x.txt", "D:/out/x.txt", ConflictPolicy.KeepBoth)), ctx);
        Assert.True(result.Allowed);
        Assert.Contains(result.Warnings, w => w.Contains("keep both", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DestinationConflict_AskNeedsConfirmation()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/x.txt");
        fs.SeedFile("D:/out/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/x.txt", "D:/out/x.txt", ConflictPolicy.Ask)), ctx);
        Assert.NotNull(result.RequiredConfirmation);
    }

    [Fact]
    public async Task InsufficientSpace_Blocked()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/big.bin", new string('x', 5000));
        var ctx = new SafetyContext(new StaticProtectedPaths(),
            FreeSpaceOverrides: new Dictionary<string, long> { ["D:/out/big.bin"] = 100 });
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/big.bin", "D:/out/big.bin")), ctx);
        Assert.False(result.Allowed);
        Assert.Contains(result.Blockers, b => b.Contains("insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LockedSource_Blocked()
    {
        var fs = new FakeFileSystemAdapter { LockPaths = true };
        fs.SeedFile("D:/in/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/x.txt", "D:/out/x.txt")), ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task MoveNotPermitted_Blocked()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths(),
            CapabilityOverrides: new Dictionary<string, PermissionCapabilities>
            {
                ["D:/in/x.txt"] = new(true, true, false, true, true, true)
            });
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/x.txt", "D:/out/x.txt")), ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task EmptyPlan_Blocked()
    {
        var fs = new FakeFileSystemAdapter();
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var empty = new ActionPlan { PlanId = "e", CreatedAt = DateTimeOffset.UtcNow, SourceDecision = "t", Items = [] };
        var result = await Engine(fs).ValidateAsync(empty, ctx);
        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task CleanMove_Allowed()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/x.txt");
        var ctx = new SafetyContext(new StaticProtectedPaths());
        var result = await Engine(fs).ValidateAsync(PlanWith(Move("D:/in/x.txt", "D:/out/x.txt")), ctx);
        Assert.True(result.Allowed);
        Assert.Empty(result.Blockers);
    }
}
