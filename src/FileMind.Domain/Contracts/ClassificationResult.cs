namespace FileMind.Domain.Contracts;

public sealed record Evidence(
    string Kind,
    string Detail,
    double ScoreContribution,
    ClassifierSource Source);

public sealed record ClassificationResult
{
    public string? Collection { get; init; }
    public string? Category { get; init; }
    public string? Subcategory { get; init; }
    public string? Subject { get; init; }
    public string? Project { get; init; }
    public required FileKind FileKind { get; init; }
    public required IReadOnlyList<Evidence> Evidence { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<ClassifierSource> Sources { get; init; }
    public required string Explanation { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool NeedsReview { get; init; }

    public static ClassificationResult Unknown(string explanation) => new()
    {
        FileKind = FileKind.Other,
        Evidence = [],
        Confidence = 0,
        Sources = [ClassifierSource.Local],
        Explanation = explanation
    };
}
