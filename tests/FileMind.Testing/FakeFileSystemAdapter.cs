using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Testing;

public sealed class FakeFileSystemAdapter : IFileSystemAdapter
{
    private sealed class Node
    {
        public bool IsDirectory;
        public byte[] Content = [];
        public DateTimeOffset CreatedAt = DateTimeOffset.UtcNow;
        public DateTimeOffset ModifiedAt = DateTimeOffset.UtcNow;
    }

    private readonly ConcurrentDictionary<string, Node> _fs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _protected = [];
    public long FreeSpace { get; set; } = 1_000_000_000;
    public bool FailNextMove { get; set; }
    public Func<string, bool>? FailMoveMatching { get; set; }
    public bool LockPaths { get; set; }
    public Func<string, bool>? VanishOn { get; set; }
    public List<string> CallLog { get; } = [];

    public void SeedFile(string path, string content = "data", DateTimeOffset? modifiedAt = null)
    {
        _fs[path] = new Node
        {
            Content = Encoding.UTF8.GetBytes(content),
            ModifiedAt = modifiedAt ?? DateTimeOffset.UtcNow
        };
        var dir = DirectoryOf(path);
        if (dir is not null) EnsureDirectory(dir);
    }

    public void SeedDirectory(string path) => EnsureDirectory(path);
    public void ProtectPath(string path) => _protected.Add(path);
    public void RemovePath(string path) => _fs.TryRemove(path, out _);
    public byte[]? ReadBytes(string path) => _fs.TryGetValue(path, out var n) && !n.IsDirectory ? n.Content : null;
    public string? ReadText(string path) => ReadBytes(path) is { } b ? Encoding.UTF8.GetString(b) : null;

    private void EnsureDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || _fs.ContainsKey(path)) return;
        _fs[path] = new Node { IsDirectory = true };
        var parent = DirectoryOf(path);
        if (parent is not null && parent != path) EnsureDirectory(parent);
    }

    private static string DirectoryOf(string path)
    {
        var i = path.LastIndexOfAny(['/', '\\']);
        return i > 0 ? path[..i] : string.Empty;
    }

    public async IAsyncEnumerable<FileEntry> EnumerateAsync(string root, bool recursive, [EnumeratorCancellation] CancellationToken ct = default)
    {
        CallLog.Add($"enumerate:{root}");
        await Task.Yield();
        var prefix = root.TrimEnd('/', '\\') + "/";
        foreach (var (path, node) in _fs.OrderBy(k => k.Key))
        {
            ct.ThrowIfCancellationRequested();
            if (node.IsDirectory) continue;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !path.Equals(root.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
                continue;
            if (!recursive)
            {
                var rel = path[prefix.Length..];
                if (rel.Contains('/') || rel.Contains('\\')) continue;
            }
            yield return new FileEntry(path, node.Content.LongLength, node.CreatedAt, node.ModifiedAt);
        }
    }

    public Task<Result<FileStat>> StatAsync(string path, CancellationToken ct = default)
    {
        CallLog.Add($"stat:{path}");
        if (VanishOn?.Invoke(path) == true)
        {
            _fs.TryRemove(path, out _);
            return Task.FromResult(Result<FileStat>.Fail(FileMindError.NotFound($"'{path}' disappeared.")));
        }
        if (LockPaths) return Task.FromResult(Result<FileStat>.Fail(FileMindError.FileLocked($"'{path}' locked.")));
        if (!_fs.TryGetValue(path, out var n))
            return Task.FromResult(Result<FileStat>.Fail(FileMindError.NotFound($"'{path}' not found.")));
        return Task.FromResult(Result<FileStat>.Ok(new FileStat(path, n.Content.LongLength, n.CreatedAt, n.ModifiedAt, n.IsDirectory, $"fake-{path.GetHashCode():x}")));
    }

    public Task<Result<Stream>> OpenReadAsync(string path, CancellationToken ct = default)
    {
        if (!_fs.TryGetValue(path, out var n) || n.IsDirectory)
            return Task.FromResult(Result<Stream>.Fail(FileMindError.NotFound($"'{path}' not found.")));
        return Task.FromResult(Result<Stream>.Ok((Stream)new MemoryStream(n.Content, writable: false)));
    }

    public Task<Result<bool>> ExistsAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Result<bool>.Ok(_fs.ContainsKey(path)));

    public Task<Result<bool>> CreateDirectoryAsync(string path, CancellationToken ct = default)
    {
        CallLog.Add($"mkdir:{path}");
        EnsureDirectory(path);
        return Task.FromResult(Result<bool>.Ok(true));
    }

    public Task<Result<string>> MoveAsync(string source, string destination, ConflictPolicy conflict, CancellationToken ct = default)
    {
        CallLog.Add($"move:{source}->{destination}");
        if (FailNextMove) { FailNextMove = false; return Task.FromResult(Result<string>.Fail(FileMindError.Io("Injected move failure."))); }
        if (FailMoveMatching?.Invoke(source) == true)
            return Task.FromResult(Result<string>.Fail(FileMindError.Io($"Injected move failure for '{source}'.")));
        if (!_fs.TryGetValue(source, out var n) || n.IsDirectory)
            return Task.FromResult(Result<string>.Fail(FileMindError.NotFound($"'{source}' not found.")));
        var dest = ResolveConflict(destination, conflict);
        if (dest is null) return Task.FromResult(Result<string>.Ok(source));
        _fs.TryRemove(source, out _);
        _fs[dest] = n;
        return Task.FromResult(Result<string>.Ok(dest));
    }

    public Task<Result<string>> CopyAsync(string source, string destination, ConflictPolicy conflict, CancellationToken ct = default)
    {
        CallLog.Add($"copy:{source}->{destination}");
        if (!_fs.TryGetValue(source, out var n) || n.IsDirectory)
            return Task.FromResult(Result<string>.Fail(FileMindError.NotFound($"'{source}' not found.")));
        var dest = ResolveConflict(destination, conflict);
        if (dest is null) return Task.FromResult(Result<string>.Ok(source));
        _fs[dest] = new Node { Content = (byte[])n.Content.Clone(), CreatedAt = n.CreatedAt };
        return Task.FromResult(Result<string>.Ok(dest));
    }

    public Task<Result<string>> RenameAsync(string source, string newName, ConflictPolicy conflict, CancellationToken ct = default)
    {
        CallLog.Add($"rename:{source}->{newName}");
        if (!_fs.TryGetValue(source, out var n) || n.IsDirectory)
            return Task.FromResult(Result<string>.Fail(FileMindError.NotFound($"'{source}' not found.")));
        var dest = ResolveConflict(newName, conflict);
        if (dest is null) return Task.FromResult(Result<string>.Ok(source));
        _fs.TryRemove(source, out _);
        _fs[dest] = n;
        return Task.FromResult(Result<string>.Ok(dest));
    }

    private string? ResolveConflict(string destination, ConflictPolicy conflict)
    {
        if (!_fs.ContainsKey(destination)) return destination;
        return conflict switch
        {
            ConflictPolicy.Skip => null,
            ConflictPolicy.KeepBoth => NextFreeName(destination),
            _ => destination
        };
    }

    private string NextFreeName(string destination)
    {
        var dir = DirectoryOf(destination);
        var name = Path.GetFileNameWithoutExtension(destination);
        var ext = Path.GetExtension(destination);
        for (var i = 2; ; i++)
        {
            var candidate = string.IsNullOrEmpty(dir) ? $"{name} ({i}){ext}" : $"{dir}/{name} ({i}){ext}";
            if (!_fs.ContainsKey(candidate)) return candidate;
        }
    }

    public Task<Result<PermissionCapabilities>> CheckCapabilitiesAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Result<PermissionCapabilities>.Ok(PermissionCapabilities.Default));

    public Task<Result<long>> GetFreeSpaceAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Result<long>.Ok(FreeSpace));

    public Task<Result<bool>> VerifyAsync(string path, string expectedFingerprint, CancellationToken ct = default)
    {
        CallLog.Add($"verify:{path}");
        if (!_fs.TryGetValue(path, out var n) || n.IsDirectory)
            return Task.FromResult(Result<bool>.Fail(FileMindError.NotFound($"'{path}' not found.")));
        var actual = FileIdentity.ComputeFingerprint(n.Content.LongLength, n.ModifiedAt);
        return Task.FromResult(Result<bool>.Ok(actual == expectedFingerprint));
    }

    public IReadOnlyList<string> GetProtectedPaths() => _protected;
}
