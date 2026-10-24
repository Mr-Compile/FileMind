using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using Xunit;

namespace FileMind.Domain.Tests;

public class ConfidenceTests
{
    private readonly ConfidenceEngine _engine = new();

    [Theory]
    [InlineData(95, ConfidenceBand.High)]
    [InlineData(100, ConfidenceBand.High)]
    [InlineData(94.9, ConfidenceBand.Medium)]
    [InlineData(70, ConfidenceBand.Medium)]
    [InlineData(69.9, ConfidenceBand.Low)]
    [InlineData(0, ConfidenceBand.Low)]
    public void Band_Thresholds(double score, ConfidenceBand expected) =>
        Assert.Equal(expected, _engine.Band(score));

    [Fact]
    public void Score_ClampsAtMax()
    {
        var evidence = Enumerable.Range(0, 10)
            .Select(_ => new Evidence("subject-code", "x", 40, ClassifierSource.Local))
            .ToList();
        Assert.Equal(100, _engine.Score(evidence));
    }

    [Fact]
    public void Score_RespectsKindCaps()
    {
        var evidence = Enumerable.Range(0, 5)
            .Select(_ => new Evidence("subject-keyword", "kw", 15, ClassifierSource.Local))
            .ToList();
        Assert.Equal(15, _engine.Score(evidence));
    }

    [Fact]
    public void Score_Deterministic()
    {
        var evidence = new List<Evidence>
        {
            new("subject-code", "a", 40, ClassifierSource.Local),
            new("extension-signal", "b", 10, ClassifierSource.Local)
        };
        Assert.Equal(_engine.Score(evidence), _engine.Score(evidence));
    }
}
