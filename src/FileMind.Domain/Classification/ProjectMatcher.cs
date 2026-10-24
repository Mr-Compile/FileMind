using FileMind.Domain.Contracts;

namespace FileMind.Domain.Classification;

public sealed class ProjectMatcher(IProjectCatalog catalog)
{
    public const double AliasMatchScore = 35;
    public const double NameMatchScore = 25;
    public const double KeywordMatchScore = 12;

    public ProjectMatchResult Match(NormalizedTokens candidate)
    {
        var evidence = new List<Evidence>();
        var warnings = new List<string>();
        var matches = new List<(ProjectEntry Project, double Score, string Kind)>();

        foreach (var project in catalog.Projects)
        {
            if (project.Aliases.Any(a => Contains(candidate, a)))
            {
                matches.Add((project, AliasMatchScore, "project-alias"));
                continue;
            }
            if (Contains(candidate, project.Name) ||
                (project.ClientName is not null && Contains(candidate, project.ClientName)))
            {
                matches.Add((project, NameMatchScore, "project-name"));
                continue;
            }
            if (project.Keywords.Any(k => Contains(candidate, k)))
                matches.Add((project, KeywordMatchScore, "project-keyword"));
        }

        string? projectId = null;
        double score = 0;
        if (matches.Count > 0)
        {
            var best = matches.OrderByDescending(m => m.Score).ThenBy(m => m.Project.Id).First();
            projectId = best.Project.Id;
            score = best.Score;
            evidence.Add(new Evidence(best.Kind, $"Matched '{best.Project.Name}'", best.Score, ClassifierSource.Local));

            if (matches.Any(m => m.Project.Id != best.Project.Id && m.Score >= best.Score))
                warnings.Add("Multiple projects matched with equal strength.");
        }

        return new ProjectMatchResult(projectId, score, evidence, warnings);
    }

    private static bool Contains(NormalizedTokens candidate, string term)
    {
        var nt = Normalizer.Normalize(term).Joined;
        return nt.Length > 0 && candidate.Joined.Contains(nt, StringComparison.Ordinal);
    }
}

public sealed record ProjectMatchResult(
    string? ProjectId,
    double Score,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Warnings);
