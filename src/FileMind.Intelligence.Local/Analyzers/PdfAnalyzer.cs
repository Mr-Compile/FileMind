using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// Minimal bounded PDF reader: header check, /Info metadata literals, and
/// FlateDecode streams inflated in-memory to harvest Tj/TJ text operands.
/// Encrypted or malformed files degrade to warnings — never throw.
/// </summary>
public sealed partial class PdfAnalyzer : IFileAnalyzer
{
    public string Name => "pdf";

    public bool CanAnalyze(FileCandidate c) =>
        c.Extension.Equals("pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        var buf = new byte[Math.Min(candidate.SizeBytes, budget.MaxBytes)];
        var read = 0;
        while (read < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read), ct);
            if (n <= 0) break;
            read += n;
        }
        var content = Encoding.Latin1.GetString(buf, 0, read);
        var warnings = new List<string>();

        if (!content.StartsWith("%PDF", StringComparison.Ordinal))
            return new AnalyzerOutput { Reliable = false, Warnings = ["missing %PDF header — not a valid PDF"] };
        if (content.Contains("/Encrypt"))
            return new AnalyzerOutput { Reliable = false, Warnings = ["encrypted PDF — metadata/text unavailable without decryption"] };

        var meta = new Dictionary<string, string>();
        foreach (var (key, field) in new[] { ("title", "Title"), ("author", "Author"),
                     ("subject-meta", "Subject"), ("keywords", "Keywords") })
        {
            var m = Regex.Match(content, $"/{field}\\s*\\((?<v>(?:\\\\.|[^\\\\)])*)\\)");
            if (m.Success)
            {
                var v = UnescapeLiteral(m.Groups["v"].Value);
                if (v.Length > 0) meta[key] = v;
            }
        }
        var dateM = Regex.Match(content, "/CreationDate\\s*\\(D:(?<y>\\d{4})");
        if (dateM.Success) meta["year"] = dateM.Groups["y"].Value;

        // FlateDecode streams -> text operators
        var sb = new StringBuilder();
        foreach (Match sm in StreamRe().Matches(content))
        {
            if (sb.Length >= budget.MaxChars) break;
            var header = sm.Groups["h"].Value;
            var flate = header.Contains("FlateDecode");
            var offset = sm.Index + sm.Length;
            var end = content.IndexOf("endstream", offset, StringComparison.Ordinal);
            if (end < 0) break;
            var len = end - offset;
            if (len > 8 * 1024 * 1024) continue;
            var raw = new byte[len];
            Array.Copy(buf, offset, raw, 0, len);
            var ts = 0;
            while (ts < raw.Length && raw[ts] is (byte)' ' or (byte)'\r' or (byte)'\n') ts++;
            var trimmed = raw.AsSpan(ts);
            string? decoded;
            if (flate)
            {
                decoded = null;
                try
                {
                    using var ds = new DeflateStream(new MemoryStream(trimmed.ToArray()), CompressionMode.Decompress);
                    using var ms = new MemoryStream();
                    ds.CopyTo(ms, 64 * 1024);
                    decoded = Encoding.Latin1.GetString(ms.ToArray());
                }
                catch (InvalidDataException) { /* damaged stream */ }
                if (decoded is null) continue;
            }
            else
            {
                decoded = Encoding.Latin1.GetString(trimmed);
            }

            foreach (Match tm in TjStrings().Matches(decoded))
            {
                if (sb.Length >= budget.MaxChars) break;
                var val = tm.Groups["v"].Value;
                var inner = ParenLiteral().Matches(val);
                var pieces = inner.Count > 0
                    ? inner.Select(m => m.Groups[1].Value)
                    : [val];
                foreach (var p in pieces)
                {
                    var piece = UnescapeLiteral(p);
                    if (piece.Any(char.IsLetterOrDigit)) { sb.Append(piece); sb.Append(' '); }
                }
            }
        }

        var evidence = new List<Evidence> { new("pdf-container", "PDF document", 5, ClassifierSource.Local) };
        if (meta.ContainsKey("title")) evidence.Add(new Evidence("pdf-title", "PDF title metadata", 8, ClassifierSource.Local));
        if (sb.Length == 0 && !meta.Any()) warnings.Add("no extractable text/metadata within byte budget");

        return new AnalyzerOutput
        {
            ExtractedText = sb.Length > 0 ? sb.ToString().Trim() : null,
            Metadata = meta,
            Evidence = evidence,
            Warnings = warnings,
        };
    }

    private static string UnescapeLiteral(string s) =>
        s.Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\")
         .Replace("\\n", " ").Replace("\\r", " ").Replace("\\t", " ");

    // stream .. endstream blocks, capturing the preceding dict header
    [GeneratedRegex(@"(?<h><<[^>]{0,4000}>>)\s*stream\r?\n?")]
    private static partial Regex StreamRe();

    // (text) Tj and [(a) -250 (b)] TJ
    [GeneratedRegex(@"\((?<v>(?:\\\\.|[^\\\\)])*)\)\s*Tj|\[(?<v>[^\]]{0,4000})\]\s*TJ")]
    private static partial Regex TjStrings();

    [GeneratedRegex(@"\(((?:\\\\.|[^\\\\)])*)\)")]
    private static partial Regex ParenLiteral();
}
