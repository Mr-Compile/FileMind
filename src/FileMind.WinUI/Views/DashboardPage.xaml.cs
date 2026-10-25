using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.WinUI.Controls;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace FileMind.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private void GoTo(string tag)
    {
        if (App.MainAppWindow?.Content is Shell shell) shell.NavigateTo(tag);
    }

    private void Organize_Click(object sender, RoutedEventArgs e) => GoTo("organizer");
    private void GoFiles(object s, RoutedEventArgs e) => GoTo("files");
    private void GoTools(object s, RoutedEventArgs e) => GoTo("tools");
    private void GoActivity(object s, RoutedEventArgs e) => GoTo("activity");
    private void GoDevices(object s, PointerRoutedEventArgs e) => GoTo("devices");
    private void GoTools(object s, PointerRoutedEventArgs e) => GoTo("tools");
    private void GoActivity(object s, PointerRoutedEventArgs e) => GoTo("activity");

    private async Task LoadAsync()
    {
        var be = await App.Backend;
        var report = await be.Storage.AnalyzeAsync();
        var recent = await be.Pipeline.RecentOperationsAsync(8);
        var inbox = await ((FileMind.Application.Ports.ISmartInboxRepository)be.Store).GetOpenAsync();

        LoadingView.Visibility = Visibility.Collapsed;
        DashContent.Visibility = Visibility.Visible;

        LoadStorage(report);
        LoadDevices();
        LoadOps(recent, inbox);
        LoadCleanup(report);
        LoadRecent(recent);

        if (be.RecoveredOnStart > 0)
        {
            RecoveredNote.Visibility = Visibility.Visible;
            RecoveredText.Text =
                $"{be.RecoveredOnStart} interrupted operation(s) recovered at startup — see Activity.";
        }
    }

    private void LoadStorage(Result<StorageReport> report)
    {
        // drive meter from real DriveInfo (mockup: C: meter + used/free line)
        try
        {
            var drive = new System.IO.DriveInfo("C");
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            var pct = (int)Math.Round(100.0 * used / drive.TotalSize);
            StorageMeterFill.Width = Math.Max(0, StorageMeter.ActualWidth * pct / 100);
            StorageMeter.SizeChanged += (_, _) =>
                StorageMeterFill.Width = StorageMeter.ActualWidth * pct / 100;
            StorageSummary.Text =
                $"{FormatBytes(used)} of {FormatBytes(drive.TotalSize)} used · {FormatBytes(drive.AvailableFreeSpace)} free";
        }
        catch { StorageSummary.Text = "Drive usage unavailable."; }

        if (report.IsSuccess && report.Value!.ByKind.Count > 0)
        {
            foreach (var bucket in report.Value.ByKind.OrderByDescending(b => b.Bytes).Take(5))
                StorageKinds.Children.Add(Row(
                    bucket.Label, $"{FormatBytes(bucket.Bytes)} · {bucket.Count:N0}"));
        }
        else
        {
            StorageKinds.Children.Add(Muted("Index a folder to see the storage breakdown."));
        }
    }

    private void LoadDevices()
    {
        // This PC — always connected
        DeviceList.Children.Add(DeviceRow("This PC", "Windows", connected: true));
        // Android — the Windows app has no phone bridge; truth: disconnected
        DeviceList.Children.Add(DeviceRow("Android phone", "companion app (PLAN-021)", connected: false));
        // removable drives
        try
        {
            foreach (var d in System.IO.DriveInfo.GetDrives()
                         .Where(d => d.DriveType == System.IO.DriveType.Removable && d.IsReady))
                DeviceList.Children.Add(DeviceRow($"{d.Name} {d.VolumeLabel}", "removable", connected: true));
        }
        catch { }
    }

    private void LoadOps(Result<IReadOnlyList<OperationRecord>> recent,
        Result<IReadOnlyList<SmartInboxItem>> inbox)
    {
        if (recent.IsSuccess && recent.Value!.Count > 0)
        {
            var last = recent.Value![0];
            OpsPanel.Children.Add(new TextBlock
            {
                Text = "No operations running right now.",
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextBodySmall"],
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(0, 4, 0, 0) };
            row.Children.Add(Muted($"Last run: {last.Items.Count} item(s) · {last.CreatedAt.LocalDateTime:g}"));
            row.Children.Add(new FmStatusChip { State = MapStatus(last.Status) });
            OpsPanel.Children.Add(row);
        }
        else
        {
            OpsPanel.Children.Add(Muted("No operations yet — run the organizer and it shows up here."));
        }

        if (inbox.IsSuccess && inbox.Value!.Count > 0)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new FmStatusChip { State = FmOpState.CompletedWithWarnings });
            row.Children.Add(Muted($"{inbox.Value!.Count} Smart Inbox item(s)"));
            OpsPanel.Children.Add(row);
        }
    }

    private void LoadCleanup(Result<StorageReport> report)
    {
        if (!report.IsSuccess) { CleanupPanel.Children.Add(Muted("Index a folder first.")); return; }
        var r = report.Value!;
        CleanupPanel.Children.Add(Row(
            "≈ " + FormatBytes(r.DuplicateReclaimableBytes) + " reclaimable in duplicates",
            r.DuplicateReclaimableBytes > 0 ? "Tools → Find duplicates" : "none found"));
        var big = r.Largest.Count(f => f.SizeBytes > 3L * 1024 * 1024 * 1024);
        CleanupPanel.Children.Add(Row($"{big} file(s) over 3 GB", big > 0 ? "Tools → Analyze storage" : ""));
        CleanupPanel.Children.Add(Row($"{r.FileCount:N0} indexed file(s)", "Files"));
    }

    private void LoadRecent(Result<IReadOnlyList<OperationRecord>> recent)
    {
        if (recent.IsSuccess && recent.Value!.Count > 0)
        {
            foreach (var op in recent.Value.Take(4))
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var chip = new FmStatusChip { State = MapStatus(op.Status), VerticalAlignment = VerticalAlignment.Center };
                var text = new TextBlock
                {
                    Text = $"Organized {op.Items.Count} item(s)",
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                var when = Muted(op.CreatedAt.LocalDateTime.ToString("g"));
                when.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(text, 1); Grid.SetColumn(when, 2);
                row.Children.Add(chip); row.Children.Add(text); row.Children.Add(when);
                RecentList.Children.Add(row);
            }
        }
        else
        {
            RecentList.Children.Add(Muted("No operations yet — organize something and it shows up here."));
        }
    }

    private static Grid Row(string label, string trailing)
    {
        var g = new Grid { ColumnSpacing = 8 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var l = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var t = Muted(trailing); t.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(t, 1);
        g.Children.Add(l); g.Children.Add(t);
        return g;
    }

    private static StackPanel DeviceRow(string name, string sub, bool connected)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new FontIcon
        {
            Glyph = "&#xEDA2;", FontSize = 16, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
        });
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = name, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextBody"] });
        texts.Children.Add(Muted(sub));
        row.Children.Add(texts);
        var chip = new Border
        {
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmBadgeBorder"],
            VerticalAlignment = VerticalAlignment.Center
        };
        chip.Child = new TextBlock
        {
            Text = connected ? "Connected" : "Offline",
            FontSize = 11,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[
                connected ? "FmStatusSuccessBrush" : "FmTextSecondaryBrush"]
        };
        row.Children.Add(chip);
        return row;
    }

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        FontSize = 12,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
    };

    internal static FmOpState MapStatus(OperationStatus status) => status switch
    {
        OperationStatus.Committed => FmOpState.Completed,
        OperationStatus.Failed => FmOpState.Failed,
        OperationStatus.NeedsAttention => FmOpState.CompletedWithWarnings,
        OperationStatus.RolledBack => FmOpState.Cancelled,
        OperationStatus.RollingBack => FmOpState.Running,
        OperationStatus.Planned => FmOpState.Queued,
        _ => FmOpState.Running
    };

    internal static string FormatBytes(long b) => b switch
    {
        >= 1L << 30 => $"{b / (double)(1L << 30):0.#} GB",
        >= 1L << 20 => $"{b / (double)(1L << 20):0.#} MB",
        >= 1L << 10 => $"{b / (double)(1L << 10):0.#} KB",
        _ => $"{b} B"
    };
}
