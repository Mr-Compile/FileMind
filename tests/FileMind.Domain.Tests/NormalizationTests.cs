using FileMind.Domain.Classification;
using Xunit;

namespace FileMind.Domain.Tests;

public class NormalizationTests
{
    [Theory]
    [InlineData("IT421", "IT 421")]
    [InlineData("IT421", "IT-421")]
    [InlineData("IT421", "IT_421")]
    [InlineData("IT421_Lecture_5.mp4", "it421 lecture 5 mp4")]
    [InlineData("  double  space  name ", "double space name")]
    public void EquivalentVariants_Match(string a, string b) =>
        Assert.True(Normalizer.Equivalent(a, b));

    [Theory]
    [InlineData("IT421_Lecture_5.mp4", new[] { "it421", "lecture", "5", "mp4" })]
    [InlineData("IT-421_hw", new[] { "it", "421", "hw" })]
    public void Tokens_SplitOnSeparators(string input, string[] expected) =>
        Assert.Equal(expected, Normalizer.Normalize(input).Tokens);

    [Fact]
    public void UnicodeAndEmoji_DoNotThrow()
    {
        var unicode = Normalizer.Normalize("ファイル.txt");
        var emoji = Normalizer.Normalize("notes_🎉_final.txt");
        Assert.Contains("ファイル", unicode.Tokens);
        Assert.Contains("final", emoji.Tokens);
    }

    [Fact]
    public void EmptyAndWhitespace_Safe()
    {
        Assert.Empty(Normalizer.Normalize("").Tokens);
        Assert.Empty(Normalizer.Normalize("   ").Tokens);
    }
}
