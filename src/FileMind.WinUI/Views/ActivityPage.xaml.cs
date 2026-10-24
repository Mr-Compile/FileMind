using FileMind.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Activity & operation center — live operation records from the
/// store; undo flows through OrganizerPipeline.UndoAsync.</summary>
public sealed partial class ActivityPage : Page
{
    private sealed record OpRow(FmOpState State, string Summary, string Detail, string OperationId);

    public ActivityPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var be = await App.Backend;
        var ops = await be.Pipeline.RecentOperationsAsync(50);
        if (!ops.IsSuccess) return;

        var rows = ops.Value!.Select(op =>
        {
            var done = op.Items.Count(i => i.Status == FileMind.Domain.Contracts.OperationStatus.Committed);
            var attention = op.Items.Count(i => i.Status is FileMind.Domain.Contracts.OperationStatus.Failed
                or FileMind.Domain.Contracts.OperationStatus.NeedsAttention);
            return new OpRow(
                DashboardPage.MapStatus(op.Status),
                $"{op.Items.Count} item(s) — {done} done" + (attention > 0 ? $", {attention} need attention" : ""),
                $"{op.OperationId[..8]}… · {op.CreatedAt.LocalDateTime:g}" +
                    (op.UndoOfOperationId is not null ? " · undo" : ""),
                op.OperationId);
        }).ToList();
        OpList.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UndoButton.IsEnabled = OpList.SelectedItem is OpRow;

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (OpList.SelectedItem is not OpRow row) return;
        var be = await App.Backend;
        await be.Pipeline.UndoAsync(row.OperationId);
        await RefreshAsync();
    }
}
