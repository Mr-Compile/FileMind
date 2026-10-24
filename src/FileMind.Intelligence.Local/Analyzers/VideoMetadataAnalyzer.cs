using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// ISO-BMFF (mp4/mov/m4v) box walk: moov/mvhd timescale+duration, first tkhd
/// width/height (16.16 fixed point). MKV/AVI report container kind only.
/// </summary>
public sealed class VideoMetadataAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "mp4", "m4v", "mov", "mkv", "avi", "wmv", "webm" };

    public string Name => "video-metadata";

    public bool CanAnalyze(FileCandidate c) => Extensions.Contains(c.Extension);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        var buf = new byte[Math.Min(candidate.SizeBytes, Math.Min(budget.MaxBytes, 2 * 1024 * 1024))];
        var read = await stream.ReadAsync(buf, ct);
        var head = buf.AsSpan(0, read);
        var meta = new Dictionary<string, string>();
        var warnings = new List<string>();
        var ev = new List<Evidence>();

        switch (candidate.Extension.TrimStart('.').ToLowerInvariant())
        {
            case "mp4" or "m4v" or "mov":
                if (head.Length < 12 || !head.Slice(4, 4).SequenceEqual("ftyp"u8.ToArray()))
                    return new AnalyzerOutput { Reliable = false, Warnings = ["missing ftyp box — not ISO-BMFF"] };
                WalkBoxes(head, 0, head.Length, meta, depth: 0);
                ev.Add(new Evidence("video-isobmff", "MP4/MOV container", 5, ClassifierSource.Local));
                if (meta.ContainsKey("video.duration.sec"))
                    ev.Add(new Evidence("video-duration", "Duration from mvhd", 6, ClassifierSource.Local));
                break;
            case "mkv" or "webm":
                ev.Add(new Evidence("video-mkv", "Matroska/WebM container", 5, ClassifierSource.Local));
                warnings.Add("Matroska metadata parsing not implemented — container only");
                break;
            default:
                ev.Add(new Evidence("video-container", $"Video container ({candidate.Extension})", 4, ClassifierSource.Local));
                warnings.Add("container metadata parsing not implemented for this format");
                break;
        }

        return new AnalyzerOutput { Metadata = meta, Evidence = ev, Warnings = warnings };
    }

    private static void WalkBoxes(Span<byte> b, int start, int end, Dictionary<string, string> meta, int depth)
    {
        if (depth > 4) return;
        var pos = start;
        while (pos + 8 <= end)
        {
            var size = Be32(b, pos);
            var type = Encoding.ASCII.GetString(b.Slice(pos + 4, 4));
            var contentStart = pos + 8;
            long contentEnd = size == 0 ? end : Math.Min((long)pos + size, end);
            if (size == 1 && pos + 16 <= end) // largesize
            {
                var big = ((long)Be32(b, pos + 8) << 32) | (uint)Be32(b, pos + 12);
                contentStart = pos + 16;
                contentEnd = Math.Min(pos + big, end);
            }
            if (contentEnd <= contentStart) break;

            var content = b.Slice(contentStart, (int)(contentEnd - contentStart));
            switch (type)
            {
                case "moov" or "trak" or "mdia" or "minf" or "stbl":
                    WalkBoxes(b, contentStart, (int)contentEnd, meta, depth + 1);
                    break;
                case "mvhd" when content.Length >= 20:
                {
                    var version = content[0];
                    var (timescale, duration) = version == 1 && content.Length >= 32
                        ? ((long)Be32(content, 20), ((long)Be32(content, 24) << 32) | (uint)Be32(content, 28))
                        : ((long)Be32(content, 12), (long)Be32(content, 16));
                    if (timescale > 0)
                        meta["video.duration.sec"] = (duration / (double)timescale).ToString("0.##");
                    break;
                }
                case "tkhd" when content.Length >= 84 && !meta.ContainsKey("video.width"):
                {
                    var version = content[0];
                    var tail = version == 1 ? 88 : 76; // width/height are last 8 bytes
                    if (content.Length >= tail)
                    {
                        var w = Be32(content, content.Length - 8) / 65536.0;
                        var h = Be32(content, content.Length - 4) / 65536.0;
                        if (w > 0 && h > 0)
                        {
                            meta["video.width"] = ((int)w).ToString();
                            meta["video.height"] = ((int)h).ToString();
                        }
                    }
                    break;
                }
            }
            if (size == 0) break;
            pos += (int)size;
        }
    }

    private static int Be32(Span<byte> b, int o) =>
        (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
}
