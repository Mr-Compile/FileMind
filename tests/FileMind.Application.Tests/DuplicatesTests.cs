using FileMind.Application.Duplicates;
using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Domain.Safety;
using FileMind.Testing;

namespace FileMind.Application.Tests;

public class DuplicatesTests
{
    private static FileCandidate Indexed(string path, long size, string ext = "bin",
        DateTimeOffset? modified = null) =>
        Candidates.Create(path, extension: ext, size: size) with
        {
            ModifiedAt = modified ?? DateTimeOffset.UtcNow.AddDays(-10)
        };

    private static async Task IndexAsync(InMemoryFileIndex index, params FileCandidate[] files)
    {
        foreach (var f in files)
            await index.UpsertAsync(f, new FileIdentity
            {
                StableId = $"id-{f.SourceUri.GetHashCode():x}",
                PathHint = f.SourceUri,
                Fingerprint = FileIdentity.ComputeFingerprint(f.SizeBytes, f.ModifiedAt)
            });
    }

    [Fact]
    public async Task ExactDuplicates_GroupedViaSizePartialFull()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/a1.bin", "identical-payload");
        fs.SeedFile("d:/f/a2.bin", "identical-payload");
        fs.SeedFile("d:/f/diff.bin", "different-bytes!");   // same length, different content
        fs.SeedFile("d:/f/uniq.bin", "x");                  // different size entirely

        var index = new InMemoryFileIndex();
        await IndexAsync(index,
            Indexed("d:/f/a1.bin", 17), Indexed("d:/f/a2.bin", 17),
            Indexed("d:/f/diff.bin", 17), Indexed("d:/f/uniq.bin", 1));

        var svc = new DuplicateDetectionService(index, new StreamHashService(fs), fs);
        var report = (await svc.FindAsync()).Value!;

        var group = Assert.Single(report.Groups);
        Assert.True(group.Confirmed);
        Assert.Equal(17, group.ReclaimableBytes);
        Assert.Equal(2, group.Files.Count);
        Assert.Contains("a1.bin", group.Files[0].Path);
        Assert.DoesNotContain(report.Warnings, w => w.Contains("uniq"));
    }

    [Fact]
    public async Task UniqueFiles_NotGrouped()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/a.bin", "aaaa");
        fs.SeedFile("d:/f/b.bin", "bbbbbb");

        var index = new InMemoryFileIndex();
        await IndexAsync(index, Indexed("d:/f/a.bin", 4), Indexed("d:/f/b.bin", 6));

        var svc = new DuplicateDetectionService(index, new StreamHashService(fs), fs);
        var report = (await svc.FindAsync()).Value!;

        Assert.Empty(report.Groups);
        Assert.Equal(0, report.TotalReclaimable);
    }

    [Fact]
    public async Task OverCap_StaysPartialOnlyUnconfirmed()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/big1.bin", new string('z', 200));
        fs.SeedFile("d:/f/big2.bin", new string('z', 200));

        var index = new InMemoryFileIndex();
        await IndexAsync(index, Indexed("d:/f/big1.bin", 200), Indexed("d:/f/big2.bin", 200));

        var svc = new DuplicateDetectionService(index, new StreamHashService(fs), fs,
            maxFullHashBytes: 100); // cap below file size
        var report = (await svc.FindAsync()).Value!;

        var group = Assert.Single(report.Groups);
        Assert.False(group.Confirmed);
        Assert.Contains(report.Warnings, w => w.Contains("cap"));
    }

    [Fact]
    public async Task ChangedSinceIndexed_RowInvalidatedAndSkipped()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/same1.bin", "payload-x");
        fs.SeedFile("d:/f/same2.bin", "payload-x-changed"); // real size != indexed size

        var index = new InMemoryFileIndex();
        // index says both are 9 bytes; the second file really has 18
        await IndexAsync(index, Indexed("d:/f/same1.bin", 9), Indexed("d:/f/same2.bin", 9));

        var svc = new DuplicateDetectionService(index, new StreamHashService(fs), fs);
        var report = (await svc.FindAsync()).Value!;

        Assert.Empty(report.Groups);
        Assert.False(index.Contains("d:/f/same2.bin"));
        Assert.Contains(report.Warnings, w => w.Contains("changed") || w.Contains("skipped"));
    }

    [Fact]
    public async Task Cancellation_Throws()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/a.bin", "zz");
        fs.SeedFile("d:/f/b.bin", "zz");

        var index = new InMemoryFileIndex();
        await IndexAsync(index, Indexed("d:/f/a.bin", 2), Indexed("d:/f/b.bin", 2));

        var svc = new DuplicateDetectionService(index, new StreamHashService(fs), fs);
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.FindAsync(ct: cts.Token));
    }

    [Fact]
    public async Task SimilarVersionedNames_FlaggedWithEvidence()
    {
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/quarterly report 2024.docx", "version-a-payload");
        fs.SeedFile("d:/f/quarterly report 2025.docx", "version-b-payload");

        var index = new InMemoryFileIndex();
        await IndexAsync(index,
            Indexed("d:/f/quarterly report 2024.docx", 17, "docx"),
            Indexed("d:/f/quarterly report 2025.docx", 17, "docx"));

        var svc = new DuplicateDetectionService(index, new StreamHashService(fs), fs);
        var report = (await svc.FindAsync()).Value!;

        var sim = Assert.Single(report.Similar);
        Assert.True(sim.Score >= 50);
        Assert.Contains(sim.Reasons, r => r.Contains("name overlap"));
        Assert.Contains(sim.Reasons, r => r.Contains("same type"));
    }

    [Fact]
    public async Task StorageReport_BreakdownAndReclaimable()
    {
        var index = new InMemoryFileIndex();
        await IndexAsync(index,
            Indexed("d:/f/a.txt", 100, "txt") with
                { ContentHash = "sha256:aaa", HashState = HashState.Full },
            Indexed("d:/f/b.txt", 100, "txt") with
                { ContentHash = "sha256:aaa", HashState = HashState.Full },
            Indexed("d:/f/c.mp4", 5000, "mp4"),
            Indexed("d:/f/old.txt", 50, "txt",
                modified: DateTimeOffset.UtcNow.AddDays(-400)));

        var svc = new StorageAnalysisService(index);
        var report = (await svc.AnalyzeAsync()).Value!;

        Assert.Equal(4, report.FileCount);
        Assert.Equal(5250, report.TotalBytes);
        Assert.Equal(100, report.DuplicateReclaimableBytes);
        Assert.Equal("Video", report.ByKind[0].Label);
        Assert.Contains(report.ByAgeBand, b => b.Label == ">365d" && b.Bytes == 50);
        Assert.Equal(5000, report.Largest[0].SizeBytes);
        Assert.True(report.Oldest[0].AgeDays >= 400);
    }

    [Fact]
    public void QuarantinePlan_KeepBothMovesOnly_RequiresReview()
    {
        var m = DateTimeOffset.UtcNow;
        var report = new DuplicatesReport(
            [new DuplicateGroup("sha256:h", 100, 100,
                [new DuplicateFile("d:/f/keep.bin", 100, m),
                 new DuplicateFile("d:/f/copy1.bin", 100, m),
                 new DuplicateFile("d:/f/copy2.bin", 100, m)], Confirmed: true),
             new DuplicateGroup("partial:500", 500, 500,
                [new DuplicateFile("d:/f/p1.bin", 500, m),
                 new DuplicateFile("d:/f/p2.bin", 500, m)], Confirmed: false)],
            [], []);

        var plan = DuplicatePlanBuilder.BuildQuarantinePlan(report, "d:/review", apply: false);

        Assert.True(plan.RequiresReview);
        Assert.True(plan.DryRun);
        Assert.Equal(2, plan.Items.Count); // unconfirmed group excluded entirely
        Assert.All(plan.Items, i =>
        {
            Assert.Equal(OperationType.Move, i.Operation);
            Assert.Equal(ConflictPolicy.KeepBoth, i.ConflictPolicy);
            Assert.StartsWith("d:/review", i.Destination!);
        });
    }

    [Fact]
    public async Task QuarantinePlan_ExecuteThenUndo_RestoresFiles()
    {
        var m = DateTimeOffset.UtcNow;
        var fs = new FakeFileSystemAdapter();
        fs.SeedFile("d:/f/keep.bin", "same-content", modifiedAt: m);
        fs.SeedFile("d:/f/copy.bin", "same-content", modifiedAt: m);

        var report = new DuplicatesReport(
            [new DuplicateGroup("sha256:h", 12, 12,
                [new DuplicateFile("d:/f/keep.bin", 12, m),
                 new DuplicateFile("d:/f/copy.bin", 12, m)], true)],
            [], []);
        var plan = DuplicatePlanBuilder.BuildQuarantinePlan(report, "d:/review", apply: true);

        var paths = new WindowsStylePathNormalizer();
        var safety = new SafetyEngine(fs, paths);
        var store = new InMemoryOperationStore();
        var tx = new TransactionEngine(fs, store, safety);

        var ctx = new SafetyContext(new StaticProtectedPaths("d:/protected"));
        var safetyResult = await safety.ValidateAsync(plan, ctx);
        Assert.True(safetyResult.Allowed, string.Join(";", safetyResult.Blockers));

        var record = (await tx.ExecuteAsync(plan, ctx)).Value!;
        Assert.Equal(OperationStatus.Committed, record.Status);
        Assert.Null(fs.ReadText("d:/f/copy.bin"));
        Assert.Equal("same-content", fs.ReadText(record.Items[0].ResultingLocation!));

        var reverse = (await new UndoService(fs).BuildReversePlanAsync(record)).Value!;
        var undoRecord = (await tx.ExecuteAsync(reverse, ctx)).Value!;
        Assert.Equal(OperationStatus.Committed, undoRecord.Status);
        Assert.Equal("same-content", fs.ReadText("d:/f/copy.bin"));
    }
}
