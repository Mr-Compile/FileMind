using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Testing;
using Xunit;

namespace FileMind.Domain.Tests;

public class PlannerTests
{
    private static readonly PlanContext Ctx = new(new WindowsStylePathNormalizer(),
        Now: DateTimeOffset.Parse("2026-03-10T12:00:00Z"));

    private static ClassificationResult Classified(string? subject = null, string? project = null, bool review = false) => new()
    {
        FileKind = FileKind.Document,
        Subject = subject,
        Project = project,
        Collection = subject is not null ? "School" : project is not null ? "Work" : null,
        Category = "General",
        Evidence = [],
        Confidence = 80,
        Sources = [ClassifierSource.Local],
        Explanation = "test",
        NeedsReview = review
    };

    private static Decision Decide(FileCandidate c, ClassificationResult r, string? dest = null) => new()
    {
        Kind = DecisionKind.Organize,
        Candidate = c,
        Classification = r,
        DestinationTemplate = dest ?? "{Collection}/{Subject}/{YYYY}"
    };

    [Fact]
    public void Plan_ResolvesDestinationTemplate()
    {
        var c = Candidates.Create("dl/IT421_hw.pdf");
        var r = Classified(subject: "IT421");
        var plan = new ActionPlanner().Plan(Decide(c, r), Ctx).Value!;
        Assert.Equal("School\\IT421\\2026\\IT421_hw.pdf", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_AttachesUndoRecipe()
    {
        var c = Candidates.Create("dl/x.pdf");
        var plan = new ActionPlanner().Plan(Decide(c, Classified()), Ctx).Value!;
        var recipe = plan.Items[0].UndoRecipe;
        Assert.NotNull(recipe);
        Assert.Equal(OperationType.Move, recipe.ReverseOperation);
        Assert.Equal("dl/x.pdf", recipe.ReverseDestination);
    }

    [Fact]
    public void Plan_IgnoreYieldsEmpty()
    {
        var c = Candidates.Create("dl/x.pdf");
        var decision = Decide(c, Classified()) with { Kind = DecisionKind.Ignore };
        var plan = new ActionPlanner().Plan(decision, Ctx).Value!;
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void Plan_ReviewYieldsAsk()
    {
        var c = Candidates.Create("dl/x.pdf");
        var decision = Decide(c, Classified(review: true)) with { Kind = DecisionKind.Review };
        var plan = new ActionPlanner().Plan(decision, Ctx).Value!;
        Assert.Single(plan.Items);
        Assert.Equal(OperationType.Ask, plan.Items[0].Operation);
        Assert.True(plan.RequiresReview);
    }

    [Fact]
    public void Plan_NamingTemplateApplied()
    {
        var c = Candidates.Create("dl/IT421_Lecture_5.mp4");
        var r = Classified(subject: "IT421");
        var decision = Decide(c, r, "School/{Subject}") with { NamingTemplate = "{Subject}_{Name}.{Ext}" };
        var plan = new ActionPlanner().Plan(decision, Ctx).Value!;
        Assert.EndsWith("IT421_IT421_Lecture_5.mp4", plan.Items[0].Destination!.Replace('\\', '/'));
    }

    [Fact]
    public void Plan_UnsortedFallbacks()
    {
        var c = Candidates.Create("dl/random.xyz");
        var r = Classified();
        var plan = new ActionPlanner().Plan(Decide(c, r), Ctx).Value!;
        Assert.Contains("Unsorted", plan.Items[0].Destination);
    }

    [Theory]
    [InlineData("IT421_hw1.pdf", "Assignments")]
    [InlineData("IT421_assignment2.pdf", "Assignments")]
    [InlineData("IT421_lab3.pdf", "Labs")]
    [InlineData("IT421_quiz_1.docx", "Activities")]
    [InlineData("IT421_reviewer.pdf", "Notes")]
    [InlineData("IT421_capstone.docx", "Projects")]
    [InlineData("IT421_random.pdf", "Documents")] // no activity token → kind folder
    public void SubKind_BucketFromStemTokens(string fileName, string expected)
    {
        var c = Candidates.Create("dl/" + fileName);
        var r = Classified(subject: "IT421");
        var plan = new ActionPlanner().Plan(Decide(c, r, "School/{Subject}/{SubKind}"), Ctx).Value!;
        Assert.Equal($"School\\IT421\\{expected}\\{fileName}", plan.Items[0].Destination);
    }

    [Theory]
    [InlineData(FileKind.Document, "Documents")]
    [InlineData(FileKind.Spreadsheet, "Documents")]
    [InlineData(FileKind.Image, "Images")]
    [InlineData(FileKind.Video, "Videos")]
    [InlineData(FileKind.Audio, "Music")]
    [InlineData(FileKind.Archive, "Archives")]
    [InlineData(FileKind.Code, "Other")]
    public void KindFolder_PluralMap(FileKind kind, string expected)
    {
        var c = Candidates.Create("dl/x.bin");
        var r = Classified() with { FileKind = kind };
        var plan = new ActionPlanner().Plan(Decide(c, r, "{KindFolder}"), Ctx).Value!;
        Assert.Equal($"{expected}\\x.bin", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_AlreadyAtDestination_EmitsNoItem()
    {
        var c = Candidates.Create("dl/Documents/x.pdf");
        var r = Classified();
        var ctx = new PlanContext(new WindowsStylePathNormalizer(),
            DestinationBase: rel => Path.Combine("dl", rel),
            Now: DateTimeOffset.Parse("2026-03-10T12:00:00Z"));
        var plan = new ActionPlanner().Plan(Decide(c, r, "{KindFolder}"), ctx).Value!;
        Assert.Empty(plan.Items);
    }

    private static PlanContext TreeCtx(string? sourceRoot, bool preserve = true) =>
        new(new WindowsStylePathNormalizer(),
            Now: DateTimeOffset.Parse("2026-03-10T12:00:00Z"),
            SourceRoot: sourceRoot, PreserveTree: preserve);

    [Fact]
    public void Plan_PreserveTree_AppendsRelativeDirAfterTemplate()
    {
        var c = Candidates.Create(@"dl\receipts\jan\b.pdf");
        var r = Classified(subject: "IT421");
        var plan = new ActionPlanner().Plan(Decide(c, r), TreeCtx("dl")).Value!;
        Assert.Equal(@"School\IT421\2026\receipts\jan\b.pdf", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_NoPreserveTree_FlattensAsBefore()
    {
        var c = Candidates.Create(@"dl\receipts\jan\b.pdf");
        var r = Classified(subject: "IT421");
        var plan = new ActionPlanner().Plan(Decide(c, r), TreeCtx("dl", preserve: false)).Value!;
        Assert.Equal(@"School\IT421\2026\b.pdf", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_PreserveTree_RootFile_NoSubdir()
    {
        var c = Candidates.Create(@"dl\b.pdf");
        var r = Classified(subject: "IT421");
        var plan = new ActionPlanner().Plan(Decide(c, r), TreeCtx("dl")).Value!;
        Assert.Equal(@"School\IT421\2026\b.pdf", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_RelativeDirToken_WorksInDestinationTemplate()
    {
        var c = Candidates.Create(@"dl\receipts\jan\b.pdf");
        var r = Classified(project: "alpha");
        var decision = Decide(c, r, "Work/{Project}/{RelativeDir}");
        var plan = new ActionPlanner().Plan(decision, TreeCtx("dl", preserve: false)).Value!;
        Assert.Equal(@"Work\alpha\receipts\jan\b.pdf", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_RelativeDirToken_InNamingTemplate_ResolvesEmpty()
    {
        var c = Candidates.Create(@"dl\receipts\jan\b.pdf");
        var r = Classified(subject: "IT421");
        var decision = Decide(c, r, "School/{Subject}") with { NamingTemplate = "{RelativeDir}{Name}.{Ext}" };
        var plan = new ActionPlanner().Plan(decision, TreeCtx("dl")).Value!;
        Assert.Equal(@"School\IT421\receipts\jan\b.pdf", plan.Items[0].Destination);
    }

    [Fact]
    public void Plan_SourceNotUnderRoot_TreatedAsEmpty()
    {
        var c = Candidates.Create(@"dl\receipts\jan\b.pdf");
        var r = Classified(subject: "IT421");
        var plan = new ActionPlanner().Plan(Decide(c, r), TreeCtx(@"other\root")).Value!;
        Assert.Equal(@"School\IT421\2026\b.pdf", plan.Items[0].Destination);
    }
}
