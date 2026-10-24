namespace FileMind.Domain.Contracts;

public sealed record SafetyResult
{
    public required bool Allowed { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? RequiredConfirmation { get; init; }

    public static SafetyResult Allow(params string[] warnings) =>
        new() { Allowed = true, Warnings = warnings };

    public static SafetyResult Block(params string[] blockers) =>
        new() { Allowed = false, Blockers = blockers };

    public static SafetyResult NeedsConfirmation(string confirmation, params string[] warnings) =>
        new() { Allowed = true, RequiredConfirmation = confirmation, Warnings = warnings };
}
