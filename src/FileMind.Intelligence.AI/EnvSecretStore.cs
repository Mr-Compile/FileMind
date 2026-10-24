using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Intelligence.AI;

/// <summary>
/// Dev-opt-in credential source: reads `KEY=value` lines from a `.env` file
/// (gitignored; documented in `.env.example`) then falls back to process env.
/// `SetAsync` writes/updates the `.env` file — plain text, dev only; real
/// deployments use `WindowsSecretStore` (DPAPI).
/// </summary>
public sealed class EnvSecretStore(string? envFilePath = null) : ISecretStore
{
    private readonly string _path = envFilePath
        ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");

    public Task<Result<string>> GetAsync(string key, CancellationToken ct = default)
    {
        var fromEnv = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(fromEnv)) return Task.FromResult(Result<string>.Ok(fromEnv));
        var map = Load();
        return Task.FromResult(map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
            ? Result<string>.Ok(v)
            : Result<string>.Fail(FileMindError.NotFound($"No value for '{key}'.")));
    }

    public Task<Result<bool>> SetAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            var map = Load();
            map[key] = value;
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(_path, map.Select(kv => $"{kv.Key}={kv.Value}"));
            return Task.FromResult(Result<bool>.Ok(true));
        }
        catch (Exception ex) { return Task.FromResult(Result<bool>.Fail(FileMindError.Io(ex.Message))); }
    }

    public Task<Result<bool>> DeleteAsync(string key, CancellationToken ct = default)
    {
        var map = Load();
        if (map.Remove(key))
            File.WriteAllLines(_path, map.Select(kv => $"{kv.Key}={kv.Value}"));
        return Task.FromResult(Result<bool>.Ok(true));
    }

    private Dictionary<string, string> Load()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(_path)) return map;
        foreach (var line in File.ReadAllLines(_path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            var eq = t.IndexOf('=');
            if (eq > 0) map[t[..eq].Trim()] = t[(eq + 1)..].Trim();
        }
        return map;
    }
}
