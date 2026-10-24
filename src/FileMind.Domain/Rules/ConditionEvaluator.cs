using System.Text.RegularExpressions;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;

namespace FileMind.Domain.Rules;

public static class ConditionEvaluator
{
    public static bool Evaluate(ConditionGroup group, FileCandidate candidate, ClassificationResult classification) =>
        group.Operator == LogicalOperator.And
            ? group.Conditions.All(c => EvaluateCondition(c, candidate, classification)) &&
              group.Groups.All(g => Evaluate(g, candidate, classification))
            : group.Conditions.Any(c => EvaluateCondition(c, candidate, classification)) ||
              group.Groups.Any(g => Evaluate(g, candidate, classification));

    private static bool EvaluateCondition(RuleCondition c, FileCandidate candidate, ClassificationResult classification)
    {
        var normalizedValue = Normalizer.Normalize(c.Value);
        var compare = comparison(c.Field, candidate);

        return c.Operator switch
        {
            ConditionOperator.Equals => compare is not null && Normalizer.Equivalent(compare, c.Value),
            ConditionOperator.NotEquals => compare is null || !Normalizer.Equivalent(compare, c.Value),
            ConditionOperator.Contains => Contains(candidate, c.Field, normalizedValue, classification),
            ConditionOperator.NotContains => !Contains(candidate, c.Field, normalizedValue, classification),
            ConditionOperator.StartsWith => compare is not null && Normalizer.Normalize(compare).Joined.StartsWith(normalizedValue.Joined, StringComparison.Ordinal),
            ConditionOperator.EndsWith => compare is not null && Normalizer.Normalize(compare).Joined.EndsWith(normalizedValue.Joined, StringComparison.Ordinal),
            ConditionOperator.Regex => compare is not null && Regex.IsMatch(compare, c.Value, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)),
            ConditionOperator.GreaterThan => compare is not null && CompareNumeric(candidate, c, classification) > 0,
            ConditionOperator.LessThan => compare is not null && CompareNumeric(candidate, c, classification) < 0,
            ConditionOperator.In => compare is not null && c.Value.Split(',', StringSplitOptions.TrimEntries).Any(v => Normalizer.Equivalent(compare, v)),
            _ => false
        };

        string? comparison(ConditionField field, FileCandidate f) => field switch
        {
            ConditionField.Filename => f.DisplayName,
            ConditionField.Extension => f.Extension,
            ConditionField.MimeType => f.MimeType,
            ConditionField.SourceFolder => f.SourceUri,
            ConditionField.SourceScope => f.SourceScope.ToString(),
            ConditionField.Size => f.SizeBytes.ToString(),
            ConditionField.Subject => classification.Subject,
            ConditionField.Project => classification.Project,
            ConditionField.Collection => classification.Collection,
            ConditionField.Category => classification.Category,
            ConditionField.Metadata => c.MetadataKey is not null && f.Metadata.TryGetValue(c.MetadataKey, out var v) ? v : null,
            _ => null
        };
    }

    private static bool Contains(FileCandidate f, ConditionField field, NormalizedTokens needle, ClassificationResult classification)
    {
        var hay = field switch
        {
            ConditionField.Filename => Normalizer.Normalize(f.DisplayName).Joined,
            ConditionField.Keyword => Normalizer.Normalize(f.DisplayName).Joined,
            ConditionField.Extension => Normalizer.Normalize(f.Extension).Joined,
            ConditionField.MimeType => Normalizer.Normalize(f.MimeType ?? "").Joined,
            ConditionField.SourceFolder => Normalizer.Normalize(f.SourceUri).Joined,
            ConditionField.ExtractedContent => Normalizer.Normalize(f.ExtractedTextRef ?? "").Joined,
            ConditionField.Metadata => null,
            ConditionField.Subject => classification.Subject,
            ConditionField.Project => classification.Project,
            ConditionField.Collection => classification.Collection,
            ConditionField.Category => classification.Category,
            _ => null
        };

        if (field == ConditionField.Metadata)
            return false;

        if (needle.Joined.Length == 0) return false;
        if (field == ConditionField.Keyword)
        {
            var compact = string.Concat(Normalizer.Normalize(f.DisplayName).Tokens);
            return compact.Contains(needle.Joined.Replace(" ", ""), StringComparison.Ordinal) ||
                   hay!.Contains(needle.Joined, StringComparison.Ordinal);
        }
        return hay is not null && hay.Contains(needle.Joined, StringComparison.Ordinal);
    }

    private static int CompareNumeric(FileCandidate f, RuleCondition c, ClassificationResult classification)
    {
        var actual = c.Field switch
        {
            ConditionField.Size => (double)f.SizeBytes,
            ConditionField.CreatedAt => (double)f.CreatedAt.UtcTicks,
            ConditionField.ModifiedAt => (double)f.ModifiedAt.UtcTicks,
            _ => double.NaN
        };
        if (double.IsNaN(actual) || !double.TryParse(c.Value, out var expected)) return 0;
        return actual.CompareTo(expected);
    }
}
