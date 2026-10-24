using FileMind.Application.UseCases;
using FileMind.Domain.Contracts;

namespace FileMind.Application.Decisions;

public enum ItemRoute
{
    Execute,
    SmartInbox,
    PreviewOnly,
    Skip
}

public sealed class ModePolicy(OrganizationMode mode)
{
    public OrganizationMode Mode { get; } = mode;

    public const string SettingsKey = "organization.mode";

    public ItemRoute RouteFor(AnalyzedFile analyzed, bool apply = false)
    {
        if (analyzed.Decision.Kind is DecisionKind.NoOp or DecisionKind.Ignore)
            return ItemRoute.Skip;

        return Mode switch
        {
            OrganizationMode.Manual when !apply => ItemRoute.PreviewOnly,
            OrganizationMode.Automatic => ItemRoute.Execute,
            _ => analyzed.Decision.Kind == DecisionKind.Review || analyzed.Classification.NeedsReview
                ? ItemRoute.SmartInbox
                : ItemRoute.Execute
        };
    }

    public bool ExecutionRequiresApply =>
        Mode == OrganizationMode.Manual;

    public static bool TryParse(string value, out OrganizationMode parsed) =>
        Enum.TryParse(value.Replace("-", ""), ignoreCase: true, out parsed);

    public static string Display(OrganizationMode mode) => mode switch
    {
        OrganizationMode.AiAssisted => "ai-assisted",
        OrganizationMode.LocalOnly => "local-only",
        _ => mode.ToString().ToLowerInvariant()
    };
}
