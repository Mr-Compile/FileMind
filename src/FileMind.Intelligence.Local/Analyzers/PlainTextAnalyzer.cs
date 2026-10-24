using System.Text;
using System.Text.RegularExpressions;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>TXT/MD/LOG/CSV/RTF: bounded decode; RTF strips control words.</summary>
public sealed class PlainTextAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "txt", "md", "log", "rtf", "csv", "tsv", "nfo" };

    public string Name => "plain-text";

    public bool CanAnalyze(FileCandidate c) => Extensions.Contains(c.Extension);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        var buf = new byte[Math.Min(budget.MaxBytes, 1 * 1024 * 1024)];
        var read = await stream.ReadAsync(buf, ct);
        var raw = Encoding.UTF8.GetString(buf, 0, read);

        var text = raw;
        if (candidate.Extension.Equals("rtf", StringComparison.OrdinalIgnoreCase))
            text = StripRtf(raw);

        text = new string(text.Where(ch => !char.IsControl(ch) || ch is '\n' or '\r' or '\t').ToArray());
        if (text.Length > budget.MaxChars) text = text[..budget.MaxChars];

        var lines = text.Count(ch => ch == '\n') + 1;
        var words = Regex.Matches(text, @"\w+").Count;
        return new AnalyzerOutput
        {
            ExtractedText = text.Length > 0 ? text : null,
            Metadata = new Dictionary<string, string>
            {
                ["text.lines"] = lines.ToString(),
                ["text.words"] = words.ToString(),
            },
            Evidence = [new Evidence("text-content",
                $"Plain-text content ({lines} lines)", 4, ClassifierSource.Local)],
        };
    }

    private static string StripRtf(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var i = 0;
        var depth = 0;
        while (i < raw.Length)
        {
            var ch = raw[i];
            if (ch == '{') { depth++; i++; }
            else if (ch == '}') { depth--; i++; }
            else if (ch == '\\' && i + 1 < raw.Length)
            {
                var j = i + 1;
                while (j < raw.Length && char.IsLetter(raw[j])) j++;
                var word = raw[(i + 1)..j];
                if (word is "par" or "line") sb.Append('\n');
                else if (word is "tab") sb.Append('\t');
                else if (word == "u" && j < raw.Length)
                {
                    var k = j;
                    while (k < raw.Length && (char.IsDigit(raw[k]) || raw[k] == '-')) k++;
                    if (int.TryParse(raw[j..k], out var cp)) sb.Append((char)cp);
                    j = k + 1; // skip the \uN fallback char
                }
                if (j < raw.Length && raw[j] == ' ') j++;
                i = j;
            }
            else { sb.Append(ch); i++; }
        }
        return sb.ToString();
    }
}
