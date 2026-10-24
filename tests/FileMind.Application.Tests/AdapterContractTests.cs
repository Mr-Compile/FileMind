using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;
using FileMind.Platform.Windows;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

/// <summary>
/// Shared FileSystemAdapter contract assertions (PLAN-006 M1). Every adapter —
/// fake and platform — must obey identical semantics. Divergences are bugs,
/// not test branches. Read/verify goes through the port itself; file seeding
/// is the only adapter-specific delegate.
/// </summary>
internal static class AdapterContract
{
    private static string P(string root, string name) => Path.Combine(root, name);
    private static string N(string p) => p.Replace('/', '\\');

    private static async Task<string> ReadText(IFileSystemAdapter fs, string path)
    {
        var s = await fs.OpenReadAsync(path);
        Assert.True(s.IsSuccess, s.Error?.Message);
        using var r = new StreamReader(s.Value!);
        return await r.ReadToEndAsync();
    }

    public static async Task Enumerate_ListsFiles_NotDirs(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("a.txt", "x"); await seed(Path.Combine("sub", "b.txt"), "y");
        var entries = new List<FileEntry>();
        await foreach (var e in fs.EnumerateAsync(root, recursive: true)) entries.Add(e);
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Contains(e.Path, new[] { P(root, "a.txt"), P(root, Path.Combine("sub", "b.txt")) }));
    }

    public static async Task Enumerate_NonRecursive_TopLevelOnly(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("a.txt", "x"); await seed(Path.Combine("sub", "b.txt"), "y");
        var entries = new List<FileEntry>();
        await foreach (var e in fs.EnumerateAsync(root, recursive: false)) entries.Add(e);
        var names = entries.Select(e => N(e.Path)).ToList();
        Assert.Single(names);
        Assert.Equal(N(P(root, "a.txt")), names[0]);
    }

    public static async Task Enumerate_MissingRoot_EmptyNoThrow(IFileSystemAdapter fs, string root)
    {
        var entries = new List<FileEntry>();
        await foreach (var e in fs.EnumerateAsync(P(root, "ghost-root"), recursive: true)) entries.Add(e);
        Assert.Empty(entries);
    }

    public static async Task Stat_File_ReportsSizeAndKind(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        var p = P(root, "sized.txt");
        await seed("sized.txt", "12345");
        var r = await fs.StatAsync(p);
        Assert.True(r.IsSuccess);
        Assert.Equal(5, r.Value!.SizeBytes);
        Assert.False(r.Value.IsDirectory);
    }

    public static async Task Stat_Directory_ReportsIsDirectory(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed(Path.Combine("dir", "x.txt"), "y");
        var r = await fs.StatAsync(P(root, "dir"));
        Assert.True(r.IsSuccess);
        Assert.True(r.Value!.IsDirectory);
    }

    public static async Task Stat_Missing_NotFound(IFileSystemAdapter fs, string root)
    {
        var r = await fs.StatAsync(P(root, "missing.txt"));
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, r.Error!.Code);
    }

    public static async Task OpenRead_ReturnsContent_Missing_NotFound(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("c.txt", "content-42");
        Assert.Equal("content-42", await ReadText(fs, P(root, "c.txt")));
        var r = await fs.OpenReadAsync(P(root, "missing.txt"));
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, r.Error!.Code);
    }

    public static async Task Exists_FileAndMissing(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("e.txt", "x");
        Assert.True((await fs.ExistsAsync(P(root, "e.txt"))).Value);
        Assert.False((await fs.ExistsAsync(P(root, "nope.txt"))).Value);
    }

    public static async Task CreateDirectory_Nested_Succeeds(IFileSystemAdapter fs, string root)
    {
        var dir = P(root, Path.Combine("a", "b", "c"));
        var r = await fs.CreateDirectoryAsync(dir);
        Assert.True(r.IsSuccess);
        Assert.True((await fs.ExistsAsync(dir)).Value);
    }

    public static async Task Move_Basic_MovesContent(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("mv-src.txt", "hello");
        var r = await fs.MoveAsync(P(root, "mv-src.txt"), P(root, Path.Combine("mv", "mv-dst.txt")), ConflictPolicy.KeepBoth);
        Assert.True(r.IsSuccess);
        Assert.Equal("hello", await ReadText(fs, r.Value!));
        Assert.False((await fs.ExistsAsync(P(root, "mv-src.txt"))).Value);
    }

    public static async Task Move_MissingSource_NotFound(IFileSystemAdapter fs, string root)
    {
        var r = await fs.MoveAsync(P(root, "gone.txt"), P(root, "dst.txt"), ConflictPolicy.KeepBoth);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, r.Error!.Code);
    }

    public static async Task Move_ConflictSkip_NoChange_OkSource(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("sk-src.txt", "new");
        await seed("sk-dst.txt", "old");
        var r = await fs.MoveAsync(P(root, "sk-src.txt"), P(root, "sk-dst.txt"), ConflictPolicy.Skip);
        Assert.True(r.IsSuccess);
        Assert.Equal("old", await ReadText(fs, P(root, "sk-dst.txt")));
        Assert.True((await fs.ExistsAsync(P(root, "sk-src.txt"))).Value); // skipped, not moved
    }

    public static async Task Move_ConflictKeepBoth_Suffixed(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("kb-src.txt", "new");
        await seed("kb-dst.txt", "old");
        var r = await fs.MoveAsync(P(root, "kb-src.txt"), P(root, "kb-dst.txt"), ConflictPolicy.KeepBoth);
        Assert.True(r.IsSuccess);
        Assert.EndsWith("kb-dst (2).txt", N(r.Value!));
        Assert.Equal("old", await ReadText(fs, P(root, "kb-dst.txt")));
        Assert.Equal("new", await ReadText(fs, r.Value!));
    }

    public static async Task Move_ConflictAsk_TypedConflict_NeverOverwrites(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("ask-src.txt", "new");
        await seed("ask-dst.txt", "old");
        var r = await fs.MoveAsync(P(root, "ask-src.txt"), P(root, "ask-dst.txt"), ConflictPolicy.Ask);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.DestinationConflict, r.Error!.Code);
        Assert.Equal("old", await ReadText(fs, P(root, "ask-dst.txt"))); // never silently overwritten
        Assert.Equal("new", await ReadText(fs, P(root, "ask-src.txt")));
    }

    public static async Task Move_ConflictReplace_Overwrites(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("rp-src.txt", "new");
        await seed("rp-dst.txt", "old");
        var r = await fs.MoveAsync(P(root, "rp-src.txt"), P(root, "rp-dst.txt"), ConflictPolicy.ReplaceWithConfirmation);
        Assert.True(r.IsSuccess);
        Assert.Equal("new", await ReadText(fs, P(root, "rp-dst.txt")));
    }

    public static async Task Copy_Basic_SourceRemains(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("cp-src.txt", "dup");
        var r = await fs.CopyAsync(P(root, "cp-src.txt"), P(root, Path.Combine("cp", "cp-dst.txt")), ConflictPolicy.KeepBoth);
        Assert.True(r.IsSuccess);
        Assert.Equal("dup", await ReadText(fs, r.Value!));
        Assert.True((await fs.ExistsAsync(P(root, "cp-src.txt"))).Value);
    }

    public static async Task Copy_MissingSource_NotFound(IFileSystemAdapter fs, string root)
    {
        var r = await fs.CopyAsync(P(root, "gone.txt"), P(root, "dst.txt"), ConflictPolicy.KeepBoth);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, r.Error!.Code);
    }

    public static async Task Rename_SameDirectory(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed(Path.Combine("rn", "old.txt"), "r");
        var r = await fs.RenameAsync(P(root, Path.Combine("rn", "old.txt")), "new.txt", ConflictPolicy.KeepBoth);
        Assert.True(r.IsSuccess);
        Assert.Equal(N(P(root, Path.Combine("rn", "new.txt"))), N(r.Value!));
        Assert.False((await fs.ExistsAsync(P(root, Path.Combine("rn", "old.txt")))).Value);
    }

    public static async Task Rename_Missing_NotFound(IFileSystemAdapter fs, string root)
    {
        var r = await fs.RenameAsync(P(root, "gone.txt"), "new.txt", ConflictPolicy.KeepBoth);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, r.Error!.Code);
    }

    public static async Task Rename_ConflictKeepBoth_Suffixed(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed(Path.Combine("rk", "a.txt"), "a");
        await seed(Path.Combine("rk", "b.txt"), "b");
        var r = await fs.RenameAsync(P(root, Path.Combine("rk", "a.txt")), "b.txt", ConflictPolicy.KeepBoth);
        Assert.True(r.IsSuccess);
        Assert.EndsWith("b (2).txt", N(r.Value!));
    }

    public static async Task CheckCapabilities_Exists_Ok_Missing_NotFound(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("cap.txt", "x");
        var ok = await fs.CheckCapabilitiesAsync(P(root, "cap.txt"));
        Assert.True(ok.IsSuccess);
        var missing = await fs.CheckCapabilitiesAsync(P(root, "gone.txt"));
        Assert.False(missing.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, missing.Error!.Code);
    }

    public static async Task GetFreeSpace_ReturnsNonNegative(IFileSystemAdapter fs, string root)
    {
        var r = await fs.GetFreeSpaceAsync(root);
        Assert.True(r.IsSuccess);
        Assert.True(r.Value >= 0);
    }

    public static async Task Verify_MatchingTrue_MismatchFalse_MissingNotFound(IFileSystemAdapter fs, string root, Func<string, string, Task> seed)
    {
        await seed("v.txt", "vdata");
        var stat = await fs.StatAsync(P(root, "v.txt"));
        var fp = FileIdentity.ComputeFingerprint(stat.Value!.SizeBytes, stat.Value.ModifiedAt);
        Assert.True((await fs.VerifyAsync(P(root, "v.txt"), fp)).Value);
        Assert.False((await fs.VerifyAsync(P(root, "v.txt"), "fp-different")).Value);
        var missing = await fs.VerifyAsync(P(root, "gone.txt"), fp);
        Assert.False(missing.IsSuccess);
        Assert.Equal(ErrorCode.SourceNotFound, missing.Error!.Code);
    }

    public static async Task Move_LockedSource_FileLocked(IFileSystemAdapter fs, string root,
        Func<string, string, Task> seed, Func<string, IDisposable> lockFile)
    {
        await seed("lk.txt", "x");
        using var _ = lockFile(P(root, "lk.txt"));
        var r = await fs.MoveAsync(P(root, "lk.txt"), P(root, "lk-dst.txt"), ConflictPolicy.KeepBoth);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.Locked, r.Error!.Code);
    }

    public static async Task GetProtectedPaths_ContainsConfigured(IFileSystemAdapter fs, string configured)
    {
        var paths = fs.GetProtectedPaths();
        Assert.Contains(paths, p => p.Equals(configured, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class FakeAdapterContractTests
{
    private const string Root = @"C:\contract";
    private readonly FakeFileSystemAdapter _fs = new();
    private Task Seed(string rel, string content)
    {
        _fs.SeedFile(P(rel), content);
        return Task.CompletedTask;
        string P(string r) => Path.Combine(Root, r);
    }

    [Fact] public Task Enumerate_ListsFiles_NotDirs() => AdapterContract.Enumerate_ListsFiles_NotDirs(_fs, Root, Seed);
    [Fact] public Task Enumerate_NonRecursive_TopLevelOnly() => AdapterContract.Enumerate_NonRecursive_TopLevelOnly(_fs, Root, Seed);
    [Fact] public Task Enumerate_MissingRoot_EmptyNoThrow() => AdapterContract.Enumerate_MissingRoot_EmptyNoThrow(_fs, Root);
    [Fact] public Task Stat_File_ReportsSizeAndKind() => AdapterContract.Stat_File_ReportsSizeAndKind(_fs, Root, Seed);
    [Fact] public Task Stat_Directory_ReportsIsDirectory() => AdapterContract.Stat_Directory_ReportsIsDirectory(_fs, Root, Seed);
    [Fact] public Task Stat_Missing_NotFound() => AdapterContract.Stat_Missing_NotFound(_fs, Root);
    [Fact] public Task OpenRead_ReturnsContent_Missing_NotFound() => AdapterContract.OpenRead_ReturnsContent_Missing_NotFound(_fs, Root, Seed);
    [Fact] public Task Exists_FileAndMissing() => AdapterContract.Exists_FileAndMissing(_fs, Root, Seed);
    [Fact] public Task CreateDirectory_Nested_Succeeds() => AdapterContract.CreateDirectory_Nested_Succeeds(_fs, Root);
    [Fact] public Task Move_Basic_MovesContent() => AdapterContract.Move_Basic_MovesContent(_fs, Root, Seed);
    [Fact] public Task Move_MissingSource_NotFound() => AdapterContract.Move_MissingSource_NotFound(_fs, Root);
    [Fact] public Task Move_ConflictSkip_NoChange_OkSource() => AdapterContract.Move_ConflictSkip_NoChange_OkSource(_fs, Root, Seed);
    [Fact] public Task Move_ConflictKeepBoth_Suffixed() => AdapterContract.Move_ConflictKeepBoth_Suffixed(_fs, Root, Seed);
    [Fact] public Task Move_ConflictAsk_TypedConflict_NeverOverwrites() => AdapterContract.Move_ConflictAsk_TypedConflict_NeverOverwrites(_fs, Root, Seed);
    [Fact] public Task Move_ConflictReplace_Overwrites() => AdapterContract.Move_ConflictReplace_Overwrites(_fs, Root, Seed);
    [Fact] public Task Copy_Basic_SourceRemains() => AdapterContract.Copy_Basic_SourceRemains(_fs, Root, Seed);
    [Fact] public Task Copy_MissingSource_NotFound() => AdapterContract.Copy_MissingSource_NotFound(_fs, Root);
    [Fact] public Task Rename_SameDirectory() => AdapterContract.Rename_SameDirectory(_fs, Root, Seed);
    [Fact] public Task Rename_Missing_NotFound() => AdapterContract.Rename_Missing_NotFound(_fs, Root);
    [Fact] public Task Rename_ConflictKeepBoth_Suffixed() => AdapterContract.Rename_ConflictKeepBoth_Suffixed(_fs, Root, Seed);
    [Fact] public Task CheckCapabilities_Exists_Ok_Missing_NotFound() => AdapterContract.CheckCapabilities_Exists_Ok_Missing_NotFound(_fs, Root, Seed);
    [Fact] public Task GetFreeSpace_ReturnsNonNegative() => AdapterContract.GetFreeSpace_ReturnsNonNegative(_fs, Root);
    [Fact] public Task Verify_MatchingTrue_MismatchFalse_MissingNotFound() => AdapterContract.Verify_MatchingTrue_MismatchFalse_MissingNotFound(_fs, Root, Seed);
    [Fact] public Task Move_LockedSource_FileLocked()
    {
        _fs.LockPaths = true;
        _fs.SeedFile(Path.Combine(Root, "lk.txt"), "x");
        return AdapterContract.Move_LockedSource_FileLocked(_fs, Root, Seed, _ => new FakeLock());
    }
    private sealed class FakeLock : IDisposable { public void Dispose() { } }

    [Fact] public Task GetProtectedPaths_ContainsConfigured()
    {
        _fs.ProtectPath(@"C:\contract-protected");
        return AdapterContract.GetProtectedPaths_ContainsConfigured(_fs, @"C:\contract-protected");
    }
}

public sealed class WindowsAdapterContractTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-contract-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly WindowsFileSystemAdapter _fs = new();
    private Task Seed(string rel, string content)
    {
        var p = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        return Task.CompletedTask;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    [Fact] public Task Enumerate_ListsFiles_NotDirs() => AdapterContract.Enumerate_ListsFiles_NotDirs(_fs, _root, Seed);
    [Fact] public Task Enumerate_NonRecursive_TopLevelOnly() => AdapterContract.Enumerate_NonRecursive_TopLevelOnly(_fs, _root, Seed);
    [Fact] public Task Enumerate_MissingRoot_EmptyNoThrow() => AdapterContract.Enumerate_MissingRoot_EmptyNoThrow(_fs, _root);
    [Fact] public Task Stat_File_ReportsSizeAndKind() => AdapterContract.Stat_File_ReportsSizeAndKind(_fs, _root, Seed);
    [Fact] public Task Stat_Directory_ReportsIsDirectory() => AdapterContract.Stat_Directory_ReportsIsDirectory(_fs, _root, Seed);
    [Fact] public Task Stat_Missing_NotFound() => AdapterContract.Stat_Missing_NotFound(_fs, _root);
    [Fact] public Task OpenRead_ReturnsContent_Missing_NotFound() => AdapterContract.OpenRead_ReturnsContent_Missing_NotFound(_fs, _root, Seed);
    [Fact] public Task Exists_FileAndMissing() => AdapterContract.Exists_FileAndMissing(_fs, _root, Seed);
    [Fact] public Task CreateDirectory_Nested_Succeeds() => AdapterContract.CreateDirectory_Nested_Succeeds(_fs, _root);
    [Fact] public Task Move_Basic_MovesContent() => AdapterContract.Move_Basic_MovesContent(_fs, _root, Seed);
    [Fact] public Task Move_MissingSource_NotFound() => AdapterContract.Move_MissingSource_NotFound(_fs, _root);
    [Fact] public Task Move_ConflictSkip_NoChange_OkSource() => AdapterContract.Move_ConflictSkip_NoChange_OkSource(_fs, _root, Seed);
    [Fact] public Task Move_ConflictKeepBoth_Suffixed() => AdapterContract.Move_ConflictKeepBoth_Suffixed(_fs, _root, Seed);
    [Fact] public Task Move_ConflictAsk_TypedConflict_NeverOverwrites() => AdapterContract.Move_ConflictAsk_TypedConflict_NeverOverwrites(_fs, _root, Seed);
    [Fact] public Task Move_ConflictReplace_Overwrites() => AdapterContract.Move_ConflictReplace_Overwrites(_fs, _root, Seed);
    [Fact] public Task Copy_Basic_SourceRemains() => AdapterContract.Copy_Basic_SourceRemains(_fs, _root, Seed);
    [Fact] public Task Copy_MissingSource_NotFound() => AdapterContract.Copy_MissingSource_NotFound(_fs, _root);
    [Fact] public Task Rename_SameDirectory() => AdapterContract.Rename_SameDirectory(_fs, _root, Seed);
    [Fact] public Task Rename_Missing_NotFound() => AdapterContract.Rename_Missing_NotFound(_fs, _root);
    [Fact] public Task Rename_ConflictKeepBoth_Suffixed() => AdapterContract.Rename_ConflictKeepBoth_Suffixed(_fs, _root, Seed);
    [Fact] public Task CheckCapabilities_Exists_Ok_Missing_NotFound() => AdapterContract.CheckCapabilities_Exists_Ok_Missing_NotFound(_fs, _root, Seed);
    [Fact] public Task GetFreeSpace_ReturnsNonNegative() => AdapterContract.GetFreeSpace_ReturnsNonNegative(_fs, _root);
    [Fact] public Task Verify_MatchingTrue_MismatchFalse_MissingNotFound() => AdapterContract.Verify_MatchingTrue_MismatchFalse_MissingNotFound(_fs, _root, Seed);
    [Fact] public Task Move_LockedSource_FileLocked() =>
        AdapterContract.Move_LockedSource_FileLocked(_fs, _root, Seed,
            p => new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None));

    [Fact] public Task GetProtectedPaths_ContainsConfigured()
    {
        var fs = new WindowsFileSystemAdapter([@"C:\contract-protected"]);
        return AdapterContract.GetProtectedPaths_ContainsConfigured(fs, @"C:\contract-protected");
    }
}
