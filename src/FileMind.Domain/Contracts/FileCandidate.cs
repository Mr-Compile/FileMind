using System.Collections.ObjectModel;

namespace FileMind.Domain.Contracts;

public sealed record FileCandidate
{
    public required string ScanId { get; init; }
    public required string SourceUri { get; init; }
    public required string DisplayName { get; init; }
    public required string Extension { get; init; }
    public string? MimeType { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ModifiedAt { get; init; }
    public required SourceScope SourceScope { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;
    public string? ExtractedTextRef { get; init; }
    public HashState HashState { get; init; } = HashState.NotComputed;
    public string? ContentHash { get; init; }
    public PermissionCapabilities Capabilities { get; init; } = PermissionCapabilities.Default;
    public IReadOnlyDictionary<string, string> PlatformHints { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;
}

public sealed record PermissionCapabilities(
    bool CanRead,
    bool CanWrite,
    bool CanMove,
    bool CanCopy,
    bool CanRename,
    bool CanCreateFolder)
{
    public static PermissionCapabilities Default { get; } = new(true, true, true, true, true, true);
}
