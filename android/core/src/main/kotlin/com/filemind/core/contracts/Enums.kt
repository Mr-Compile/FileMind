package com.filemind.core.contracts

enum class OperationType {
    Move, Copy, Rename, CreateFolder, Tag, Ignore, Notify, Ask
}

enum class ConflictPolicy {
    KeepBoth, Skip, Ask, ReplaceWithConfirmation
}

enum class OperationStatus {
    Planned, Validating, Executing, Verifying, Committed, Failed, RollingBack, RolledBack, NeedsAttention
}

enum class ConfidenceBand {
    High, Medium, Low
}

enum class ClassifierSource {
    Local, Rule, Ai
}

enum class SourceScope {
    Downloads, Documents, Desktop, Media, WatchedFolder, ExternalDrive, Other
}

enum class HashState {
    NotComputed, Partial, Full
}

enum class FileKind {
    Document, Image, Video, Audio, Archive, Code, Spreadsheet, Presentation, Executable, Font, Other
}

enum class DecisionKind {
    Organize, Review, Ignore, NoOp
}

enum class OrganizationMode {
    Manual, Smart, Automatic, AiAssisted, LocalOnly
}
