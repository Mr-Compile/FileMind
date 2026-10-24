using FileMind.Application.Ports;

namespace FileMind.Platform.Windows;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
