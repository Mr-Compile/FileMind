using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// JPEG (SOF dims + APP1 EXIF IFD0 strings) and PNG (IHDR dims + tEXt/iTXt
/// chunks). Reads at most 256 KiB — metadata lives at the head.
/// </summary>
public sealed class ImageMetadataAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png" };

    public string Name => "image-metadata";

    public bool CanAnalyze(FileCandidate c) => Extensions.Contains(c.Extension);

    public async Task<AnalyzerOutput> AnalyzeAsync(
        FileCandidate candidate, IFileSystemAdapter fs, ExtractionBudget budget, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(candidate.SourceUri, ct);
        if (!opened.IsSuccess)
            return new AnalyzerOutput { Reliable = false, Warnings = [$"open failed: {opened.Error!.Message}"] };

        await using var stream = opened.Value!;
        var buf = new byte[Math.Min(candidate.SizeBytes, Math.Min(budget.MaxBytes, 256 * 1024))];
        var read = await stream.ReadAsync(buf, ct);

        return candidate.Extension.TrimStart('.').ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => ParseJpeg(buf.AsSpan(0, read)),
            "png" => ParsePng(buf.AsSpan(0, read)),
            _ => new AnalyzerOutput { Reliable = false },
        };
    }

    private static AnalyzerOutput ParseJpeg(Span<byte> b)
    {
        var meta = new Dictionary<string, string>();
        var warnings = new List<string>();
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8)
            return new AnalyzerOutput { Reliable = false, Warnings = ["missing JPEG SOI marker"] };

        var pos = 2;
        while (pos + 4 <= b.Length)
        {
            if (b[pos] != 0xFF) { pos++; continue; }
            var marker = b[pos + 1];
            var segLen = (b[pos + 2] << 8) | b[pos + 3];
            if (segLen < 2 || pos + 2 + segLen > b.Length) break;
            var seg = b.Slice(pos + 4, segLen - 2);

            if (marker is >= 0xC0 and <= 0xC3 && seg.Length >= 6)
            {
                meta["image.height"] = ((seg[1] << 8) | seg[2]).ToString();
                meta["image.width"] = ((seg[3] << 8) | seg[4]).ToString();
            }
            else if (marker == 0xE1 && seg.Length > 14 &&
                     seg[..6].SequenceEqual("Exif\0\0"u8.ToArray()))
            {
                ParseExif(seg[6..], meta, warnings);
            }
            if (marker == 0xDA) break; // SOS: image data begins
            pos += 2 + segLen;
        }

        var ev = new List<Evidence> { new("image-jpeg", "JPEG image", 5, ClassifierSource.Local) };
        if (meta.ContainsKey("exif.datetime")) ev.Add(new Evidence("exif-present", "EXIF metadata present", 6, ClassifierSource.Local));
        return new AnalyzerOutput { Metadata = meta, Evidence = ev, Warnings = warnings };
    }

    private static void ParseExif(Span<byte> tiff, Dictionary<string, string> meta, List<string> warnings)
    {
        try
        {
            var littleEndian = tiff[..2].SequenceEqual("II"u8.ToArray());
            // static local funcs: Span params can't be captured by closures
            static ushort U16(Span<byte> t, bool le, int o) => le
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(t[o..])
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(t[o..]);
            static uint U32(Span<byte> t, bool le, int o) => le
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(t[o..])
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(t[o..]);

            var ifd0 = (int)U32(tiff, littleEndian, 4);
            var count = U16(tiff, littleEndian, ifd0);
            for (var i = 0; i < count; i++)
            {
                var e = ifd0 + 2 + i * 12;
                if (e + 12 > tiff.Length) break;
                var tag = U16(tiff, littleEndian, e);
                var len = (int)U32(tiff, littleEndian, e + 4);
                var valueOff = len <= 4 ? e + 8 : (int)U32(tiff, littleEndian, e + 8);
                if (valueOff < 0 || valueOff + len > tiff.Length) continue;
                var s = Encoding.ASCII.GetString(tiff.Slice(valueOff, len)).Trim('\0', ' ');

                switch (tag)
                {
                    case 0x010F: meta["exif.make"] = s; break;
                    case 0x0110: meta["exif.model"] = s; break;
                    case 0x010E: meta["exif.description"] = s; meta["title"] = s; break;
                    case 0x013B: meta["author"] = s; break;
                    case 0x0132 or 0x9003:
                        meta["exif.datetime"] = s;
                        if (s.Length >= 4) meta["year"] = s[..4];
                        break;
                }
            }
        }
        catch (Exception ex) { warnings.Add($"EXIF parse partial: {ex.Message}"); }
    }

    private static AnalyzerOutput ParsePng(Span<byte> b)
    {
        var meta = new Dictionary<string, string>();
        byte[] sig = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (b.Length < 24 || !b[..8].SequenceEqual(sig))
            return new AnalyzerOutput { Reliable = false, Warnings = ["missing PNG signature"] };

        meta["image.width"] = ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19]).ToString();
        meta["image.height"] = ((b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]).ToString();

        var pos = 8;
        while (pos + 12 <= b.Length)
        {
            var len = (b[pos] << 24) | (b[pos + 1] << 16) | (b[pos + 2] << 8) | b[pos + 3];
            var type = Encoding.ASCII.GetString(b.Slice(pos + 4, 4));
            if (len < 0 || pos + 12 + len > b.Length) break;
            if (type is "tEXt" or "iTXt")
            {
                var text = Encoding.Latin1.GetString(b.Slice(pos + 8, len));
                var nul = text.IndexOf('\0');
                if (nul > 0 && nul + 1 < text.Length)
                {
                    var key = text[..nul];
                    var val = type == "iTXt"
                        ? Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(text[(nul + 1)..])).Split('\0').Last()
                        : text[(nul + 1)..];
                    if (key.Equals("Title", StringComparison.OrdinalIgnoreCase)) meta["title"] = val;
                    if (key.Equals("Author", StringComparison.OrdinalIgnoreCase)) meta["author"] = val;
                    if (key.Equals("Description", StringComparison.OrdinalIgnoreCase)) meta["description"] = val;
                }
            }
            if (type == "IEND" || type == "IDAT") break;
            pos += 12 + len;
        }

        return new AnalyzerOutput
        {
            Metadata = meta,
            Evidence = [new Evidence("image-png", "PNG image", 5, ClassifierSource.Local)],
        };
    }
}
