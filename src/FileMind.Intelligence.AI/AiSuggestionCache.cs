using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using FileMind.Application.Ports;

namespace FileMind.Intelligence.AI;

/// <summary>
/// TTL cache keyed by a hash of the minimized request payload (PLAN-009 M2.1).
/// Cache hits spend zero quota. In-memory only — suggestions are advisory and
/// cheap to recompute.
/// </summary>
public sealed class AiSuggestionCache(IClock clock, int maxEntries = 256, TimeSpan? ttl = null)
{
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromHours(6);
    private readonly ConcurrentDictionary<string, (AiSuggestion Suggestion, DateTimeOffset At)> _entries = new();

    public bool TryGet(string requestHash, out AiSuggestion suggestion)
    {
        suggestion = null!;
        if (_entries.TryGetValue(requestHash, out var e) && clock.UtcNow - e.At <= _ttl)
        {
            suggestion = e.Suggestion;
            return true;
        }
        _entries.TryRemove(requestHash, out _);
        return false;
    }

    public void Store(string requestHash, AiSuggestion suggestion)
    {
        if (_entries.Count >= maxEntries)
        {
            var now = clock.UtcNow;
            foreach (var kv in _entries.Where(kv => now - kv.Value.At > _ttl)) _entries.TryRemove(kv.Key, out _);
            if (_entries.Count >= maxEntries)
                _entries.TryRemove(_entries.OrderBy(kv => kv.Value.At).First().Key, out _);
        }
        _entries[requestHash] = (suggestion, clock.UtcNow);
    }

    /// <summary>Canonical minimized-payload hash — same input always hashes identically.</summary>
    public static string HashOf(AiRequestContext context)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            name = context.DisplayName,
            ext = context.Extension,
            meta = context.Metadata.OrderBy(kv => kv.Key, StringComparer.Ordinal),
            snip = context.ExtractedSnippet
        });
        return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
    }
}
