using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Domain.Ports;

public sealed record FileStat(
    string Path,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    bool IsDirectory,
    string? FileIndexId = null);

public sealed record FileEntry(
    string Path,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt);

public interface IFileSystemAdapter
{
    IAsyncEnumerable<FileEntry> EnumerateAsync(string root, bool recursive, CancellationToken ct = default);
    Task<Result<FileStat>> StatAsync(string path, CancellationToken ct = default);
    Task<Result<Stream>> OpenReadAsync(string path, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(string path, CancellationToken ct = default);
    Task<Result<bool>> CreateDirectoryAsync(string path, CancellationToken ct = default);
    Task<Result<string>> MoveAsync(string source, string destination, ConflictPolicy conflict, CancellationToken ct = default);
    Task<Result<string>> CopyAsync(string source, string destination, ConflictPolicy conflict, CancellationToken ct = default);
    Task<Result<string>> RenameAsync(string source, string newName, ConflictPolicy conflict, CancellationToken ct = default);
    Task<Result<PermissionCapabilities>> CheckCapabilitiesAsync(string path, CancellationToken ct = default);
    Task<Result<long>> GetFreeSpaceAsync(string path, CancellationToken ct = default);
    Task<Result<bool>> VerifyAsync(string path, string expectedFingerprint, CancellationToken ct = default);
    IReadOnlyList<string> GetProtectedPaths();
}

public interface IContentExtractor
{
    Task<Result<string>> ExtractTextAsync(FileCandidate candidate, int maxChars, CancellationToken ct = default);
}

public interface IMetadataExtractor
{
    Task<Result<IReadOnlyDictionary<string, string>>> ExtractAsync(FileCandidate candidate, CancellationToken ct = default);
}

/// <summary>Extraction output merged onto a FileCandidate before classification.</summary>
public sealed record CandidateEnrichment(
    IReadOnlyDictionary<string, string> Metadata,
    string? ExtractedText,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Warnings,
    bool Reliable);

/// <summary>
/// Local (offline) content/metadata enrichment. Implementations must be
/// bounded by size/time budgets and fail-safe per file — a corrupt or
/// unsupported file yields warnings, never an abort of the whole scan.
/// </summary>
public interface ICandidateEnricher
{
    Task<Result<CandidateEnrichment>> EnrichAsync(
        FileCandidate candidate, IFileSystemAdapter fs, CancellationToken ct = default);
}

public interface IHashService
{
    Task<Result<string>> HashAsync(string path, bool full, CancellationToken ct = default);
}

public interface IPathNormalizer
{
    string Normalize(string path);
    string Combine(params string[] parts);
    bool IsUnder(string path, string root);
    string FileName(string path);
    string? DirectoryOf(string path);
}

public interface IProtectedPathProvider
{
    bool IsProtected(string path);
    IReadOnlyList<string> ProtectedRoots { get; }
}

public interface IOperationStore
{
    Task<Result<OperationRecord>> LoadAsync(string operationId, CancellationToken ct = default);
    Task<Result<OperationRecord>> SaveAsync(OperationRecord record, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OperationRecord>>> LoadIncompleteAsync(CancellationToken ct = default);
}
