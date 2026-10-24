namespace FileMind.Domain.Classification;

public sealed record SubjectEntry(
    string Id,
    string Name,
    string? Code,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Keywords);

public sealed record ProjectEntry(
    string Id,
    string Name,
    string? ClientName,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Keywords);

public interface ISubjectCatalog
{
    IReadOnlyList<SubjectEntry> Subjects { get; }
}

public interface IProjectCatalog
{
    IReadOnlyList<ProjectEntry> Projects { get; }
}

public sealed class InMemorySubjectCatalog(IReadOnlyList<SubjectEntry> subjects) : ISubjectCatalog
{
    public IReadOnlyList<SubjectEntry> Subjects { get; } = subjects;
}

public sealed class InMemoryProjectCatalog(IReadOnlyList<ProjectEntry> projects) : IProjectCatalog
{
    public IReadOnlyList<ProjectEntry> Projects { get; } = projects;
}
