using System.IO.Compression;
using System.Text;
using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Scanning;
using FileMind.Application.UseCases;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Domain.Ports;
using FileMind.Domain.Rules;
using FileMind.Domain.Safety;
using FileMind.Infrastructure.Sqlite;
using FileMind.Intelligence.Local.Analysis;
using FileMind.Intelligence.Local.Analyzers;
using FileMind.Platform.Windows;
using Xunit;

namespace FileMind.Application.Tests;

/// <summary>
/// PLAN-007 local intelligence: real analyzers over hand-crafted fixture bytes,
/// fail-safe corruption handling, enrichment -> classification -> rename e2e.
/// </summary>
public sealed class LocalIntelligenceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-intel-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly WindowsFileSystemAdapter _adapter = new();
    private readonly ExtractionBudget _budget = new();

    public LocalIntelligenceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static FileCandidate Cand(string path) => new()
    {
        ScanId = "t", SourceUri = path, DisplayName = Path.GetFileName(path),
        Extension = Path.GetExtension(path).TrimStart('.'), SizeBytes = new FileInfo(path).Length,
        CreatedAt = DateTimeOffset.UtcNow, ModifiedAt = DateTimeOffset.UtcNow,
        SourceScope = SourceScope.WatchedFolder,
    };

    private string Write(string name, byte[] bytes)
    {
        var p = Path.Combine(_root, name);
        File.WriteAllBytes(p, bytes);
        return p;
    }
    private string Write(string name, string text) => Write(name, Encoding.UTF8.GetBytes(text));

    // ---------- fixtures ----------

    private byte[] MakeDocx(string title, string creator, string bodyText)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var core = zip.CreateEntry("docProps/core.xml");
            using (var w = new StreamWriter(core.Open()))
                w.Write($"<cp:coreProperties xmlns:dc=\"x\" xmlns:cp=\"y\" xmlns:dcterms=\"z\">" +
                    $"<dc:title>{title}</dc:title><dc:creator>{creator}</dc:creator>" +
                    $"<dcterms:created>2021-03-04T00:00:00Z</dcterms:created></cp:coreProperties>");
            var doc = zip.CreateEntry("word/document.xml");
            using (var w = new StreamWriter(doc.Open()))
                w.Write($"<w:document><w:body><w:p><w:r><w:t>{bodyText}</w:t></w:r></w:p></w:body></w:document>");
        }
        return ms.ToArray();
    }

    private byte[] MakePdf(string title, string streamText)
    {
        var inner = $"BT /F1 12 Tf 72 720 Td ({streamText}) Tj ET";
        var pdf = "%PDF-1.4\n" +
            $"1 0 obj << /Title ({title}) /Author (Unit Test) >> endobj\n" +
            $"2 0 obj << /Length {inner.Length} >> stream\n{inner}\nendstream endobj\n" +
            "trailer << /Root 1 0 R >>\n%%EOF";
        return Encoding.Latin1.GetBytes(pdf);
    }

    private byte[] MakeDeflatedPdf(string streamText)
    {
        var inner = $"BT /F1 12 Tf 72 720 Td ({streamText}) Tj ET";
        byte[] deflated;
        using (var ms = new MemoryStream())
        {
            using (var ds = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
                ds.Write(Encoding.Latin1.GetBytes(inner));
            deflated = ms.ToArray();
        }
        var pdf = "%PDF-1.4\n" +
            "1 0 obj << /Title (Deflated Doc) /Author (Zip Test) >> endobj\n" +
            $"2 0 obj << /Length {deflated.Length} /Filter /FlateDecode >> stream\n" +
            Encoding.Latin1.GetString(deflated) + "\nendstream endobj\n%%EOF";
        return Encoding.Latin1.GetBytes(pdf);
    }

    private byte[] MakePng(int w, int h)
    {
        var b = new byte[33];
        byte[] sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        sig.CopyTo(b, 0);
        b[8] = 0; b[9] = 0; b[10] = 0; b[11] = 13; // IHDR len
        b[12] = (byte)'I'; b[13] = (byte)'H'; b[14] = (byte)'D'; b[15] = (byte)'R';
        b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
        b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
        b[24] = 8; b[25] = 2;
        return b;
    }

    private byte[] MakeId3(string title, string artist)
    {
        static byte[] Frame(string id, string text)
        {
            var body = new byte[] { 3 }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
            var hdr = Encoding.ASCII.GetBytes(id)
                .Concat(new[] { (byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length })
                .Concat(new byte[] { 0, 0 }).ToArray();
            return hdr.Concat(body).ToArray();
        }
        var frames = Frame("TIT2", title).Concat(Frame("TPE1", artist)).ToArray();
        var tag = Encoding.ASCII.GetBytes("ID3")
            .Concat(new byte[] { 4, 0, 0 })
            .Concat(new[] { (byte)(frames.Length >> 21), (byte)(frames.Length >> 14), (byte)(frames.Length >> 7), (byte)(frames.Length & 0x7F) })
            .Concat(frames).ToArray();
        return tag;
    }

    private byte[] MakeZip(params string[] names)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var n in names) zip.CreateEntry(n);
        return ms.ToArray();
    }

    // ---------- tests ----------

    [Fact]
    public async Task PlainText_ExtractsTextAndCounts()
    {
        var p = Write("notes.txt", "hello world\nsecond line here");
        var o = await new PlainTextAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.True(o.Reliable);
        Assert.Contains("hello world", o.ExtractedText);
        Assert.Equal("2", o.Metadata["text.lines"]);
    }

    [Fact]
    public async Task Docx_TitleAuthorAndBodyText()
    {
        var p = Write("report.docx", MakeDocx("Quarterly Plan", "Alice", "alpha rollout network budget"));
        var o = await new OoxmlAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.True(o.Reliable);
        Assert.Equal("Quarterly Plan", o.Metadata["title"]);
        Assert.Equal("Alice", o.Metadata["author"]);
        Assert.Equal("2021", o.Metadata["year"]);
        Assert.Contains("alpha rollout", o.ExtractedText);
    }

    [Fact]
    public async Task Pdf_InfoAndPlainStream()
    {
        var p = Write("doc.pdf", MakePdf("Networks Guide", "it421 lecture networks"));
        var o = await new PdfAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.Equal("Networks Guide", o.Metadata["title"]);
        Assert.Equal("Unit Test", o.Metadata["author"]);
        Assert.Contains("it421 lecture networks", o.ExtractedText);
    }

    [Fact]
    public async Task Pdf_FlateDecode_InflatesText()
    {
        var p = Write("flat.pdf", MakeDeflatedPdf("compressed content it421"));
        var o = await new PdfAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.True(o.Reliable);
        Assert.Contains("compressed content it421", o.ExtractedText);
    }

    [Fact]
    public async Task Png_IhdrDimensions()
    {
        var p = Write("shot.png", MakePng(1920, 1080));
        var o = await new ImageMetadataAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.True(o.Reliable);
        Assert.Equal("1920", o.Metadata["image.width"]);
        Assert.Equal("1080", o.Metadata["image.height"]);
    }

    [Fact]
    public async Task Mp3_Id3v2_TitleArtist()
    {
        var p = Write("song.mp3", MakeId3("Lecture Intro", "Prof X"));
        var o = await new AudioTagAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.Equal("Lecture Intro", o.Metadata["title"]);
        Assert.Equal("Prof X", o.Metadata["author"]);
    }

    [Fact]
    public async Task Zip_ManifestListedNeverExtracted()
    {
        var p = Write("pack.zip", MakeZip("a/b.txt", "c.png", "docs/"));
        var o = await new ArchiveManifestAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.Equal("3", o.Metadata["archive.entries"]);
        Assert.Contains("a/b.txt", o.ExtractedText);
        Assert.False(Directory.Exists(Path.Combine(_root, "a")), "archive must not be extracted to disk");
    }

    [Fact]
    public async Task Code_PythonDetected()
    {
        var p = Write("script.py", "#!/usr/bin/env python\nimport os\ndef main():\n    pass\n");
        var o = await new CodeSignalAnalyzer().AnalyzeAsync(Cand(p), _adapter, _budget);
        Assert.Equal("python", o.Metadata["code.language"]);
        Assert.Contains("import os", o.ExtractedText);
    }

    [Fact]
    public async Task Corrupt_Files_FailSafe()
    {
        var garbage = Encoding.UTF8.GetBytes("definitely not a real format " + new string('x', 400));
        foreach (var (file, analyzer) in new (string, IFileAnalyzer)[]
        {
            ("bad.pdf", new PdfAnalyzer()),
            ("bad.docx", new OoxmlAnalyzer()),
            ("bad.zip", new ArchiveManifestAnalyzer()),
            ("bad.png", new ImageMetadataAnalyzer()),
            ("bad.mp3", new AudioTagAnalyzer()),
            ("bad.mp4", new VideoMetadataAnalyzer()),
        })
        {
            var p = Write(file, garbage);
            var o = await analyzer.AnalyzeAsync(Cand(p), _adapter, _budget);
            Assert.False(o.Reliable, $"{file} should be unreliable");
            Assert.NotEmpty(o.Warnings);
        }
    }

    [Fact]
    public async Task Budget_SmallMaxChars_Truncates()
    {
        var p = Write("big.txt", new string('a', 20000));
        var o = await new PlainTextAnalyzer().AnalyzeAsync(Cand(p), _adapter,
            _budget with { MaxChars = 500 });
        Assert.Equal(500, o.ExtractedText!.Length);
    }

    [Fact]
    public async Task Registry_ResolvesByExtension()
    {
        var reg = new AnalyzerRegistry();
        var p = Write("a.docx", MakeDocx("t", "a", "x"));
        var analyzers = reg.For(Cand(p));
        Assert.Contains(analyzers, a => a is OoxmlAnalyzer);
        Assert.DoesNotContain(analyzers, a => a is PdfAnalyzer);
    }

    [Fact]
    public async Task Service_MergesMetadataTextEvidence_Capped()
    {
        var p = Write("song.mp3", MakeId3("T", "A"));
        var svc = new LocalAnalyzerService();
        var enr = await svc.EnrichAsync(Cand(p), _adapter);
        Assert.True(enr.IsSuccess);
        Assert.Equal("T", enr.Value!.Metadata["title"]);
        Assert.True(enr.Value.Evidence.Sum(e => e.ScoreContribution) <= LocalAnalyzerService.EvidenceCap + 5);
    }

    // ---------- pipeline integration ----------

    private OrganizerPipeline BuildPipeline(SqliteStore store, WindowsPathNormalizer paths,
        FileIndexingService indexing, ICandidateEnricher enricher)
    {
        var confidence = new ConfidenceEngine();
        var safety = new SafetyEngine(_adapter, paths);
        ((ISubjectRepository)store).UpsertAsync(new SubjectLike(
            "IT421", "IT421 Networks", "IT421",
            ["IT 421", "IT-421", "IT_421"], ["networks"])).GetAwaiter().GetResult();
        ((ICollectionRepository)store).UpsertAsync(
            new CollectionEntry("school", "School", "School/{Subject}", ["IT421"])).GetAwaiter().GetResult();
        var classifier = new ClassifierEngine(
            new SubjectMatcher(new InMemorySubjectCatalog(
            [
                new SubjectEntry("IT421", "IT421 Networks", "IT421",
                    ["IT 421", "IT-421", "IT_421"], ["networks"])
            ])),
            new ProjectMatcher(new InMemoryProjectCatalog([])),
            confidence);
        var scanner = new FileScanner(_adapter, paths, new ExclusionMatcher(["*.tmp"]));
        return new OrganizerPipeline(
            scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
            new ActionPlanner(), safety,
            new TransactionEngine(_adapter, store, safety),
            new UndoService(_adapter),
            _adapter, paths, _adapter.ProtectedPaths,
            (IRuleRepository)store, (ICollectionRepository)store,
            (IOperationRepository)store, store, indexing, enricher);
    }

    [Fact]
    public async Task Pipeline_ContentMatch_ClassifiesSubject()
    {
        // filename carries no subject; content does -> IT421 via content match
        var dir = Path.Combine(_root, "scan1");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mystery.txt"),
            "lecture notes about networks and IT421 course material");

        using var db = FileMindDb.Open(Path.Combine(_root, "intel.db"));
        var store = new SqliteStore(db);
        var paths = new WindowsPathNormalizer();
        var indexing = new FileIndexingService(store, new WindowsHashService());
        var pipe = BuildPipeline(store, paths, indexing, new LocalAnalyzerService());

        var cands = new List<FileCandidate>();
        await foreach (var c in pipe.ScanAsync(dir)) cands.Add(c);
        var analyzed = await pipe.AnalyzeAsync(cands);
        Assert.True(analyzed.IsSuccess);
        var a = Assert.Single(analyzed.Value!);
        Assert.Equal("IT421", a.Classification.Subject);
        Assert.NotNull(a.Candidate.ExtractedTextRef);
        Assert.Contains(a.Classification.Evidence,
            e => e.Kind.StartsWith("content-") || e.Kind == "extracted-text");
    }

    [Fact]
    public async Task Pipeline_NoEnricher_StillWorks()
    {
        var dir = Path.Combine(_root, "scan2");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "IT421_notes.txt"), "x");
        using var db = FileMindDb.Open(Path.Combine(_root, "intel2.db"));
        var store = new SqliteStore(db);
        var pipe = BuildPipeline(store, new WindowsPathNormalizer(),
            new FileIndexingService(store, new WindowsHashService()), enricher: null!);
        var cands = new List<FileCandidate>();
        await foreach (var c in pipe.ScanAsync(dir)) cands.Add(c);
        var analyzed = await pipe.AnalyzeAsync(cands);
        Assert.True(analyzed.IsSuccess);
        Assert.Equal("IT421", analyzed.Value![0].Classification.Subject);
    }

    [Fact]
    public async Task SmartRename_TitleToken_UsesExtractedMetadata()
    {
        // rule: move .docx -> School/{Subject} with naming template {Title}
        var dir = Path.Combine(_root, "scan3");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "blob.docx"),
            MakeDocx("IT421 Final Guide", "Prof", "networks exam prep"));

        using var db = FileMindDb.Open(Path.Combine(_root, "intel3.db"));
        var store = new SqliteStore(db);
        var paths = new WindowsPathNormalizer();
        var indexing = new FileIndexingService(store, new WindowsHashService());
        var pipe = BuildPipeline(store, paths, indexing, new LocalAnalyzerService());

        var upsert = await ((IRuleRepository)store).UpsertAsync(new Rule
        {
            Id = "rename-docx", Name = "Rename docx by title", Enabled = true, Priority = 50,
            SchemaVersion = 1,
            ConditionRoot = new ConditionGroup
            {
                Operator = LogicalOperator.And,
                Conditions = [new RuleCondition
                {
                    Field = ConditionField.Extension, Operator = ConditionOperator.Equals, Value = "docx"
                }]
            },
            Actions = [new RuleAction
            {
                Kind = RuleActionKind.Move,
                DestinationTemplate = "Renamed",
                NamingTemplate = "{Title}.docx",
            }]
        });
        Assert.True(upsert.IsSuccess);

        var cands = new List<FileCandidate>();
        await foreach (var c in pipe.ScanAsync(dir)) cands.Add(c);
        var analyzed = await pipe.AnalyzeAsync(cands);
        var dest = Path.Combine(_root, "out");
        var plan = pipe.Plan(analyzed.Value!, dryRun: false, destinationRoot: dest);
        Assert.True(plan.IsSuccess);
        var item = Assert.Single(plan.Value!.Items);
        Assert.EndsWith("IT421 Final Guide.docx", item.Destination);
    }
}
