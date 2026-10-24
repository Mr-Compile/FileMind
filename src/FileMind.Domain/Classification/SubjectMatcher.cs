using FileMind.Domain.Contracts;

namespace FileMind.Domain.Classification;

public sealed class SubjectMatcher(ISubjectCatalog catalog)
{
    public const double CodeMatchScore = 65;
    public const double NameAliasMatchScore = 45;
    public const double KeywordMatchScore = 15;
    public const double AmbiguityPenalty = 10;

    public SubjectMatchResult Match(NormalizedTokens candidate)
    {
        var matches = new List<(SubjectEntry Subject, double Score, string Kind)>();

        foreach (var subject in catalog.Subjects)
        {
            if (subject.Code is not null &&
                ContainsCode(candidate, Normalizer.Normalize(subject.Code).Joined))
            {
                matches.Add((subject, CodeMatchScore, "subject-code"));
                continue;
            }

            var namedMatch =
                EquivalentToAny(candidate, subject.Name, subject.Aliases);
            if (namedMatch)
            {
                matches.Add((subject, NameAliasMatchScore, "subject-alias"));
                continue;
            }

            if (ContainsKeyword(candidate, subject.Keywords))
                matches.Add((subject, KeywordMatchScore, "subject-keyword"));
        }

        var evidence = new List<Evidence>();
        string? subjectId = null;
        var warnings = new List<string>();
        double score = 0;

        if (matches.Count > 0)
        {
            var best = matches.OrderByDescending(m => m.Score).ThenBy(m => m.Subject.Id).First();
            subjectId = best.Subject.Id;
            score = best.Score;
            evidence.Add(new Evidence(best.Kind, $"Matched '{best.Subject.Name}'", best.Score, ClassifierSource.Local));

            var ambiguous = matches
                .Where(m => m.Subject.Id != best.Subject.Id && m.Score >= best.Score)
                .ToList();
            if (ambiguous.Count > 0)
            {
                score -= AmbiguityPenalty;
                warnings.Add($"Ambiguous subject match: {string.Join(", ", ambiguous.Select(a => a.Subject.Name))} also matched.");
            }
        }

        return new SubjectMatchResult(subjectId, score, evidence, warnings);
    }

    private static bool ContainsCode(NormalizedTokens candidate, string normalizedCode)
    {
        if (normalizedCode.Length == 0) return false;
        var joined = candidate.Joined;
        if (joined.Contains(normalizedCode, StringComparison.Ordinal)) return true;
        var compactCode = normalizedCode.Replace(" ", "");
        var compactCandidate = string.Concat(candidate.Tokens);
        return compactCode.Length > 0 && compactCandidate.Contains(compactCode, StringComparison.Ordinal);
    }

    private static bool EquivalentToAny(NormalizedTokens candidate, string name, IReadOnlyList<string> aliases)
    {
        var joined = candidate.Joined;
        if (Normalizer.Normalize(name).Joined is { Length: > 0 } n && joined.Contains(n, StringComparison.Ordinal))
            return true;
        return aliases.Any(a =>
            Normalizer.Normalize(a).Joined is { Length: > 0 } na &&
            joined.Contains(na, StringComparison.Ordinal));
    }

    private static bool ContainsKeyword(NormalizedTokens candidate, IReadOnlyList<string> keywords) =>
        keywords.Any(k =>
            Normalizer.Normalize(k).Joined is { Length: > 0 } nk &&
            candidate.Joined.Contains(nk, StringComparison.Ordinal));
}

public sealed record SubjectMatchResult(
    string? SubjectId,
    double Score,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Warnings);
