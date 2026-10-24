using FileMind.WinUI.Controls;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var be = await App.Backend;
        var report = await be.Storage.AnalyzeAsync();
        var recent = await be.Pipeline.RecentOperationsAsync(8);
        var inbox = await ((FileMind.Application.Ports.ISmartInboxRepository)be.Store).GetOpenAsync();

        LoadingView.Visibility = Visibility.Collapsed;
        DashContent.Visibility = Visibility.Visible;

        if (report.IsSuccess)
        {
            StatFiles.Text = report.Value!.FileCount.ToString("N0");
            StatBytes.Text = FormatBytes(report.Value.TotalBytes);
            StatDupes.Text = FormatBytes(report.Value.DuplicateReclaimableBytes);
        }
        else
        {
            StatFiles.Text = "—"; StatBytes.Text = report.Error!.Message; StatDupes.Text = "—";
        }

        if (inbox.IsSuccess) StatInbox.Text = inbox.Value!.Count.ToString("N0");

        if (recent.IsSuccess)
        {
            foreach (var op in recent.Value!)
                RecentList.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FmStatusChip { State = MapStatus(op.Status) },
                        new TextBlock
                        {
                            Text = $"{op.Items.Count} item(s) · {op.CreatedAt.LocalDateTime:g}",
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                });
            if (recent.Value!.Count == 0)
                RecentList.Children.Add(new TextBlock
                {
                    Text = "No operations yet — organize something and it shows up here.",
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
                });
        }

        if (be.RecoveredOnStart > 0)
        {
            RecoveredNote.Visibility = Visibility.Visible;
            RecoveredText.Text =
                $"{be.RecoveredOnStart} interrupted operation(s) recovered at startup — see Activity.";
        }
    }

    internal static FmOpState MapStatus(FileMind.Domain.Contracts.OperationStatus status) => status switch
    {
        FileMind.Domain.Contracts.OperationStatus.Committed => FmOpState.Completed,
        FileMind.Domain.Contracts.OperationStatus.Failed => FmOpState.Failed,
        FileMind.Domain.Contracts.OperationStatus.NeedsAttention => FmOpState.CompletedWithWarnings,
        FileMind.Domain.Contracts.OperationStatus.RolledBack => FmOpState.Cancelled,
        FileMind.Domain.Contracts.OperationStatus.RollingBack => FmOpState.Running,
        FileMind.Domain.Contracts.OperationStatus.Planned => FmOpState.Queued,
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
