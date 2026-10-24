using FileMind.Domain.Contracts;

namespace FileMind.Domain.Classification;

public sealed class FileKindMap
{
    private static readonly Dictionary<FileKind, string[]> Map = new()
    {
        [FileKind.Document] = ["pdf", "doc", "docx", "txt", "rtf", "md", "odt"],
        [FileKind.Spreadsheet] = ["xls", "xlsx", "csv", "ods"],
        [FileKind.Presentation] = ["ppt", "pptx", "odp", "key"],
        [FileKind.Image] = ["jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff", "heic", "svg"],
        [FileKind.Video] = ["mp4", "mkv", "avi", "mov", "wmv", "webm", "m4v"],
        [FileKind.Audio] = ["mp3", "wav", "flac", "aac", "ogg", "m4a", "wma"],
        [FileKind.Archive] = ["zip", "rar", "7z", "tar", "gz", "bz2"],
        [FileKind.Code] = ["cs", "sln", "csproj", "js", "ts", "py", "java", "cpp", "c", "h", "rs", "go", "php", "html", "css", "json", "xml", "yml", "yaml", "sql", "sh", "ps1"],
        [FileKind.Executable] = ["exe", "msi", "dll", "bat", "cmd", "com"],
        [FileKind.Font] = ["ttf", "otf", "woff", "woff2"]
    };

    public static FileKind FromExtension(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        foreach (var (kind, exts) in Map)
            if (exts.Contains(ext)) return kind;
        return FileKind.Other;
    }
}

public sealed class ClassifierEngine(
    SubjectMatcher subjectMatcher,
    ProjectMatcher projectMatcher,
    ConfidenceEngine confidence)
{
    public const double ContextOverTypeBonus = 20;

    public ClassificationResult Classify(FileCandidate candidate)
    {
        var evidence = new List<Evidence>();
        var warnings = new List<string>();

        var tokens = Normalizer.Normalize(candidate.DisplayName);

        var fileKind = FileKindMap.FromExtension(candidate.Extension);
        if (fileKind != FileKind.Other)
            evidence.Add(new Evidence("extension-signal",
                $"Extension '{candidate.Extension}' => {fileKind}", 10, ClassifierSource.Local));
        if (candidate.MimeType is not null)
            evidence.Add(new Evidence("mime-signal",
                $"MIME '{candidate.MimeType}'", 8, ClassifierSource.Local));

        var subject = subjectMatcher.Match(tokens);
        evidence.AddRange(subject.Evidence);
        warnings.AddRange(subject.Warnings);

        var project = projectMatcher.Match(tokens);
        evidence.AddRange(project.Evidence);
        warnings.AddRange(project.Warnings);

        if (subject.SubjectId is not null && fileKind != FileKind.Other)
            evidence.Add(new Evidence("context-over-type",
                "Subject context present; context beats generic type routing.",
                ContextOverTypeBonus, ClassifierSource.Local));

        var score = confidence.Score(evidence);
        var band = confidence.Band(score);

        var explanation = BuildExplanation(fileKind, subject.SubjectId, project.ProjectId, band, evidence);

        return new ClassificationResult
        {
            FileKind = fileKind,
            Subject = subject.SubjectId,
            Project = project.ProjectId,
            Evidence = evidence,
            Confidence = score,
            Sources = [ClassifierSource.Local],
            Explanation = explanation,
            Warnings = warnings,
            NeedsReview = band == ConfidenceBand.Medium || warnings.Count > 0
        };
    }

    private static string BuildExplanation(FileKind kind, string? subject, string? project, ConfidenceBand band, List<Evidence> evidence)
    {
        var parts = new List<string> { $"kind={kind}" };
        if (subject is not null) parts.Add($"subject={subject}");
        if (project is not null) parts.Add($"project={project}");
        parts.Add($"band={band}");
        parts.Add($"evidence={evidence.Count}");
        return string.Join("; ", parts);
    }
}
