using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// MP3 ID3v2 frames (TIT2/TPE1/TALB/TDRC/TYER/TCON) and FLAC Vorbis comments
/// (TITLE/ARTIST/ALBUM/DATE). Head-only reads; ID3v1 fallback for MP3.
/// </summary>
public sealed class AudioTagAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "mp3", "flac", "ogg", "m4a", "wav" };

    public string Name => "audio-tags";

    public bool CanAnalyze(FileCandidate c) => Extensions.Contains(c.Extension);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        var buf = new byte[Math.Min(candidate.SizeBytes, Math.Min(budget.MaxBytes, 512 * 1024))];
        var read = await stream.ReadAsync(buf, ct);
        var head = buf.AsSpan(0, read);

        return candidate.Extension.TrimStart('.').ToLowerInvariant() switch
        {
            "mp3" => ParseId3(head),
            "flac" => ParseFlac(head),
            _ => new AnalyzerOutput { Reliable = false, Warnings = ["metadata parser not implemented for this container"] },
        };
    }

    private static AnalyzerOutput ParseId3(Span<byte> b)
    {
        var meta = new Dictionary<string, string>();
        var warnings = new List<string>();
        if (b.Length < 10 || b[0] != 'I' || b[1] != 'D' || b[2] != '3')
        {
            return new AnalyzerOutput { Reliable = false, Warnings = ["no ID3v2 tag (ID3v1 fallback not read)"] };
        }
        var major = b[3];
        var tagSize = Synchsafe(b.Slice(6, 4));
        var pos = 10;
        var end = Math.Min(10 + tagSize, b.Length);
        while (pos + 10 <= end)
        {
            var id = Encoding.ASCII.GetString(b.Slice(pos, 4));
            if (id[0] == '\0' || id.Any(ch => !char.IsLetterOrDigit(ch))) break;
            var size = major >= 4 ? Synchsafe(b.Slice(pos + 4, 4)) : Be32(b.Slice(pos + 4, 4));
            if (size <= 0 || pos + 10 + size > b.Length) break;
            var frame = b.Slice(pos + 10, size);
            var text = DecodeFrameText(frame);
            switch (id)
            {
                case "TIT2": meta["title"] = text; break;
                case "TPE1": meta["author"] = text; meta["artist"] = text; break;
                case "TALB": meta["album"] = text; break;
                case "TYER" or "TDRC":
                    meta["audio.year"] = text;
                    if (text.Length >= 4) meta["year"] = text[..4];
                    break;
                case "TCON": meta["genre"] = text; break;
            }
            pos += 10 + size;
        }
        var ev = new List<Evidence> { new("audio-id3", "ID3v2 tag present", 5, ClassifierSource.Local) };
        if (meta.ContainsKey("title")) ev.Add(new Evidence("audio-title", "Track title metadata", 8, ClassifierSource.Local));
        return new AnalyzerOutput { Metadata = meta, Evidence = ev, Warnings = warnings };
    }

    private static AnalyzerOutput ParseFlac(Span<byte> b)
    {
        var meta = new Dictionary<string, string>();
        if (b.Length < 8 || !b[..4].SequenceEqual("fLaC"u8.ToArray()))
            return new AnalyzerOutput { Reliable = false, Warnings = ["missing fLaC marker"] };
        var pos = 4;
        while (pos + 4 <= b.Length)
        {
            var type = b[pos] & 0x7F;
            var len = (b[pos + 1] << 16) | (b[pos + 2] << 8) | b[pos + 3];
            if (pos + 4 + len > b.Length) break;
            if (type == 4) // VORBIS_COMMENT
            {
                var vc = b.Slice(pos + 4, len);
                try
                {
                    var p = 0;
                    var vendorLen = Le32(vc, p); p += 4 + vendorLen;
                    var count = Le32(vc, p); p += 4;
                    for (var i = 0; i < count && p + 4 <= vc.Length; i++)
                    {
                        var l = Le32(vc, p); p += 4;
                        if (p + l > vc.Length) break;
                        var kv = Encoding.UTF8.GetString(vc.Slice(p, l));
                        p += l;
                        var eq = kv.IndexOf('=');
                        if (eq < 0) continue;
                        var k = kv[..eq].ToUpperInvariant();
                        var v = kv[(eq + 1)..];
                        if (k == "TITLE") meta["title"] = v;
                        if (k == "ARTIST") { meta["author"] = v; meta["artist"] = v; }
                        if (k == "ALBUM") meta["album"] = v;
                        if (k is "DATE" or "YEAR") { meta["audio.year"] = v; if (v.Length >= 4) meta["year"] = v[..4]; }
                    }
                }
                catch { /* malformed comment block */ }
            }
            if ((b[pos] & 0x80) != 0) break; // last metadata block
            pos += 4 + len;
        }
        var ev = new List<Evidence> { new("audio-flac", "FLAC audio", 5, ClassifierSource.Local) };
        if (meta.ContainsKey("title")) ev.Add(new Evidence("audio-title", "Track title metadata", 8, ClassifierSource.Local));
        return new AnalyzerOutput { Metadata = meta, Evidence = ev };
    }

    private static int Synchsafe(Span<byte> b) =>
        (b[0] & 0x7F) << 21 | (b[1] & 0x7F) << 14 | (b[2] & 0x7F) << 7 | (b[3] & 0x7F);

    private static int Be32(Span<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    private static int Le32(Span<byte> b, int o) =>
        b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);

    private static string DecodeFrameText(Span<byte> frame)
    {
        if (frame.Length < 2) return "";
        var enc = frame[0];
        var body = frame[1..];
        var text = enc switch
        {
            1 or 2 => Encoding.BigEndianUnicode.GetString(body),
            3 => Encoding.UTF8.GetString(body),
            _ => Encoding.Latin1.GetString(body),
        };
        return text.Trim('\0', ' ');
    }
}
