using FileMind.Domain.Ports;

namespace FileMind.Platform.Windows;

public sealed class WindowsPathNormalizer : IPathNormalizer
{
    public string Normalize(string path)
    {
        var p = path.Replace('/', '\\').TrimEnd('\\');
        return p.Length == 2 && p[1] == ':' ? p + "\\" : p;
    }

    public string Combine(params string[] parts) =>
        string.Join('\\', parts.Select(p => p.Replace('/', '\\').Trim('\\')).Where(p => p.Length > 0));

    public bool IsUnder(string path, string root) =>
        Normalize(path).StartsWith(Normalize(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    public string FileName(string path) => path.Split('/', '\\').Last();

    public string? DirectoryOf(string path)
    {
        var i = path.LastIndexOfAny(['/', '\\']);
        return i > 0 ? path[..i] : null;
    }
}
