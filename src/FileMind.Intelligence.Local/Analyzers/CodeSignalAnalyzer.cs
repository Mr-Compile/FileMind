using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;
using FileMind.Intelligence.Local.Analysis;

namespace FileMind.Intelligence.Local.Analyzers;

/// <summary>
/// Code files: bounded head-read → language heuristics (shebang, using,
/// import, package, preprocessor) + line/comment signals. Complements MimeMap.
/// </summary>
public sealed class CodeSignalAnalyzer : IFileAnalyzer
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs", "js", "ts", "tsx", "jsx", "py", "java", "cpp", "c", "h", "hpp",
        "rs", "go", "php", "rb", "swift", "kt", "sql", "sh", "ps1", "fs", "fsx", "vb",
    };

    public string Name => "code-signals";

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
        var head = Encoding.UTF8.GetString(buf, 0, read);
        var lines = head.Split('\n');
        var previewLines = lines.Take(200).ToArray();
        var preview = string.Join('\n', previewLines);

        var (lang, confidence) = DetectLanguage(candidate.Extension, preview);
        var meta = new Dictionary<string, string>
        {
            ["code.lines.read"] = lines.Length.ToString(),
        };
        if (lang is not null) meta["code.language"] = lang;

        var evidence = new List<Evidence>
        {
            new("code-file", $"Source code ({candidate.Extension})", 5, ClassifierSource.Local),
        };
        if (lang is not null)
            evidence.Add(new Evidence("code-language", $"Language heuristic: {lang}", 6, ClassifierSource.Local));

        return new AnalyzerOutput
        {
            ExtractedText = preview.Length > 0 ? preview[..Math.Min(preview.Length, budget.MaxChars)] : null,
            Metadata = meta,
            Evidence = evidence,
            Reliable = true,
        };
    }

    private static (string? Lang, double Conf) DetectLanguage(string ext, string head)
    {
        var e = ext.TrimStart('.').ToLowerInvariant();
        // extension is the primary signal; content corroborates
        return e switch
        {
            "cs" or "fs" or "fsx" or "vb" => ("dotnet", 0.9),
            "py" => head.Contains("import ") || head.Contains("def ") ? ("python", 0.9) : ("python", 0.6),
            "js" or "jsx" => ("javascript", 0.8),
            "ts" or "tsx" => ("typescript", 0.85),
            "java" => ("java", 0.85),
            "cpp" or "hpp" or "h" or "c" => head.Contains("#include") ? ("c-cpp", 0.9) : ("c-cpp", 0.6),
            "rs" => head.Contains("fn ") || head.Contains("let ") ? ("rust", 0.85) : ("rust", 0.6),
            "go" => head.Contains("package ") ? ("go", 0.9) : ("go", 0.6),
            "php" => head.Contains("<?php") ? ("php", 0.95) : (null, 0),
            "rb" => ("ruby", 0.7),
            "swift" => ("swift", 0.8),
            "kt" => ("kotlin", 0.8),
            "sql" => ("sql", 0.8),
            "sh" => head.StartsWith("#!") ? ("shell", 0.9) : ("shell", 0.6),
            "ps1" => ("powershell", 0.8),
            _ => (null, 0),
        };
    }
}
