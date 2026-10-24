namespace FileMind.Domain.Contracts;

public sealed record FileIdentity
{
    public required string StableId { get; init; }
    public required string PathHint { get; init; }
    public required string Fingerprint { get; init; }

    public static string ComputeFingerprint(long sizeBytes, DateTimeOffset modifiedAt) =>
        $"{sizeBytes:x}:{modifiedAt.UtcTicks:x}";
}
