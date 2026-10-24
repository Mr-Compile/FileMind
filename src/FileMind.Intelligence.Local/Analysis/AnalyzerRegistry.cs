using FileMind.Domain.Contracts;
using FileMind.Intelligence.Local.Analyzers;

namespace FileMind.Intelligence.Local.Analysis;

/// <summary>
/// Resolves analyzers for a candidate. Order matters: first matching analyzer
/// is the primary; all matching analyzers run and merge (fail-safe each).
/// </summary>
public sealed class AnalyzerRegistry
{
    private readonly List<IFileAnalyzer> _analyzers;

    public AnalyzerRegistry(IEnumerable<IFileAnalyzer>? analyzers = null) =>
        _analyzers = analyzers?.ToList() ?? AnalyzerDefaults.All();

    public IReadOnlyList<IFileAnalyzer> For(FileCandidate candidate) =>
        _analyzers.Where(a =>
        {
            try { return a.CanAnalyze(candidate); }
            catch { return false; }
        }).ToList();
}
