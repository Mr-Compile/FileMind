using FileMind.Application.Ports;

namespace FileMind.Application.Watching;

public static class WatchEventKind
{
    public const string Created = "Created";
    public const string Changed = "Changed";
    public const string Renamed = "Renamed";
    public const string Deleted = "Deleted";
    public const string Error = "Error";

    public static string Normalize(string kind) => kind switch
    {
        Created or Changed or Renamed or Deleted or Error => kind,
        _ => Error
    };
}

public sealed record QueuedWatchEvent(
    WatchEvent Event,
    DateTimeOffset EnqueuedAt,
    int Attempts = 0);

/// <summary>Persisted payload for a watch-organize job (jobs table).</summary>
public sealed record WatchJobPayload(
    string Path,
    string ChangeKind,
    string Mode,
    bool Apply,
    int Attempts = 0)
{
    public const string JobType = "watch-event";
}
