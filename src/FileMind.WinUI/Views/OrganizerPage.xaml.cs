using FileMind.Domain.Contracts;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace FileMind.WinUI.Views;

/// <summary>
/// Operation flow per ux/01: select → pre-check → confirm → execute →
/// result. All mutation goes through OrganizerPipeline; the page only
/// displays what the backend returns.
/// </summary>
public sealed partial class OrganizerPage : Page
{
    private ActionPlan? _plan;
    private string? _lastOperationId;

    private sealed record PlanRow(string Label, string Detail);

    public OrganizerPage() => InitializeComponent();

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow));
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync() is { } p) SourceBox.Text = p;
    }

    private async void BrowseDest_Click(object sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync() is { } p) DestBox.Text = p;
    }

    private void ShowBusy(string title, string body)
    {
        Busy.Visibility = Visibility.Visible;
        Busy.Title = title; Busy.Body = body;
        Error.Visibility = Visibility.Collapsed;
    }

    private void ShowError(FileMind.Domain.Errors.FileMindError err)
    {
        var friendly = FmErrorPresenter.Present(err);
        Busy.Visibility = Visibility.Collapsed;
        Error.Visibility = Visibility.Visible;
        Error.Title = friendly.Title;
        Error.Body = $"{friendly.Explanation} {friendly.Recovery}";
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var source = SourceBox.Text.Trim();
        var dest = DestBox.Text.Trim();
        if (source.Length == 0)
        {
            ShowError(FileMind.Domain.Errors.FileMindError.Validation("Choose a source folder first."));
            return;
        }

        ShowBusy("Scanning", "Scanning and classifying files…");
        PlanCard.Visibility = Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Collapsed;

        var be = await App.Backend;
        var candidates = new List<FileCandidate>();
        try
        {
            await foreach (var c in be.Pipeline.ScanAsync(source, recursive: true))
                candidates.Add(c);
        }
        catch (Exception ex)
        {
            ShowError(FileMind.Domain.Errors.FileMindError.Io(ex.Message));
            return;
        }

        if (candidates.Count == 0)
        {
            Busy.Visibility = Visibility.Collapsed;
            ShowError(FileMind.Domain.Errors.FileMindError.NotFound(
                "No files found in that folder."));
            return;
        }

        var analyzed = await be.Pipeline.AnalyzeAsync(candidates);
        if (!analyzed.IsSuccess) { ShowError(analyzed.Error!); return; }

        var destRoot = dest.Length > 0 ? dest : null;
        var plan = be.Pipeline.Plan(analyzed.Value!, dryRun: false,
            destinationRoot: destRoot, sourceRoot: source,
            preserveTree: PreserveTree.IsChecked == true);
        if (!plan.IsSuccess) { ShowError(plan.Error!); return; }

        _plan = plan.Value!;
        var safety = await be.Pipeline.DryRunAsync(_plan);
        if (!safety.IsSuccess) { ShowError(safety.Error!); return; }

        Busy.Visibility = Visibility.Collapsed;
        PlanCard.Visibility = Visibility.Visible;

        var mutating = _plan.Items.Where(i => i.Operation is not OperationType.Tag
            and not OperationType.Ignore and not OperationType.Notify
            and not OperationType.Ask).ToList();
        PlanSummary.Text =
            $"{_plan.Items.Count} planned action(s), {mutating.Count} mutating. " +
            $"{(safety.Value!.Allowed ? "Safety check passed." : "Safety check has warnings — review before executing.")}" +
            (_plan.RequiresReview ? " Some items require review." : "");
        PlanList.ItemsSource = _plan.Items.Take(200).Select(i => new PlanRow(
            $"{i.Operation}  {i.Source}" + (i.Destination is { } d ? $"  →  {d}" : ""),
            i.Reason)).ToList();
        ExecuteButton.IsEnabled = mutating.Count > 0 && safety.Value.Allowed;
    }

    private async void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_plan is null) return;
        ShowBusy("Executing", "Applying the plan — verifying each item…");

        var be = await App.Backend;
        var record = await be.Pipeline.ExecuteAsync(_plan);
        Busy.Visibility = Visibility.Collapsed;
        ResultCard.Visibility = Visibility.Visible;

        if (!record.IsSuccess)
        {
            ResultChip.State = Controls.FmOpState.Failed;
            var friendly = FmErrorPresenter.Present(record.Error!);
            ResultText.Text = $"{friendly.Title} {friendly.Explanation}";
            return;
        }

        var op = record.Value!;
        _lastOperationId = op.OperationId;
        ResultChip.State = DashboardPage.MapStatus(op.Status);
        var done = op.Items.Count(i => i.Status is OperationStatus.Committed);
        var failed = op.Items.Count(i => i.Status is OperationStatus.Failed
            or OperationStatus.NeedsAttention);
        ResultText.Text = $"{done} item(s) completed" +
            (failed > 0 ? $", {failed} need attention — see Activity." : ".");
        UndoButton.Visibility = done > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOperationId is null) return;
        ShowBusy("Undoing", "Reversing the last operation…");
        var be = await App.Backend;
        var undone = await be.Pipeline.UndoAsync(_lastOperationId);
        Busy.Visibility = Visibility.Collapsed;
        ResultChip.State = undone.IsSuccess ? Controls.FmOpState.Cancelled : Controls.FmOpState.Failed;
        ResultText.Text = undone.IsSuccess
            ? "Operation reversed — files are back where they were."
            : $"Undo failed: {undone.Error!.Message}";
    }
}
