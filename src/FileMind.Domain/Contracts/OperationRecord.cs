using FileMind.Domain.Errors;

namespace FileMind.Domain.Contracts;

public sealed record OperationRecord
{
    public required string OperationId { get; init; }
    public required string PlanId { get; init; }
    public required OperationStatus Status { get; init; }
    public required IReadOnlyList<OperationItemState> Items { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CommittedAt { get; init; }
    public string? UndoOfOperationId { get; init; }
    public FileMindError? Error { get; init; }

    public bool IsTerminal => Status is OperationStatus.Committed
        or OperationStatus.Failed or OperationStatus.RolledBack;
}

public sealed record OperationItemState
{
    public required string ItemId { get; init; }
    public required OperationType Operation { get; init; }
    public required string OriginalLocation { get; init; }
    public string? ResultingLocation { get; init; }
    public required OperationStatus Status { get; init; }
    public string? Verification { get; init; }
    public FileMindError? Error { get; init; }
    public UndoRecipe? UndoRecipe { get; init; }
}
