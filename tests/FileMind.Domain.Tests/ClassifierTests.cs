using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Testing;
using Xunit;

namespace FileMind.Domain.Tests;

public class ClassifierTests
{
    private static ClassifierEngine Engine()
    {
        var subjects = new InMemorySubjectCatalog(
        [
            new SubjectEntry("IT421", "IT421 Networks", "IT421", ["IT 421", "IT-421", "IT_421"], ["networks", "lecture", "lab"])
        ]);
        var projects = new InMemoryProjectCatalog(
        [
            new ProjectEntry("alpha", "Project Alpha", "Contoso", ["alpha"], ["status", "report"]),
            new ProjectEntry("acme", "ACME Engagement", "ACME", ["acme"], ["meeting"])
        ]);
        return new ClassifierEngine(new SubjectMatcher(subjects), new ProjectMatcher(projects), new ConfidenceEngine());
    }

    [Fact]
    public void ContextOverType_IT421LectureRoutesToSubject()
    {
        var result = Engine().Classify(Candidates.Create("dl/IT421_Lecture_5.mp4"));
        Assert.Equal(FileKind.Video, result.FileKind);
        Assert.Equal("IT421", result.Subject);
        Assert.True(result.Confidence >= 95);
        Assert.False(result.NeedsReview);
        Assert.Contains(result.Evidence, e => e.Kind == "context-over-type");
    }

    [Theory]
    [InlineData("IT 421 notes.docx")]
    [InlineData("IT-421_hw.pdf")]
    [InlineData("IT_421_lab.txt")]
    public void SubjectVariants_AllHigh(string fileName)
    {
        var result = Engine().Classify(Candidates.Create($"school/{fileName}"));
        Assert.Equal("IT421", result.Subject);
        Assert.True(result.Confidence >= 95, $"confidence={result.Confidence}");
    }

    [Fact]
    public void UnknownFile_LowConfidenceNoSubject()
    {
        var result = Engine().Classify(Candidates.Create("dl/calculus_notes.pdf"));
        Assert.Null(result.Subject);
        Assert.True(result.Confidence < 70);
    }

    [Fact]
    public void ProjectEvidence_MediumBand()
    {
        var result = Engine().Classify(Candidates.Create("work/alpha_status_report.docx"));
        Assert.Equal("alpha", result.Project);
        Assert.True(result.Confidence >= 70);
        Assert.True(result.Confidence < 95);
    }

    [Fact]
    public void Classification_Deterministic()
    {
        var c = Candidates.Create("dl/IT421_Lecture_5.mp4");
        var a = Engine().Classify(c);
        var b = Engine().Classify(c);
        Assert.Equal(a.Confidence, b.Confidence);
        Assert.Equal(a.Subject, b.Subject);
        Assert.Equal(a.Evidence.Count, b.Evidence.Count);
    }

    [Fact]
    public void UnicodeNames_DoNotThrow()
    {
        var result = Engine().Classify(Candidates.Create("dl/ファイル.txt"));
        Assert.Equal(FileKind.Document, result.FileKind);
    }
}
