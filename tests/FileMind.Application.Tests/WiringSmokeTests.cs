using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

public class WiringSmokeTests
{
    [Fact]
    public async Task FakeAdapter_BasicMoveWorks()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("D:/in/a.txt", "x");
        var moved = await fs.MoveAsync("D:/in/a.txt", "D:/out/a.txt", Domain.Contracts.ConflictPolicy.KeepBoth);
        Assert.True(moved.IsSuccess);
        Assert.Equal("D:/out/a.txt", moved.Value);
    }

    [Fact]
    public async Task OperationStore_RoundTrips()
    {
        var store = new InMemoryOperationStore();
        var record = new Domain.Contracts.OperationRecord
        {
            OperationId = "op1",
            PlanId = "p1",
            Status = Domain.Contracts.OperationStatus.Planned,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = []
        };
        await store.SaveAsync(record);
        var loaded = await store.LoadAsync("op1");
        Assert.True(loaded.IsSuccess);
        Assert.Equal("p1", loaded.Value!.PlanId);
    }
}
