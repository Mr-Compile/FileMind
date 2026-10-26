using System.Runtime.CompilerServices;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Platform.Windows;

public sealed class WindowsFileSystemAdapter : IFileSystemAdapter
{
    private readonly WindowsPathNormalizer _paths = new();
    private readonly WindowsProtectedPaths _protected;

    public WindowsFileSystemAdapter(IEnumerable<string>? userProtectedPaths = null) =>
        _protected = new WindowsProtectedPaths(userProtectedPaths);

    public IProtectedPathProvider ProtectedPaths => _protected;

    public async IAsyncEnumerable<FileEntry> EnumerateAsync(
        string root, bool recursive, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
            ReturnSpecialDirectories = false
        };

        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(root, "*", options);
        }
        catch (Exception ex) when (Mapable(ex))
        {
            yield break;
        }

        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            FileInfo info;
            try { info = new FileInfo(path); }
            catch { continue; }
            yield return new FileEntry(
                info.FullName, info.Length,
                new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero),
                new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                DisplayName: info.Name);
            await Task.Yield();
        }
    }

    public Task<Result<FileStat>> StatAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Guard<FileStat>(() =>
        {
            if (Directory.Exists(path))
            {
                var d = new DirectoryInfo(path);
                return Result<FileStat>.Ok(new FileStat(d.FullName, 0,
                    new DateTimeOffset(d.CreationTimeUtc, TimeSpan.Zero),
                    new DateTimeOffset(d.LastWriteTimeUtc, TimeSpan.Zero), true));
            }
            var f = new FileInfo(path);
            if (!f.Exists) return Result<FileStat>.Fail(FileMindError.NotFound($"'{path}' not found."));
            return Result<FileStat>.Ok(new FileStat(f.FullName, f.Length,
                new DateTimeOffset(f.CreationTimeUtc, TimeSpan.Zero),
                new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero), false));
        }));

    public Task<Result<Stream>> OpenReadAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Guard<Stream>(() =>
        {
            Stream s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Result<Stream>.Ok(s);
        }));

    public Task<Result<bool>> ExistsAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Result<bool>.Ok(File.Exists(path) || Directory.Exists(path)));

    public Task<Result<bool>> CreateDirectoryAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Guard<bool>(() =>
        {
            Directory.CreateDirectory(path);
            return Result<bool>.Ok(true);
        }));

    public Task<Result<string>> MoveAsync(string source, string destination, ConflictPolicy conflict, CancellationToken ct = default) =>
        Task.FromResult(Guard<string>(() => Transfer(source, destination, conflict,
            (s, d, o) => File.Move(s, d, o))));

    public Task<Result<string>> CopyAsync(string source, string destination, ConflictPolicy conflict, CancellationToken ct = default) =>
        Task.FromResult(Guard<string>(() => Transfer(source, destination, conflict,
            (s, d, o) => File.Copy(s, d, o))));

    public Task<Result<string>> RenameAsync(string source, string newName, ConflictPolicy conflict, CancellationToken ct = default)
    {
        var dir = _paths.DirectoryOf(source);
        var destination = dir is null ? newName : _paths.Combine(dir, newName);
        return MoveAsync(source, destination, conflict, ct);
    }

    public Task<Result<PermissionCapabilities>> CheckCapabilitiesAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Guard<PermissionCapabilities>(() =>
        {
            var exists = File.Exists(path) || Directory.Exists(path);
            if (!exists)
                return Result<PermissionCapabilities>.Fail(FileMindError.NotFound($"'{path}' not found."));

            var readOnly = false;
            try
            {
                readOnly = (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
            }
            catch { }

            return Result<PermissionCapabilities>.Ok(new PermissionCapabilities(
                CanRead: true,
                CanWrite: !readOnly,
                CanMove: !readOnly,
                CanCopy: true,
                CanRename: !readOnly,
                CanCreateFolder: true));
        }));

    public Task<Result<long>> GetFreeSpaceAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Guard<long>(() =>
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (root is null)
                return Result<long>.Fail(FileMindError.Validation($"Cannot determine drive root for '{path}'."));
            return Result<long>.Ok(new DriveInfo(root).AvailableFreeSpace);
        }));

    public Task<Result<bool>> VerifyAsync(string path, string expectedFingerprint, CancellationToken ct = default) =>
        Task.FromResult(Guard<bool>(() =>
        {
            var f = new FileInfo(path);
            if (!f.Exists) return Result<bool>.Fail(FileMindError.NotFound($"'{path}' not found."));
            var actual = FileIdentity.ComputeFingerprint(f.Length,
                new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero));
            return Result<bool>.Ok(actual == expectedFingerprint);
        }));

    public IReadOnlyList<string> GetProtectedPaths() => _protected.ProtectedRoots;

    private Result<string> Transfer(string source, string destination, ConflictPolicy conflict,
        Action<string, string, bool> transfer)
    {
        if (!File.Exists(source))
            return Result<string>.Fail(FileMindError.NotFound($"'{source}' not found."));

        var dest = destination;
        if (File.Exists(dest))
        {
            switch (conflict)
            {
                case ConflictPolicy.Skip:
                    return Result<string>.Ok(source);
                case ConflictPolicy.Ask:
                    return Result<string>.Fail(FileMindError.Conflict(
                        $"'{dest}' exists; user decision required."));
                case ConflictPolicy.KeepBoth:
                    dest = NextFreeName(dest);
                    break;
                case ConflictPolicy.ReplaceWithConfirmation:
                    transfer(source, dest, true);
                    return Result<string>.Ok(dest);
            }
        }

        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        transfer(source, dest, false);
        return Result<string>.Ok(dest);
    }

    private string NextFreeName(string destination)
    {
        var dir = Path.GetDirectoryName(destination) ?? "";
        var name = Path.GetFileNameWithoutExtension(destination);
        var ext = Path.GetExtension(destination);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static bool Mapable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private static Result<T> Guard<T>(Func<Result<T>> body)
    {
        try { return body(); }
        catch (Exception ex) { return Result<T>.Fail(Map(ex)); }
    }

    private static FileMindError Map(Exception ex) => ex switch
    {
        FileNotFoundException or DirectoryNotFoundException => FileMindError.NotFound(ex.Message),
        UnauthorizedAccessException or System.Security.SecurityException =>
            new FileMindError(ErrorCode.PermissionDenied, ex.Message, false, true),
        IOException when ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) =>
            FileMindError.FileLocked(ex.Message),
        IOException => FileMindError.Io(ex.Message),
        OperationCanceledException => FileMindError.Cancelled(),
        _ => FileMindError.Io(ex.Message)
    };
}
