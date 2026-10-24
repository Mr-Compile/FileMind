using System.Text.RegularExpressions;

namespace FileMind.Application.Scanning;

public sealed class ExclusionMatcher
{
    private readonly IReadOnlyList<Regex> _patterns;

    public ExclusionMatcher(IEnumerable<string> globPatterns) =>
        _patterns = globPatterns.Select(GlobToRegex).ToList();

    public bool IsExcluded(string pathOrName) =>
        _patterns.Any(p => p.IsMatch(pathOrName));

    private static Regex GlobToRegex(string glob)
    {
        var pattern = Regex.Escape(glob)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", @"[^/\\]*")
            .Replace(@"\?", ".");
        return new Regex($"^{pattern}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
