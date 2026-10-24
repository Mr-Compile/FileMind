using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;

namespace FileMind.Application.Scanning;

public sealed class FileScanner(
    IFileSystemAdapter fs,
    IPathNormalizer paths,
    ExclusionMatcher? exclusions = null)
{
    public async IAsyncEnumerable<FileCandidate> ScanAsync(
        string root, bool recursive = true,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var scope = InferScope(root);
        await foreach (var entry in fs.EnumerateAsync(root, recursive, ct).WithCancellation(ct))
        {
            var name = paths.FileName(entry.Path);
            var normalized = paths.Normalize(entry.Path);
            if (exclusions?.IsExcluded(name) == true || exclusions?.IsExcluded(normalized) == true)
                continue;

            var ext = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : "";
            yield return new FileCandidate
            {
                ScanId = StableScanId(normalized),
                SourceUri = entry.Path,
                DisplayName = name,
                Extension = ext,
                MimeType = MimeMap.FromExtension(ext),
                SizeBytes = entry.SizeBytes,
                CreatedAt = entry.CreatedAt,
                ModifiedAt = entry.ModifiedAt,
                SourceScope = scope
            };
        }
    }

    private static SourceScope InferScope(string root)
    {
        var r = root.Replace('/', '\\');
        if (r.Contains("\\Downloads", StringComparison.OrdinalIgnoreCase)) return SourceScope.Downloads;
        if (r.Contains("\\Documents", StringComparison.OrdinalIgnoreCase)) return SourceScope.Documents;
        if (r.Contains("\\Desktop", StringComparison.OrdinalIgnoreCase)) return SourceScope.Desktop;
        if (r.Contains("\\Pictures", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("\\Videos", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("\\Music", StringComparison.OrdinalIgnoreCase)) return SourceScope.Media;
        return SourceScope.Other;
    }

    private static string StableScanId(string normalizedPath) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)))[..16];
}
