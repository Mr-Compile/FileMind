namespace FileMind.Domain.Contracts;

public sealed record ActionPlan
{
    public required string PlanId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string SourceDecision { get; init; }
    public required IReadOnlyList<ActionItem> Items { get; init; }
    public bool RequiresReview { get; init; }
    public bool DryRun { get; init; }
    public bool ReversibleEstimate { get; init; }
}

public sealed record ActionItem
{
    public required OperationType Operation { get; init; }
    public required string Source { get; init; }
    public string? Destination { get; init; }
    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.KeepBoth;
    public IReadOnlyList<string> Preconditions { get; init; } = [];
    public required string Reason { get; init; }
    public UndoRecipe? UndoRecipe { get; init; }

    public bool RequiresDestination => Operation is OperationType.Move
        or OperationType.Copy or OperationType.Rename or OperationType.CreateFolder;

    public bool IsValid =>
        !RequiresDestination || !string.IsNullOrWhiteSpace(Destination);
}

public sealed record UndoRecipe(
    OperationType ReverseOperation,
    string ReverseSource,
    string ReverseDestination,
    string Fingerprint,
    IReadOnlyList<string> Preconditions);
