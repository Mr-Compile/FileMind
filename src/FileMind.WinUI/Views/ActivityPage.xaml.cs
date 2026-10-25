using FileMind.Application.Ports;
using FileMind.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Activity matched to the mockup: operations with per-row undo,
/// Smart Inbox with real ResolveAsync, canonical states legend.</summary>
public sealed partial class ActivityPage : Page
{
    private sealed record OpRow(
        FmOpState State, string OpId, string When, string Summary,
        string ItemsHeader, string ItemsDetail, string OperationId,
        Visibility UndoVisibility, Visibility UndoBadgeVisibility)
    {
        public override string ToString() => $"{State}: {Summary} — {When}";
    }

    private sealed record InboxRow(string Id, string Name, string Path, string Reason)
    {
        public override string ToString() => $"{Name} — {Reason}";
    }

    public ActivityPage()
    {
        InitializeComponent();
        Legend.ItemsSource = new[]
        {
            FmOpState.Ready, FmOpState.Queued, FmOpState.Running, FmOpState.Completed,
            FmOpState.CompletedWithWarnings, FmOpState.Failed, FmOpState.Cancelled,
            FmOpState.Offline, FmOpState.Unsupported, FmOpState.PermissionRequired
        }.Select(s => new FmStatusChip { State = s }).ToList();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var be = await App.Backend;
        var ops = await be.Pipeline.RecentOperationsAsync(50);
        if (ops.IsSuccess)
        {
            var undone = ops.Value!.Where(o => o.UndoOfOperationId is not null)
                .Select(o => o.UndoOfOperationId!).ToHashSet();
            var rows = ops.Value!.Select(op =>
            {
                var done = op.Items.Count(i => i.Status == FileMind.Domain.Contracts.OperationStatus.Committed);
                var failed = op.Items.Count(i => i.Status == FileMind.Domain.Contracts.OperationStatus.Failed);
                var canUndo = op.Status == FileMind.Domain.Contracts.OperationStatus.Committed
                    && op.UndoOfOperationId is null && !undone.Contains(op.OperationId);
                var detail = string.Join("\n", op.Items.Select(i =>
                    $"{i.Operation,-12} {i.OriginalLocation}\n  -> {i.ResultingLocation ?? "—"}  [{i.Status}]"));
                return new OpRow(
                    DashboardPage.MapStatus(op.Status),
                    op.OperationId[..Math.Min(12, op.OperationId.Length)] + "…",
                    op.CreatedAt.LocalDateTime.ToString("g"),
                    $"{done} moved" + (failed > 0 ? $" · {failed} failed" : ""),
                    $"Items ({op.Items.Count})", detail, op.OperationId,
                    canUndo ? Visibility.Visible : Visibility.Collapsed,
                    op.UndoOfOperationId is not null ? Visibility.Visible : Visibility.Collapsed);
            }).ToList();
            OpList.ItemsSource = rows;
            OpsEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        var inbox = await ((ISmartInboxRepository)be.Store).GetOpenAsync();
        if (inbox.IsSuccess)
        {
            var items = inbox.Value!.Select(i => new InboxRow(
                i.Id,
                System.IO.Path.GetFileName(i.Path),
                i.Path,
                i.Reason)).ToList();
            InboxList.ItemsSource = items;
            InboxEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            InboxBadge.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            InboxBadgeText.Text = $"{items.Count} pending";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void UndoRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string opId) return;
        var be = await App.Backend;
        await be.Pipeline.UndoAsync(opId);
        await RefreshAsync();
    }

    private async void ResolveRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        var be = await App.Backend;
        await ((ISmartInboxRepository)be.Store).ResolveAsync(id);
        await RefreshAsync();
    }
}
