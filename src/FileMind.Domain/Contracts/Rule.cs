namespace FileMind.Domain.Contracts;

public static class RuleLimits
{
    public const int MaxConditionNestingDepth = 3;
}

public enum ConditionField
{
    Filename,
    Extension,
    MimeType,
    Keyword,
    SourceFolder,
    SourceScope,
    Size,
    CreatedAt,
    ModifiedAt,
    Metadata,
    ExtractedContent,
    Subject,
    Project,
    Collection,
    Category
}

public enum ConditionOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    Regex,
    GreaterThan,
    LessThan,
    In
}

public enum LogicalOperator
{
    And,
    Or
}

public sealed record ConditionGroup
{
    public required LogicalOperator Operator { get; init; }
    public IReadOnlyList<RuleCondition> Conditions { get; init; } = [];
    public IReadOnlyList<ConditionGroup> Groups { get; init; } = [];

    public int Depth =>
        1 + (Groups.Count == 0 ? 0 : Groups.Max(g => g.Depth));
}

public sealed record RuleCondition
{
    public required ConditionField Field { get; init; }
    public required ConditionOperator Operator { get; init; }
    public required string Value { get; init; }
    public string? MetadataKey { get; init; }

    public int Specificity => Field switch
    {
        ConditionField.Subject or ConditionField.Project => 4,
        ConditionField.Filename => 3,
        ConditionField.Keyword or ConditionField.Metadata or ConditionField.ExtractedContent => 2,
        _ => 1
    };
}

public enum RuleActionKind
{
    Move,
    Copy,
    Rename,
    Tag,
    Ignore,
    Notify,
    Ask,
    CreateFolder,
    RouteToCollection
}

public sealed record RuleAction
{
    public required RuleActionKind Kind { get; init; }
    public string? DestinationTemplate { get; init; }
    public string? NamingTemplate { get; init; }
    public string? Collection { get; init; }
    public string? Tag { get; init; }
    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.KeepBoth;
}

public enum RuleProvenance
{
    Builtin,
    User,
    Imported,
    AiSuggested
}

public sealed record Rule
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool Enabled { get; init; } = true;
    public int Priority { get; init; }
    public required ConditionGroup ConditionRoot { get; init; }
    public required IReadOnlyList<RuleAction> Actions { get; init; }
    public bool StopProcessing { get; init; }
    public string Mode { get; init; } = "all";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public required int SchemaVersion { get; init; }
    public RuleProvenance Provenance { get; init; } = RuleProvenance.User;

    public int ConditionCount => CountConditions(ConditionRoot);

    private static int CountConditions(ConditionGroup g) =>
        g.Conditions.Count + g.Groups.Sum(CountConditions);
}

public sealed record Decision
{
    public required DecisionKind Kind { get; init; }
    public required FileCandidate Candidate { get; init; }
    public required ClassificationResult Classification { get; init; }
    public string? DestinationTemplate { get; init; }
    public string? NamingTemplate { get; init; }
    public ConflictPolicy ConflictPolicy { get; init; } = ConflictPolicy.KeepBoth;
    public IReadOnlyList<string> Reasons { get; init; } = [];
}
