using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Platform.Windows;

/// <summary>
/// Backend-phase notification sink: console line + optional rolling log file
/// (paths/ids only — never file contents). UI/toast rendering is PLAN-014.
/// </summary>
public sealed class ConsoleNotificationService(string? logPath = null) : INotificationService
{
    public async Task<Result<bool>> NotifyAsync(Notification notification, CancellationToken ct = default)
    {
        var line = $"[notify] {notification.Title}: {notification.Body}" +
                   (notification.OperationId is null ? "" : $" (op {notification.OperationId})");
        Console.WriteLine(line);
        if (logPath is not null)
        {
            try
            {
                await File.AppendAllTextAsync(logPath,
                    $"{DateTimeOffset.UtcNow:u} {line}{Environment.NewLine}", ct);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(FileMindError.Io(ex.Message));
            }
        }
        return Result<bool>.Ok(true);
    }
}
