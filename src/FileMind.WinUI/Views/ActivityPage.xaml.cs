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

    private sealed record InboxRow(
        string Id, string Name, string Path, string Reason,
        string Suggested, Visibility SuggestedVisibility)
    {
        public override string ToString() => $"{Name} — {Reason}";
    }

    private sealed record ActiveRow(FmOpState State, string OpId, double Progress, string Summary);

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

        // in-flight operations, like the mockup's "running" cards — from
        // the same incomplete-records feed the organizer polls for progress
        var incomplete = await ((IOperationRepository)be.Store).GetIncompleteAsync();
        if (incomplete.IsSuccess)
        {
            var act = incomplete.Value!.Select(o =>
            {
                var done = o.Items.Count(i => i.Status
                    is FileMind.Domain.Contracts.OperationStatus.Committed
                    or FileMind.Domain.Contracts.OperationStatus.Failed
                    or FileMind.Domain.Contracts.OperationStatus.NeedsAttention);
                return new ActiveRow(
                    DashboardPage.MapStatus(o.Status),
                    o.OperationId[..Math.Min(12, o.OperationId.Length)] + "…",
                    o.Items.Count > 0 ? 100.0 * done / o.Items.Count : 0,
                    $"{done}/{o.Items.Count} items settled");
            }).ToList();
            ActiveList.ItemsSource = act;
            ActiveSection.Visibility = act.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

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
            var items = inbox.Value!.Select(i =>
            {
                var dest = SuggestedDest(i);
                return new InboxRow(
                    i.Id,
                    System.IO.Path.GetFileName(i.Path),
                    i.Path,
                    i.Reason,
                    dest is { } d ? $"→ {d}" : "",
                    dest is not null ? Visibility.Visible : Visibility.Collapsed);
            }).ToList();
            InboxList.ItemsSource = items;
            InboxEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            InboxBadge.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            InboxBadgeText.Text = $"{items.Count} pending";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    /// <summary>Suggested destination for an inbox item, following the same
    /// convention as the organizer: organized\&lt;category-or-kind&gt;\ under
    /// the file's parent directory. Null when classification is too weak to
    /// suggest anything honest.</summary>
    private static string? SuggestedDest(FileMind.Application.Ports.SmartInboxItem i)
    {
        var bucket = i.Classification.Category is { Length: > 0 } cat ? cat
            : i.Classification.Subject is { Length: > 0 } sub ? sub
            : i.Classification.FileKind is FileMind.Domain.Contracts.FileKind.Other
                ? null : i.Classification.FileKind.ToString();
        if (bucket is null) return null;
        var dir = System.IO.Path.GetDirectoryName(i.Path);
        return dir is null ? null
            : System.IO.Path.Combine(dir, "organized", bucket);
    }

    private async void UndoRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string opId) return;
        // mockup requires confirmation before reversing real file moves
        var ok = await new ContentDialog
        {
            Title = "Undo this operation?",
            Content = "Files move back to their original locations. " +
                "Anything already changed by hand is left alone and flagged.",
            PrimaryButtonText = "Undo",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Close
        }.ShowAsync();
        if (ok != ContentDialogResult.Primary) return;
        var be = await App.Backend;
        await be.Pipeline.UndoAsync(opId);
        await RefreshAsync();
    }

    /// <summary>"Send to organized" — executes a real move for an inbox
    /// item to its suggested destination through the pipeline, then marks
    /// the inbox entry resolved.</summary>
    private async void SendSuggested_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        var be = await App.Backend;
        var store = (ISmartInboxRepository)be.Store;
        var open = await store.GetOpenAsync();
        var item = open.Value?.FirstOrDefault(i => i.Id == id);
        var destDir = item is null ? null : SuggestedDest(item);
        if (item is null || destDir is null) return;

        var dest = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(item.Path));
        var plan = new FileMind.Domain.Contracts.ActionPlan
        {
            PlanId = $"inbox-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.Now,
            SourceDecision = "smart inbox send-to-suggested",
            Items =
            [
                new FileMind.Domain.Contracts.ActionItem
                {
                    Operation = FileMind.Domain.Contracts.OperationType.Move,
                    Source = item.Path,
                    Destination = dest,
                    Reason = $"Smart Inbox: moved to suggested destination from {item.Classification.FileKind} classification."
                }
            ],
            DryRun = false,
            ReversibleEstimate = true
        };
        var record = await be.Pipeline.ExecuteAsync(plan);
        if (record.IsSuccess)
            await store.ResolveAsync(id);
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
