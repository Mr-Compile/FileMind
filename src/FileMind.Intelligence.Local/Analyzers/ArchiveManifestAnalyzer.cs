using System.IO.Compression;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// Archive manifest metadata: entry names + counts. Archives are NEVER
/// extracted to disk — listing only. ZIP gets full manifest; RAR/7z get
/// signature verification only.
/// </summary>
public sealed class ArchiveManifestAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "zip", "rar", "7z", "tar", "gz" };

    private const int MaxEntries = 200;

    public string Name => "archive-manifest";

    public bool CanAnalyze(FileCandidate c) => Extensions.Contains(c.Extension);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        var ext = candidate.Extension.TrimStart('.').ToLowerInvariant();
        var meta = new Dictionary<string, string>();
        var warnings = new List<string>();

        if (ext == "zip")
        {
            ZipArchive zip;
            try { zip = new ZipArchive(stream, ZipArchiveMode.Read); }
            catch (InvalidDataException)
            {
                return new AnalyzerOutput { Reliable = false, Warnings = ["corrupt zip — central directory unreadable"] };
            }
            using (zip)
            {
                var names = zip.Entries.Take(MaxEntries).Select(e => e.FullName).ToList();
                meta["archive.entries"] = zip.Entries.Count.ToString();
                meta["archive.dirs"] = zip.Entries.Count(e => e.FullName.EndsWith('/')).ToString();
                var preview = string.Join('\n', names.Take(40));
                return new AnalyzerOutput
                {
                    ExtractedText = preview,
                    Metadata = meta,
                    Evidence = [new Evidence("archive-manifest",
                        $"ZIP manifest ({zip.Entries.Count} entries listed, never extracted)", 6, ClassifierSource.Local)],
                    Warnings = zip.Entries.Count > MaxEntries
                        ? [$"manifest truncated at {MaxEntries}/{zip.Entries.Count} entries"]
                        : warnings,
                };
            }
        }

        // Signature check for non-zip archives
        var sig = new byte[8];
        var n = await stream.ReadAsync(sig, ct);
        var ok = ext switch
        {
            "rar" => n >= 7 && sig[..7].SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 }) ,
            "7z" => n >= 6 && sig[..6].SequenceEqual(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }),
            "gz" => n >= 2 && sig[0] == 0x1F && sig[1] == 0x8B,
            _ => n >= 0,
        };
        if (!ok) warnings.Add($"{ext} signature mismatch — file may be corrupt or misnamed");
        else warnings.Add($"{ext} manifest parsing not implemented — signature verified only");

        return new AnalyzerOutput
        {
            Metadata = meta,
            Evidence = [new Evidence("archive-container", $"{ext.ToUpperInvariant()} archive", 5, ClassifierSource.Local)],
            Warnings = warnings,
            Reliable = ok,
        };
    }
}
