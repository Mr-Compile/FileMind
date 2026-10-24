using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Intelligence.Local.Analysis;

/// <summary>Hard bounds for one file's extraction — never exceeded.</summary>
public sealed record ExtractionBudget(
    long MaxBytes = 4 * 1024 * 1024,
    int MaxChars = 8192,
    TimeSpan? Timeout = null)
{
    public TimeSpan TimeoutOrDefault => Timeout ?? TimeSpan.FromSeconds(10);
}

/// <summary>What one analyzer extracted from one file.</summary>
public sealed record AnalyzerOutput
{
    /// <summary>Bounded plain-text content (≤ budget.MaxChars). Never logged raw.</summary>
    public string? ExtractedText { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<Evidence> Evidence { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    /// <summary>True when the format parsed cleanly enough to trust metadata.</summary>
    public bool Reliable { get; init; } = true;
}

/// <summary>
/// Plugin-style analyzer: claims a file by extension/kind, extracts bounded
/// evidence. Implementations must never throw for corrupt input — return an
/// output with <see cref="AnalyzerOutput.Warnings"/> and Reliable=false.
/// </summary>
public interface IFileAnalyzer
{
    string Name { get; }
    bool CanAnalyze(FileCandidate candidate);
    Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget,
        CancellationToken ct = default);
}
