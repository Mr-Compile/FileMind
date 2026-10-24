using FileMind.Domain.Ports;

namespace FileMind.Platform.Windows;

public sealed class WindowsProtectedPaths : IProtectedPathProvider
{
    private readonly WindowsPathNormalizer _paths = new();
    private readonly List<string> _roots;

    public WindowsProtectedPaths(IEnumerable<string>? userRoots = null)
    {
        _roots = DefaultRoots().Concat(userRoots ?? []).Select(_paths.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> ProtectedRoots => _roots;

    public bool IsProtected(string path)
    {
        var normalized = _paths.Normalize(path);
        return _roots.Any(r =>
            normalized.Equals(r, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> DefaultRoots()
    {
        var windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        yield return windir;
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        var systemDrive = Environment.GetEnvironmentVariable("SYSTEMDRIVE") ?? "C:";
        yield return systemDrive + "\\$Recycle.Bin";
        yield return systemDrive + "\\System Volume Information";
    }
}
