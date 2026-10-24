using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// DOCX/PPTX/XLSX: Open Packaging Convention zips. Reads core properties +
/// bounded document text; never extracts entries to disk.
/// </summary>
public sealed partial class OoxmlAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "docx", "pptx", "xlsx", "docm", "pptm", "xlsm" };

    public string Name => "ooxml";

    public bool CanAnalyze(FileCandidate c) => Extensions.Contains(c.Extension);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        ZipArchive zip;
        try { zip = new ZipArchive(stream, ZipArchiveMode.Read); }
        catch (InvalidDataException)
        {
            return new AnalyzerOutput { Reliable = false, Warnings = ["not a readable OOXML package (corrupt or not a zip)"] };
        }
        using (zip)
        {
            var meta = new Dictionary<string, string>();
            var warnings = new List<string>();
            var sb = new StringBuilder();

            // core properties
            var core = zip.GetEntry("docProps/core.xml");
            if (core is not null)
            {
                var xml = ReadEntry(core, 64 * 1024);
                SetIf(meta, "title", Tag(xml, "dc:title"));
                SetIf(meta, "author", Tag(xml, "dc:creator"));
                SetIf(meta, "subject-meta", Tag(xml, "dc:subject"));
                SetIf(meta, "keywords", Tag(xml, "cp:keywords"));
                var created = Tag(xml, "dcterms:created");
                if (created is { Length: >= 4 }) meta["year"] = created[..4];
            }

            var ext = candidate.Extension.TrimStart('.').ToLowerInvariant();
            IEnumerable<ZipArchiveEntry> contentEntries = ext switch
            {
                "docx" or "docm" => zip.Entries.Where(e => e.FullName == "word/document.xml"),
                "pptx" or "pptm" => zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                                        .OrderBy(e => e.FullName).Take(50),
                "xlsx" or "xlsm" => zip.Entries.Where(e => e.FullName == "xl/sharedStrings.xml"),
                _ => []
            };

            foreach (var entry in contentEntries)
            {
                if (sb.Length >= budget.MaxChars) break;
                var xml = ReadEntry(entry, Math.Min(budget.MaxBytes, 4 * 1024 * 1024));
                foreach (Match m in TextRuns().Matches(xml))
                {
                    if (sb.Length >= budget.MaxChars) break;
                    var decoded = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value);
                    if (decoded.Length > 0) { sb.Append(decoded); sb.Append(' '); }
                }
            }

            var evidence = new List<Evidence>
            {
                new("ooxml-package", $"OOXML package ({ext})", 5, ClassifierSource.Local)
            };
            if (meta.ContainsKey("title"))
                evidence.Add(new Evidence("ooxml-title", "Document title metadata", 8, ClassifierSource.Local));

            return new AnalyzerOutput
            {
                ExtractedText = sb.Length > 0 ? sb.ToString().Trim() : null,
                Metadata = meta,
                Evidence = evidence,
                Warnings = warnings,
            };
        }
    }

    private static void SetIf(Dictionary<string, string> d, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) d[key] = value.Trim();
    }

    private static string? Tag(string xml, string tag)
    {
        var m = Regex.Match(xml, $"<{tag}[^>]*>(.*?)</{tag}>", RegexOptions.Singleline);
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
    }

    private static string ReadEntry(ZipArchiveEntry entry, long maxBytes)
    {
        using var s = entry.Open();
        using var ms = new MemoryStream();
        var buf = new byte[64 * 1024];
        long total = 0;
        int read;
        while (total < maxBytes && (read = s.Read(buf, 0, (int)Math.Min(buf.Length, maxBytes - total))) > 0)
        {
            ms.Write(buf, 0, read);
            total += read;
        }
        return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
    }

    [GeneratedRegex(@"<(?:w|a):t(?:\s[^>]*)?>([^<]*)</(?:w|a):t>")]
    private static partial Regex TextRuns();
}
