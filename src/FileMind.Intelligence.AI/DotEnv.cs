namespace FileMind.Intelligence.AI;

/// <summary>
/// Minimal dev .env loader (PLAN-009 recorded decision): finds a ".env" file by
/// walking up from the current directory, then from the app base directory, and
/// copies KEY=VALUE pairs into process environment variables — only for keys
/// that are not already set, so real environment variables always win.
/// .env is gitignored; .env.example documents the supported keys.
/// </summary>
public static class DotEnv
{
    public const string FileName = ".env";

    public static string? Find(string? startDir = null)
    {
        foreach (var start in new[] { startDir ?? Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, FileName);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    public static IReadOnlyDictionary<string, string> Parse(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            if (key.Length > 0) values[key] = value;
        }
        return values;
    }

    /// <summary>Loads the nearest .env into unset process env vars. Returns count applied; 0 when no file.</summary>
    public static int LoadIntoEnvironment(string? path = null)
    {
        var file = path ?? Find();
        if (file is null) return 0;
        var applied = 0;
        foreach (var (key, value) in Parse(file))
        {
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
                applied++;
            }
        }
        return applied;
    }
}
