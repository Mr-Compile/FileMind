using FileMind.Domain.Classification;
using Xunit;

namespace FileMind.Domain.Tests;

public class MatcherTests
{
    private static readonly SubjectEntry It421 = new(
        "IT421", "IT421 Networks", "IT421", ["IT 421", "IT-421", "IT_421"], ["networks", "lecture", "lab"]);
    private static readonly SubjectEntry Cs101 = new(
        "CS101", "CS101 Intro", "CS101", ["computer science"], ["programming"]);

    private static SubjectMatcher Subjects() => new(new InMemorySubjectCatalog([It421, Cs101]));

    private static readonly ProjectEntry Alpha = new(
        "alpha", "Project Alpha", "Contoso", ["alpha"], ["status", "report"]);
    private static readonly ProjectEntry Acme = new(
        "acme", "ACME Engagement", "ACME", ["acme"], ["meeting"]);

    private static ProjectMatcher Projects() => new(new InMemoryProjectCatalog([Alpha, Acme]));

    [Theory]
    [InlineData("IT421_Lecture_5.mp4")]
    [InlineData("IT 421 notes.docx")]
    [InlineData("IT-421_hw.pdf")]
    [InlineData("IT_421_lab.txt")]
    public void SubjectCode_AllVariantsMatch(string fileName)
    {
        var result = Subjects().Match(Normalizer.Normalize(fileName));
        Assert.Equal("IT421", result.SubjectId);
        Assert.Equal(SubjectMatcher.CodeMatchScore, result.Score);
    }

    [Fact]
    public void SubjectKeyword_LowerScoreThanCode()
    {
        var result = Subjects().Match(Normalizer.Normalize("my_lecture_recording.mp4"));
        Assert.Equal("IT421", result.SubjectId);
        Assert.True(result.Score < SubjectMatcher.CodeMatchScore);
    }

    [Fact]
    public void UnknownSubject_ReturnsNull()
    {
        var result = Subjects().Match(Normalizer.Normalize("random_file.xyz"));
        Assert.Null(result.SubjectId);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void ConflictingAliases_LowerScoreWithWarning()
    {
        var sharedAlias = new SubjectEntry("SHARED", "Shared", null, [], ["networks"]);
        var matcher = new SubjectMatcher(new InMemorySubjectCatalog([It421, sharedAlias]));
        var result = matcher.Match(Normalizer.Normalize("networks_assignment.pdf"));
        Assert.NotEmpty(result.Warnings);
        Assert.True(result.Score < SubjectMatcher.KeywordMatchScore);
    }

    [Fact]
    public void ProjectAlias_BeatsGenericKeyword()
    {
        var result = Projects().Match(Normalizer.Normalize("alpha_status_report.docx"));
        Assert.Equal("alpha", result.ProjectId);
        Assert.Equal(ProjectMatcher.AliasMatchScore, result.Score);
    }

    [Fact]
    public void ClientName_MatchesProject()
    {
        var result = Projects().Match(Normalizer.Normalize("invoice_contoso_q3.pdf"));
        Assert.Equal("alpha", result.ProjectId);
    }

    [Fact]
    public void UnknownProject_ReturnsNull()
    {
        var result = Projects().Match(Normalizer.Normalize("personal_todo.txt"));
        Assert.Null(result.ProjectId);
    }
}
