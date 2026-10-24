using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>Default analyzer set for <see cref="AnalyzerRegistry"/>.</summary>
public static class AnalyzerDefaults
{
    public static List<IFileAnalyzer> All() =>
    [
        new PlainTextAnalyzer(),
        new OoxmlAnalyzer(),
        new PdfAnalyzer(),
        new ImageMetadataAnalyzer(),
        new AudioTagAnalyzer(),
        new VideoMetadataAnalyzer(),
        new ArchiveManifestAnalyzer(),
        new CodeSignalAnalyzer(),
    ];
}
