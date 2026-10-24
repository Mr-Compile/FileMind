using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Rules;
using FileMind.Testing;
using Xunit;

namespace FileMind.Domain.Tests;

public class RuleTests
{
    private static ClassificationResult Classified(string? subject = null, string? project = null) => new()
    {
        FileKind = FileKind.Document,
        Subject = subject,
        Project = project,
        Evidence = [],
        Confidence = 80,
        Sources = [ClassifierSource.Local],
        Explanation = "test"
    };

    private static Rule Rule(string id, int priority, ConditionGroup conditions, params RuleAction[] actions) => new()
    {
        Id = id,
        Name = $"Rule {id}",
        Priority = priority,
        ConditionRoot = conditions,
        Actions = actions,
        SchemaVersion = 1
    };

    private static ConditionGroup Where(params RuleCondition[] conditions) =>
        new() { Operator = LogicalOperator.And, Conditions = conditions };

    private static RuleCondition Cond(ConditionField field, ConditionOperator op, string value, string? key = null) =>
        new() { Field = field, Operator = op, Value = value, MetadataKey = key };

    [Fact]
    public void And_AllMustMatch()
    {
        var g = Where(
            Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf"),
            Cond(ConditionField.Filename, ConditionOperator.Contains, "invoice"));
        var candidate = Candidates.Create("dl/invoice_q3.pdf");
        Assert.True(ConditionEvaluator.Evaluate(g, candidate, Classified()));
        Assert.False(ConditionEvaluator.Evaluate(g, Candidates.Create("dl/notes.pdf"), Classified()));
    }

    [Fact]
    public void Or_AnyMatches()
    {
        var g = new ConditionGroup
        {
            Operator = LogicalOperator.Or,
            Conditions = [Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf"),
                          Cond(ConditionField.Extension, ConditionOperator.Equals, "docx")]
        };
        Assert.True(ConditionEvaluator.Evaluate(g, Candidates.Create("a/b.docx"), Classified()));
        Assert.False(ConditionEvaluator.Evaluate(g, Candidates.Create("a/b.txt"), Classified()));
    }

    [Fact]
    public void SubjectCondition_UsesClassification()
    {
        var g = Where(Cond(ConditionField.Subject, ConditionOperator.Equals, "IT421"));
        Assert.True(ConditionEvaluator.Evaluate(g, Candidates.Create("x/IT421_lab.txt"), Classified(subject: "IT421")));
        Assert.False(ConditionEvaluator.Evaluate(g, Candidates.Create("x/other.txt"), Classified()));
    }

    [Fact]
    public void Validator_RejectsMalformed()
    {
        var bad = Rule("r1", 0, Where(), new RuleAction { Kind = RuleActionKind.Move });
        var result = RuleValidator.Validate(bad);
        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.ErrorCode.ValidationFailed, result.Error.Code);
    }

    [Fact]
    public void Validator_RejectsNestingOverLimit()
    {
        var inner4 = new ConditionGroup { Operator = LogicalOperator.And, Conditions = [Cond(ConditionField.Filename, ConditionOperator.Contains, "x")] };
        var inner3 = new ConditionGroup { Operator = LogicalOperator.And, Groups = [inner4] };
        var inner2 = new ConditionGroup { Operator = LogicalOperator.And, Groups = [inner3] };
        var root = new ConditionGroup { Operator = LogicalOperator.And, Groups = [inner2] };
        var deep = Rule("deep", 0, root, new RuleAction { Kind = RuleActionKind.Tag, Tag = "x" });
        Assert.Equal(4, root.Depth);
        Assert.False(RuleValidator.Validate(deep).IsSuccess);
    }

    [Fact]
    public void Validator_RejectsNewerSchemaVersion()
    {
        var future = Rule("f1", 0, Where(Cond(ConditionField.Filename, ConditionOperator.Contains, "x")),
            new RuleAction { Kind = RuleActionKind.Ignore });
        future = future with { SchemaVersion = 99 };
        var result = RuleValidator.Validate(future);
        Assert.False(result.IsSuccess);
        Assert.Equal(Errors.ErrorCode.UnsupportedOperation, result.Error.Code);
    }

    [Fact]
    public void Engine_PriorityThenSpecificityOrder()
    {
        var low = Rule("low", 1, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Tag, Tag = "low" });
        var high = Rule("high", 10, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Tag, Tag = "high" });
        var eval = new RuleEngine().Evaluate(Candidates.Create("a/x.pdf"), Classified(), [low, high]);
        Assert.Equal("high", eval.MatchedRuleIds[0]);
        Assert.Contains(eval.Trace, t => t.StartsWith("MATCH high"));
    }

    [Fact]
    public void Engine_StopProcessingHalts()
    {
        var stopper = Rule("stop", 10, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Ignore }) with { StopProcessing = true };
        var other = Rule("other", 1, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Tag, Tag = "x" });
        var eval = new RuleEngine().Evaluate(Candidates.Create("a/x.pdf"), Classified(), [stopper, other]);
        Assert.Single(eval.MatchedRuleIds);
        Assert.DoesNotContain("other", eval.MatchedRuleIds);
    }

    [Fact]
    public void Engine_ConflictingDestinations_NeedsReview()
    {
        var r1 = Rule("a", 10, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Move, DestinationTemplate = "School/{Subject}" });
        var r2 = Rule("b", 5, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Move, DestinationTemplate = "Work/{Project}" });
        var eval = new RuleEngine().Evaluate(Candidates.Create("a/x.pdf"), Classified(), [r1, r2]);
        Assert.True(eval.NeedsReview);
        Assert.NotEmpty(eval.Conflicts);
    }

    [Fact]
    public void Engine_Deterministic()
    {
        var rules = new[]
        {
            Rule("z", 1, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")), new RuleAction { Kind = RuleActionKind.Tag, Tag = "z" }),
            Rule("a", 1, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")), new RuleAction { Kind = RuleActionKind.Tag, Tag = "a" })
        };
        var engine = new RuleEngine();
        var e1 = engine.Evaluate(Candidates.Create("a/x.pdf"), Classified(), rules);
        var e2 = engine.Evaluate(Candidates.Create("a/x.pdf"), Classified(), rules.Reverse().ToArray());
        Assert.Equal(e1.MatchedRuleIds, e2.MatchedRuleIds);
        Assert.Equal(e1.Trace, e2.Trace);
    }

    [Fact]
    public void Engine_DisabledRulesSkipped()
    {
        var off = Rule("off", 10, Where(Cond(ConditionField.Extension, ConditionOperator.Equals, "pdf")),
            new RuleAction { Kind = RuleActionKind.Tag, Tag = "x" }) with { Enabled = false };
        var eval = new RuleEngine().Evaluate(Candidates.Create("a/x.pdf"), Classified(), [off]);
        Assert.Empty(eval.MatchedRuleIds);
    }
}
