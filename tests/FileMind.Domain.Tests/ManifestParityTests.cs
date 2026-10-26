using System.Text.Json;
using FileMind.Application.Scanning;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Domain.Ports;
using FileMind.Testing;

namespace FileMind.Domain.Tests;

/// <summary>
/// Parity suite over tests/fixtures/manifest.json — the same expectations are
/// asserted on Android by com.filemind.core.parity.ManifestParityTest.
/// </summary>
public class ManifestParityTests
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly DateTimeOffset FixedTime =
        DateTimeOffset.Parse("2026-01-15T10:00:00Z");

    private static ManifestDoc LoadManifest()
    {
        var asm = typeof(ManifestParityTests).Assembly;
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("fixtures.manifest.json"));
        using var s = asm.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<ManifestDoc>(s, JsonOpts)!;
    }

    private readonly ManifestDoc _manifest = LoadManifest();

    private readonly ClassifierEngine _classifier;
    private readonly ExclusionMatcher _exclusions;
    private readonly ActionPlanner _planner = new();
    private readonly IPathNormalizer _paths = new WindowsStylePathNormalizer();

    public ManifestParityTests()
    {
        _classifier = new ClassifierEngine(
            new SubjectMatcher(new InMemorySubjectCatalog(
                _manifest.Catalog.Subjects.Select(s =>
                    new SubjectEntry(s.Id, s.Name, s.Code, s.Aliases, s.Keywords)).ToList())),
            new ProjectMatcher(new InMemoryProjectCatalog(
                _manifest.Catalog.Projects.Select(p =>
                    new ProjectEntry(p.Id, p.Name, p.ClientName, p.Aliases, p.Keywords)).ToList())),
            new ConfidenceEngine());
        _exclusions = new ExclusionMatcher(_manifest.Exclusions);
    }

    private static FileCandidate CandidateFor(ManifestCase c)
    {
        var name = c.Path.Split('/', '\\').Last();
        var ext = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : "";
        return new FileCandidate
        {
            ScanId = $"parity-{c.Path.GetHashCode():x}",
            SourceUri = c.Path,
            DisplayName = name,
            Extension = ext,
            MimeType = MimeMap.FromExtension(ext),
            SizeBytes = 1024,
            CreatedAt = FixedTime,
            ModifiedAt = FixedTime,
            SourceScope = SourceScope.Other
        };
    }

    [Fact]
    public void FixtureLoadsAndHasCases()
    {
        Assert.NotEmpty(_manifest.Cases);
        Assert.NotEmpty(_manifest.Exclusions);
    }

    [Fact]
    public void ExcludedCasesAreExcludedBySharedPatterns()
    {
        var expected = _manifest.Cases.Where(c => c.ExpectedExcluded == true).ToList();
        Assert.NotEmpty(expected);
        foreach (var c in expected)
        {
            var name = c.Path.Split('/', '\\').Last();
            var normalized = c.Path.Replace('\\', '/');
            Assert.True(_exclusions.IsExcluded(name) || _exclusions.IsExcluded(normalized),
                $"expected excluded but was not: {c.Path}");
        }
    }

    [Fact]
    public void IncludedCasesAreNotExcluded()
    {
        foreach (var c in _manifest.Cases.Where(c => c.ExpectedExcluded != true))
        {
            var name = c.Path.Split('/', '\\').Last();
            var normalized = c.Path.Replace('\\', '/');
            Assert.True(!_exclusions.IsExcluded(name) && !_exclusions.IsExcluded(normalized),
                $"unexpectedly excluded: {c.Path}");
        }
    }

    [Fact]
    public void ClassificationsMatchManifestExpectations()
    {
        foreach (var c in _manifest.Cases.Where(c => c.ExpectedExcluded != true))
        {
            var result = _classifier.Classify(CandidateFor(c));
            if (c.ExpectedFileKind is not null)
                Assert.Equal(Enum.Parse<FileKind>(c.ExpectedFileKind), result.FileKind);
            if (c.ExpectedSubject is not null)
                Assert.Equal(c.ExpectedSubject, result.Subject);
            if (c.ExpectedProject is not null)
                Assert.Equal(c.ExpectedProject, result.Project);
            if (c.ExpectedBand is not null)
                Assert.Equal(Enum.Parse<ConfidenceBand>(c.ExpectedBand),
                    new ConfidenceEngine().Band(result.Confidence));
        }
    }

    [Fact]
    public void RoutedCasesLandUnderExpectedRoutePrefix()
    {
        // expectedRoute encodes collection+subject intent ("context beats
        // type"); parity asserts the first two segments, as on Android.
        foreach (var c in _manifest.Cases.Where(c => c.ExpectedRoute is not null))
        {
            var candidate = CandidateFor(c);
            var classification = _classifier.Classify(candidate);
            var expectedPrefix = string.Join('/',
                c.ExpectedRoute!.Split('/').Take(2));
            var template = classification.Subject is not null ? "School/{Subject}/{FileKind}"
                : classification.Project is not null ? "Work/{Project}/{FileKind}"
                : "Files/{FileKind}";
            var decision = new Decision
            {
                Kind = DecisionKind.Organize,
                Candidate = candidate,
                Classification = classification,
                DestinationTemplate = template
            };
            var plan = _planner.Plan(decision,
                new PlanContext(_paths, Now: FixedTime));
            Assert.True(plan.IsSuccess, $"plan failed for {c.Path}: {plan.Error?.Message}");
            var dest = plan.Value!.Items.Single().Destination!.Replace('\\', '/');
            Assert.True(dest.StartsWith(expectedPrefix, StringComparison.Ordinal),
                $"route mismatch for {c.Path}: '{dest}' does not start with '{expectedPrefix}'");
        }
    }

    [Fact]
    public void FingerprintFormatMatchesTicksContract()
    {
        // Golden value shared with the Kotlin suite: UtcTicks of
        // 2026-01-15T10:00:00Z = 0x8de541cd86c9000.
        var fp = FileIdentity.ComputeFingerprint(1024, FixedTime);
        Assert.Equal("400:8de541cd86c9000", fp);
        Assert.Matches("^[0-9a-f]+:[0-9a-f]+$", fp);
    }

    // --- manifest.json DTOs (camelCase) ---

    public sealed class ManifestDoc
    {
        public int Version { get; set; }
        public CatalogDoc Catalog { get; set; } = new();
        public List<string> Exclusions { get; set; } = [];
        public List<ManifestCase> Cases { get; set; } = [];
    }

    public sealed class CatalogDoc
    {
        public List<SubjectDoc> Subjects { get; set; } = [];
        public List<ProjectDoc> Projects { get; set; } = [];
    }

    public sealed class SubjectDoc
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public List<string> Aliases { get; set; } = [];
        public List<string> Keywords { get; set; } = [];
    }

    public sealed class ProjectDoc
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? ClientName { get; set; }
        public List<string> Aliases { get; set; } = [];
        public List<string> Keywords { get; set; } = [];
    }

    public sealed class ManifestCase
    {
        public string Path { get; set; } = "";
        public string? ExpectedFileKind { get; set; }
        public string? ExpectedSubject { get; set; }
        public string? ExpectedProject { get; set; }
        public string? ExpectedBand { get; set; }
        public string? ExpectedRoute { get; set; }
        public bool? ExpectedExcluded { get; set; }
    }
}
