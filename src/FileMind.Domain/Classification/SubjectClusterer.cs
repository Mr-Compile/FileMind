using System.Text.RegularExpressions;

namespace FileMind.Domain.Classification;

/// <summary>
/// Infers subject groups from filename patterns without a configured catalog.
/// Filenames sharing a course-code-like token (IT-412, IT421, CS 301) cluster
/// into a canonical subject id LETTERS-DIGITS (IT-412), letting school files
/// route to School/{Subject}/{FileKind} on their own. Deterministic and
/// offline — inferred ids are ephemeral per scan, never persisted.
/// </summary>
public sealed class SubjectClusterer(int minGroup = 2)
{
    /// <summary>
    /// Cluster-backed inference is near code-match strength (catalog code = 65)
    /// so clustered files reach the auto-organize band on their own.
    /// </summary>
    public const double InferredScore = 60.0;
    // Word-bounded: letters 2-5, optional separator, 3-4 digits + optional
    // letter suffix. Boundaries stop matches inside longer words
    // ("Acceptance-2026" must not produce "ance-2026").
    private static readonly Regex Code = new(
        @"(?<![A-Za-z0-9])([A-Za-z]{2,5})[-_ ]?([0-9]{3,4}[A-Za-z]?)(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> Denied = new(StringComparer.Ordinal)
    {
        "img", "pxl", "vid", "dsc", "dscn", "screenshot", "screen", "photo",
        "wa", "wp", "mmexport", "received", "download", "doc", "file",
        "copy", "untitled", "final", "scan", "snap", "new", "temp", "mov"
    };

    private static string Key(Match m) =>
        $"{m.Groups[1].Value}-{m.Groups[2].Value}".ToUpperInvariant();

    /// <summary>Canonical code → matching filename count, for groups of >= minGroup.</summary>
    public IReadOnlyDictionary<string, int> Detect(IReadOnlyList<string> names)
    {
        var groups = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            foreach (Match m in Code.Matches(name))
            {
                if (Denied.Contains(m.Groups[1].Value.ToLowerInvariant())) continue;
                var k = Key(m);
                if (!groups.TryGetValue(k, out var set)) groups[k] = set = new HashSet<string>();
                set.Add(name);
            }
        }
        return groups.Where(g => g.Value.Count >= minGroup)
            .ToDictionary(g => g.Key, g => g.Value.Count);
    }

    /// <summary>The detected code contained in <paramref name="name"/>, or null.</summary>
    public string? CodeIn(string name, IReadOnlySet<string> detected) =>
        Code.Matches(name).Cast<Match>()
            .Where(m => !Denied.Contains(m.Groups[1].Value.ToLowerInvariant()))
            .Select(Key)
            .FirstOrDefault(detected.Contains);
}
