using FileMind.Domain.Classification;
using Xunit;

namespace FileMind.Domain.Tests;

public class SubjectClustererTests
{
    private static readonly SubjectClusterer Sut = new();

    [Fact]
    public void SharedCourseCode_ClustersAcrossNames()
    {
        var found = Sut.Detect([
            "IT421_Lab3.pdf", "IT-421 notes.docx", "IT 421 lecture.mp4",
            "random_photo.jpg"
        ]);
        Assert.Equal(3, found["IT-421"]);
        Assert.Single(found);
    }

    [Fact]
    public void SeparatorVariants_NormalizeToOneGroup()
    {
        var found = Sut.Detect(["CS301_a.pdf", "cs 301_b.pdf", "CS-301_c.pdf"]);
        Assert.Equal(["CS-301"], found.Keys);
    }

    [Fact]
    public void SingleOccurrence_NotACluster()
    {
        var found = Sut.Detect(["IT421_only.pdf", "readme.txt", "todo.docx"]);
        Assert.Empty(found);
    }

    [Fact]
    public void DenyListTokens_DoNotCluster()
    {
        var found = Sut.Detect(["IMG_2024_1.jpg", "IMG_2024_2.jpg", "IMG_2024_3.jpg"]);
        Assert.Empty(found);
    }

    [Fact]
    public void CodeInsideLongerWord_NotDetected()
    {
        var found = Sut.Detect(["Acceptance-2026.pdf", "Acceptance-2026b.pdf"]);
        Assert.Empty(found);
    }

    [Fact]
    public void MultipleDistinctCodes_AllCluster()
    {
        var found = Sut.Detect([
            "IT421_a.pdf", "IT421_b.pdf",
            "MATH210_x.pdf", "MATH-210_y.pdf",
            "unrelated.txt"
        ]);
        Assert.Equal(2, found.Count);
        Assert.Equal(2, found["IT-421"]);
        Assert.Equal(2, found["MATH-210"]);
    }

    [Fact]
    public void CodeIn_ReturnsDetectedCodeOrNull()
    {
        var detected = new HashSet<string> { "IT-421" };
        Assert.Equal("IT-421", Sut.CodeIn("IT_421_final.pdf", detected));
        Assert.Null(Sut.CodeIn("IT_422_final.pdf", detected));
        Assert.Null(Sut.CodeIn("plain_document.pdf", detected));
    }
}
