namespace FileMind.Domain.Contracts;

public enum OperationType
{
    Move,
    Copy,
    Rename,
    CreateFolder,
    Tag,
    Ignore,
    Notify,
    Ask
}

public enum ConflictPolicy
{
    KeepBoth,
    Skip,
    Ask,
    ReplaceWithConfirmation
}

public enum OperationStatus
{
    Planned,
    Validating,
    Executing,
    Verifying,
    Committed,
    Failed,
    RollingBack,
    RolledBack,
    NeedsAttention
}

public enum ConfidenceBand
{
    High,
    Medium,
    Low
}

public enum ClassifierSource
{
    Local,
    Rule,
    Ai
}

public enum SourceScope
{
    Downloads,
    Documents,
    Desktop,
    Media,
    WatchedFolder,
    ExternalDrive,
    Other
}

public enum HashState
{
    NotComputed,
    Partial,
    Full
}

public enum FileKind
{
    Document,
    Image,
    Video,
    Audio,
    Archive,
    Code,
    Spreadsheet,
    Presentation,
    Executable,
    Font,
    Other
}

public enum DecisionKind
{
    Organize,
    Review,
    Ignore,
    NoOp
}

public enum OrganizationMode
{
    Manual,
    Smart,
    Automatic,
    AiAssisted,
    LocalOnly
}
