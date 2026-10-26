using FileMind.Application.Scanning;
using FileMind.Domain.Contracts;
using FileMind.Platform.Windows;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

/// <summary>
/// FileEntry.DisplayName/MimeType (provider metadata) must win over
/// path-derived names — Android SAF/MediaStore docIds can be opaque
/// ("msf:1234"), and the same preference rule applies cross-platform.
/// </summary>
public class ScannerMetadataTests
{
    private readonly FakeFileSystemAdapter _fs = new();
    private readonly WindowsPathNormalizer _paths = new();

    [Fact]
    public async Task Scan_PrefersProviderDisplayNameAndMime()
    {
        // Opaque docId-style path: nothing a path parser could use.
        _fs.SeedFile("C:/scan/1000001234", displayName: "photo.jpg", mimeType: "image/heic");
        var scanner = new FileScanner(_fs, _paths);

        var candidates = new List<FileCandidate>();
        await foreach (var c in scanner.ScanAsync("C:/scan")) candidates.Add(c);

        var file = Assert.Single(candidates);
        Assert.Equal("photo.jpg", file.DisplayName);
        Assert.Equal("jpg", file.Extension);
        Assert.Equal("image/heic", file.MimeType);
    }

    [Fact]
    public async Task Scan_FallsBackToPathNameWhenProviderSilent()
    {
        _fs.SeedFile("C:/scan/report.pdf");
        var scanner = new FileScanner(_fs, _paths);

        var candidates = new List<FileCandidate>();
        await foreach (var c in scanner.ScanAsync("C:/scan")) candidates.Add(c);

        var file = Assert.Single(candidates);
        Assert.Equal("report.pdf", file.DisplayName);
        Assert.Equal("pdf", file.Extension);
        Assert.Equal("application/pdf", file.MimeType);
    }
}
