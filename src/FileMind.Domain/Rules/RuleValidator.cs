using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Domain.Rules;

public static class RuleValidator
{
    public const int CurrentSchemaVersion = 1;

    public static Result<Rule> Validate(Rule rule)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rule.Id)) errors.Add("Rule.Id is required.");
        if (string.IsNullOrWhiteSpace(rule.Name)) errors.Add("Rule.Name is required.");
        if (rule.SchemaVersion > CurrentSchemaVersion)
            return Result<Rule>.Fail(new FileMindError(
                ErrorCode.UnsupportedOperation,
                $"Rule schema version {rule.SchemaVersion} is newer than supported {CurrentSchemaVersion}.",
                RetrySafe: false, RequiresReview: true));
        if (rule.SchemaVersion < 1) errors.Add("Rule.SchemaVersion must be >= 1.");
        if (rule.ConditionRoot.Depth > RuleLimits.MaxConditionNestingDepth)
            errors.Add($"Condition nesting depth {rule.ConditionRoot.Depth} exceeds max {RuleLimits.MaxConditionNestingDepth}.");
        if (rule.ConditionRoot.Conditions.Count == 0 && rule.ConditionRoot.Groups.Count == 0)
            errors.Add("Rule must have at least one condition.");
        if (rule.Actions.Count == 0) errors.Add("Rule must have at least one action.");

        foreach (var action in rule.Actions)
        {
            if (action.Kind is RuleActionKind.Move or RuleActionKind.Copy or RuleActionKind.CreateFolder
                    && string.IsNullOrWhiteSpace(action.DestinationTemplate))
                errors.Add($"Action {action.Kind} requires DestinationTemplate.");
            if (action.Kind == RuleActionKind.Rename && string.IsNullOrWhiteSpace(action.NamingTemplate))
                errors.Add("Rename action requires NamingTemplate.");
            if (action.Kind == RuleActionKind.RouteToCollection && string.IsNullOrWhiteSpace(action.Collection))
                errors.Add("RouteToCollection action requires Collection.");
        }

        var conditionError = ValidateGroup(rule.ConditionRoot);
        if (conditionError is not null) errors.Add(conditionError);

        return errors.Count == 0
            ? Result<Rule>.Ok(rule)
            : Result<Rule>.Fail(FileMindError.Validation(string.Join(' ', errors)));
    }

    private static string? ValidateGroup(ConditionGroup group)
    {
        foreach (var c in group.Conditions)
        {
            if (string.IsNullOrWhiteSpace(c.Value) && c.Operator is not ConditionOperator.NotContains)
                return $"Condition on {c.Field} requires a value.";
            if (c.Field == ConditionField.Metadata && string.IsNullOrWhiteSpace(c.MetadataKey))
                return "Metadata condition requires MetadataKey.";
        }
        foreach (var g in group.Groups)
        {
            var err = ValidateGroup(g);
            if (err is not null) return err;
        }
        return null;
    }
}
