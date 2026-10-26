using FileMind.Application.Ports;
using FileMind.Application.UseCases;
using FileMind.Domain.Contracts;
using FileMind.WinUI.Controls;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;

namespace FileMind.WinUI.Views;

/// <summary>Organizer matched to mockup flows.js: 7-step stepper
/// (source → scan → analyze → plan → safety → execute → result) with a
/// dry-run toggle, streaming scan, per-file analysis, safety confirm,
/// live execute progress, cancel, and undo. All mutation goes through
/// OrganizerPipeline; the page only displays what the backend returns.
/// </summary>
public sealed partial class OrganizerPage : Page
{
    private enum Step { Source, Scan, Analyze, Plan, Safety, Execute, Result }
    private static readonly string[] StepNames =
        ["source", "scan", "analyze", "plan", "safety", "execute", "result"];

    private Step _step = Step.Source;
    private readonly List<FileCandidate> _candidates = [];
    private IReadOnlyList<AnalyzedFile> _analyzed = [];
    private ActionPlan? _plan;
    private SafetyResult? _safety;
    private CancellationTokenSource? _cts;
    private string? _lastOperationId;
    private string _source = "";

    // ---- row view models ------------------------------------------------

    private sealed record AnalyzeRow(
        string Name, string Kind, double Confidence, string ConfText,
        string Subject, string Project, string Decision, Brush DecisionBrush,
        string Reason, string Evidence);

    private sealed record PlanRow(
        FmOpState State, string Source, string Dest, string Why);

    private sealed record LogRow(string Text)
    {
        public override string ToString() => Text;
    }

    public OrganizerPage()
    {
        InitializeComponent();
        RenderStepBar();
        Loaded += async (_, _) =>
        {
            // default folder from Settings pre-fills the source picker
            var be = await App.Backend;
            var def = await ((FileMind.Application.Ports.ISettingsRepository)be.Store)
                .GetAsync(SettingsPage.DefaultFolderKey);
            if (def.Value is { Length: > 0 } d && SourceBox.Text.Length == 0)
                SourceBox.Text = d;
        };
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // "dryrun" nav param (Dashboard quick action) presets the toggle
        if (e.Parameter is string s && s == "dryrun") DryRunCheck.IsChecked = true;
    }

    // ---- step machinery -------------------------------------------------

    private void SetStep(Step s)
    {
        _step = s;
        StepSource.Visibility = s == Step.Source ? Visibility.Visible : Visibility.Collapsed;
        StepScan.Visibility = s == Step.Scan ? Visibility.Visible : Visibility.Collapsed;
        StepAnalyze.Visibility = s == Step.Analyze ? Visibility.Visible : Visibility.Collapsed;
        StepPlan.Visibility = s == Step.Plan ? Visibility.Visible : Visibility.Collapsed;
        StepSafety.Visibility = s == Step.Safety ? Visibility.Visible : Visibility.Collapsed;
        StepExecute.Visibility = s == Step.Execute ? Visibility.Visible : Visibility.Collapsed;
        StepResult.Visibility = s == Step.Result ? Visibility.Visible : Visibility.Collapsed;
        RenderStepBar();
    }

    private void RenderStepBar()
    {
        var idx = (int)_step;
        StepBar.ItemsSource = StepNames.Select((n, i) => new TextBlock
        {
            Text = $"{(i < idx ? "✓" : (i + 1).ToString())}  {n[0].ToString().ToUpper() + n[1..]}" +
                   (i < StepNames.Length - 1 ? "   ›" : ""),
            FontSize = 12,
            FontWeight = i == idx ? Microsoft.UI.Text.FontWeights.Bold
                                  : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[
                i <= idx ? "FmAccentBrush" : "FmTextSecondaryBrush"]
        }).ToList();
    }

    private void ShowError(FileMind.Domain.Errors.FileMindError err)
    {
        var friendly = FmErrorPresenter.Present(err);
        Error.Visibility = Visibility.Visible;
        Error.Title = friendly.Title;
        Error.Body = $"{friendly.Explanation} {friendly.Recovery}";
    }

    // ---- step 1: source -------------------------------------------------

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

    // ---- step 2: scan — streams rows like `filemind scan` ---------------

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var source = SourceBox.Text.Trim();
        if (source.Length == 0)
        {
            ShowError(FileMind.Domain.Errors.FileMindError.Validation("Choose a source folder first."));
            return;
        }
        _source = source;
        _candidates.Clear();
        ScanSummary.Text = "";
        AnalyzeNext.IsEnabled = false;
        Error.Visibility = Visibility.Collapsed;
        SetStep(Step.Scan);

        _cts = new CancellationTokenSource();
        var rows = new System.Collections.ObjectModel.ObservableCollection<LogRow>();
        ScanList.ItemsSource = rows;
        var be = await App.Backend;
        try
        {
            await foreach (var c in be.Pipeline.ScanAsync(source, recursive: true, _cts.Token))
            {
                _candidates.Add(c);
                rows.Add(new LogRow(
                    $"{c.DisplayName,-44} {DashboardPage.FormatBytes(c.SizeBytes),12}  {c.SourceScope}"));
                if (rows.Count % 20 == 0) ScanScroll.ChangeView(null, double.MaxValue, null);
            }
        }
        catch (OperationCanceledException) { SetStep(Step.Source); return; }
        catch (Exception ex)
        {
            ShowError(FileMind.Domain.Errors.FileMindError.Io(ex.Message));
            SetStep(Step.Source);
            return;
        }
        ScanScroll.ChangeView(null, double.MaxValue, null);

        if (_candidates.Count == 0)
        {
            ScanSummary.Text = "No files found in this source — pick a folder that contains files.";
            return;
        }
        ScanSummary.Text = $"-- {_candidates.Count} file(s)";
        AnalyzeNext.Content = $"Analyze {_candidates.Count} file(s)";
        AnalyzeNext.IsEnabled = true;
    }

    // ---- step 3: analyze — full table, tap a row for evidence -----------

    private async void AnalyzeNext_Click(object sender, RoutedEventArgs e)
    {
        Error.Visibility = Visibility.Collapsed;
        var be = await App.Backend;
        var analyzed = await be.Pipeline.AnalyzeAsync(_candidates);
        if (!analyzed.IsSuccess) { ShowError(analyzed.Error!); return; }
        _analyzed = analyzed.Value!;
        RenderAnalyze();
        SetStep(Step.Analyze);
    }

    private void RenderAnalyze()
    {
        var res = Microsoft.UI.Xaml.Application.Current.Resources;
        Brush BrushFor(DecisionKind k) => (Brush)res[k switch
        {
            DecisionKind.Organize => "FmStatusSuccessBrush",
            DecisionKind.Review => "FmStatusWarningBrush",
            _ => "FmTextSecondaryBrush"
        }];

        AnalyzeChips.Children.Clear();
        foreach (var g in _analyzed.GroupBy(a => a.Decision.Kind).OrderBy(g => g.Key.ToString()))
        {
            AnalyzeChips.Children.Add(new FmStatusChip
            {
                State = g.Key switch
                {
                    DecisionKind.Organize => FmOpState.Completed,
                    DecisionKind.Review => FmOpState.CompletedWithWarnings,
                    DecisionKind.Ignore => FmOpState.Cancelled,
                    _ => FmOpState.Offline
                }
            });
            AnalyzeChips.Children.Add(new TextBlock
            {
                Text = $"× {g.Count()}", FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)res["FmTextSecondaryBrush"]
            });
        }

        AnalyzeList.ItemsSource = _analyzed.Select(a => new AnalyzeRow(
            a.Candidate.DisplayName,
            a.Classification.FileKind.ToString(),
            a.Classification.Confidence,
            $"{a.Classification.Confidence:0}%",
            a.Classification.Subject ?? "-",
            a.Classification.Project ?? "-",
            a.Decision.Kind.ToString(),
            BrushFor(a.Decision.Kind),
            $"{a.Decision.Kind}: {string.Join("; ", a.Decision.Reasons)}" +
                (a.Rules.MatchedRuleIds.Count > 0
                    ? $"  ·  rules: {string.Join(", ", a.Rules.MatchedRuleIds)}" : ""),
            string.Join("\n", a.Classification.Evidence.Select(ev =>
                $"{ev.Kind,-22} {(ev.ScoreContribution > 0 ? "+" : "")}{ev.ScoreContribution:0}  {ev.Detail}")) +
                (a.Classification.Warnings.Count > 0
                    ? "\n" + string.Join("\n", a.Classification.Warnings.Select(w => $"warning: {w}")) : "")
        )).ToList();
    }

    // ---- step 4: plan ----------------------------------------------------

    private void PlanNext_Click(object sender, RoutedEventArgs e) => BuildPlan();

    private void BuildPlan()
    {
        var dest = DestBox.Text.Trim();
        // Mockup parity: organize into <source>\organized by default —
        // never scatter categories across the source root.
        var destRoot = dest.Length > 0 ? dest : System.IO.Path.Combine(_source, "organized");
        var plan = App.Backend.Result.Pipeline.Plan(
            _analyzed, dryRun: DryRunCheck.IsChecked == true,
            destinationRoot: destRoot, sourceRoot: _source,
            preserveTree: PreserveTree.IsChecked == true);
        if (!plan.IsSuccess) { ShowError(plan.Error!); return; }
        _plan = plan.Value!;
        RenderPlan();
        SetStep(Step.Plan);
    }

    private void RenderPlan()
    {
        var p = _plan!;
        PlanHeader.Text = $"plan {p.PlanId}  items={p.Items.Count}  " +
            $"review={p.RequiresReview}  dryRun={p.DryRun}";

        static FmOpState StateFor(OperationType op) => op switch
        {
            OperationType.Move => FmOpState.Running,
            OperationType.Copy => FmOpState.Queued,
            OperationType.Rename => FmOpState.Queued,
            OperationType.Ask => FmOpState.CompletedWithWarnings,
            OperationType.Ignore => FmOpState.Offline,
            _ => FmOpState.Ready
        };

        PlanList.ItemsSource = p.Items.Select(i => new PlanRow(
            StateFor(i.Operation),
            Rel(i.Source),
            i.Destination is { } d ? Rel(d) : "Smart Inbox",
            i.Reason)).ToList();

        var skipped = _analyzed.Count - p.Items.Count;
        PlanSkipped.Text = skipped > 0
            ? $"{skipped} file(s) untouched (NoOp/Ignore decisions)."
            : "";
    }

    private string Rel(string path) =>
        path.StartsWith(_source, StringComparison.OrdinalIgnoreCase)
            ? path[_source.Length..].TrimStart('\\', '/')
            : path;

    // ---- step 5: safety --------------------------------------------------

    private async void SafetyNext_Click(object sender, RoutedEventArgs e)
    {
        var be = await App.Backend;
        var safety = await be.Pipeline.DryRunAsync(_plan!);
        if (!safety.IsSuccess) { ShowError(safety.Error!); return; }
        _safety = safety.Value!;
        RenderSafety();
        SetStep(Step.Safety);
    }

    private void RenderSafety()
    {
        var s = _safety!;
        var res = Microsoft.UI.Xaml.Application.Current.Resources;
        SafetyBanners.Children.Clear();

        void Banner(string text, string brushKey)
        {
            SafetyBanners.Children.Add(new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(10),
                Background = (Brush)res["FmSurfacePrimaryBrush"],
                BorderBrush = (Brush)res[brushKey],
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = text, TextWrapping = TextWrapping.WrapWholeWords,
                    Foreground = (Brush)res["FmTextPrimaryBrush"]
                }
            });
        }

        foreach (var b in s.Blockers) Banner($"Blocked: {b}", "FmStatusErrorBrush");
        foreach (var w in s.Warnings) Banner($"Warning: {w}", "FmStatusWarningBrush");
        if (s.Allowed && s.Warnings.Count == 0)
            Banner("Pre-checks passed: source exists, destination is valid, " +
                "no protected paths, no unresolved conflicts.", "FmStatusSuccessBrush");

        ExecuteButton.IsEnabled = s.Allowed && _plan!.Items.Count > 0;
        ExecuteButton.Content = _plan!.DryRun ? "Execute dry run" : "Execute for real";
    }

    private void BackToPlan_Click(object sender, RoutedEventArgs e) => SetStep(Step.Plan);

    // ---- step 6: execute — live progress via OperationRecord polling -----

    private async void Execute_Click(object sender, RoutedEventArgs e)
    {
        var plan = _plan!;
        var safety = _safety!;
        if (!plan.DryRun && (safety.RequiredConfirmation is not null || safety.Warnings.Count > 0))
        {
            var ok = await new ContentDialog
            {
                Title = "Execute with warnings?",
                Content = string.Join("\n", safety.Warnings) +
                    "\n\nEvery move is verified and recorded — you can undo the whole operation afterwards.",
                PrimaryButtonText = $"Execute {plan.Items.Count(i => i.Operation == OperationType.Move)} move(s)",
                CloseButtonText = "Back",
                XamlRoot = XamlRoot,
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            if (ok != ContentDialogResult.Primary) return;
        }
        await RunExecuteAsync(plan);
    }

    private async Task RunExecuteAsync(ActionPlan plan)
    {
        SetStep(Step.Execute);
        RunLog.ItemsSource = new List<LogRow>();
        RunStage.State = FmOpState.Running;
        RunBar.Value = 0;
        RunCount.Text = $"0/{plan.Items.Count}";

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var be = await App.Backend;
        var store = (IOperationRepository)be.Store;
        RunLog.ItemsSource = new System.Collections.ObjectModel
            .ObservableCollection<LogRow>();

        // Live progress: TransactionEngine persists the record per item —
        // poll the repo for committed/total like the Activity page reads.
        var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var poller = Task.Run(async () =>
        {
            while (!pollCts.IsCancellationRequested)
            {
                try { await Task.Delay(250, pollCts.Token); }
                catch (OperationCanceledException) { return; }
                var live = (await store.GetIncompleteAsync()).Value
                    ?.FirstOrDefault(o => o.PlanId == plan.PlanId);
                if (live is null) continue;
                DispatcherQueue.TryEnqueue(() => RenderRunProgress(live));
            }
        });

        var record = await be.Pipeline.ExecuteAsync(plan, ct);
        await pollCts.CancelAsync();
        try { await poller; } catch (OperationCanceledException) { }

        if (!record.IsSuccess &&
            record.Error!.Code != FileMind.Domain.Errors.ErrorCode.Cancelled)
        {
            ShowError(record.Error);
            SetStep(Step.Plan);
            return;
        }

        if (record.IsSuccess) ShowResult(record.Value!, plan);
        else ShowCancelled(plan);
    }

    private void RenderRunProgress(OperationRecord live)
    {
        var done = live.Items.Count(i =>
            i.Status is OperationStatus.Committed or OperationStatus.Failed
                or OperationStatus.NeedsAttention);
        RunStage.State = DashboardPage.MapStatus(live.Status);
        RunBar.Value = live.Items.Count > 0 ? 100.0 * done / live.Items.Count : 0;
        RunCount.Text = $"{done}/{live.Items.Count}";
        var last = live.Items.TakeLast(5).Select(i =>
            new LogRow($"{BaseName(i.OriginalLocation)} — {i.Status}"));
        RunLog.ItemsSource = last.ToList();
    }

    private static string BaseName(string path)
    {
        var i = path.LastIndexOfAny(['\\', '/']);
        return i >= 0 ? path[(i + 1)..] : path;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void ShowCancelled(ActionPlan plan)
    {
        // Terminalize the record — a cancelled mid-run op must not linger
        // in GetIncomplete() as a zombie "active" operation.
        var be = await App.Backend;
        var store = (IOperationRepository)be.Store;
        var live = (await store.GetIncompleteAsync()).Value
            ?.FirstOrDefault(o => o.PlanId == plan.PlanId);
        if (live is not null)
        {
            await store.SaveAsync(live with
            {
                Status = OperationStatus.NeedsAttention,
                Error = FileMind.Domain.Errors.FileMindError.Validation(
                    "Cancelled partway through; some items may have moved already.")
            });
        }
        ShowError(FileMind.Domain.Errors.FileMindError.Validation(
            "Operation cancelled — partially-moved items are flagged for review in Activity."));
        SetStep(Step.Source);
    }

    // ---- step 7: result --------------------------------------------------

    private void ShowResult(OperationRecord record, ActionPlan plan)
    {
        _lastOperationId = record.OperationId;
        var done = record.Items.Count(i => i.Status == OperationStatus.Committed);
        var failed = record.Items.Count(i =>
            i.Status is OperationStatus.Failed or OperationStatus.NeedsAttention);

        if (plan.DryRun)
        {
            ResultChip.State = FmOpState.Completed;
            ResultText.Text = "Dry run complete — the full pipeline ran; nothing was moved.";
            ResultDetail.Text = $"{done} item(s) verified in the dry run.";
            RunForReal.Visibility = Visibility.Visible;
            UndoButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ResultChip.State = DashboardPage.MapStatus(record.Status);
            ResultText.Text = $"{done} item(s) moved" +
                (failed > 0 ? $", {failed} need attention — see Activity." : ".");
            ResultDetail.Text = "Recorded in Activity with an undo recipe. " +
                "Low-confidence files wait in the Smart Inbox.";
            RunForReal.Visibility = Visibility.Collapsed;
            UndoButton.Visibility = done > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        SetStep(Step.Result);
    }

    private void RunForReal_Click(object sender, RoutedEventArgs e)
    {
        DryRunCheck.IsChecked = false;
        BuildPlan();
        if (_plan is not null)
        {
            SafetyNext_Click(sender, e);
        }
    }

    private void StartOver_Click(object sender, RoutedEventArgs e)
    {
        _plan = null; _safety = null; _analyzed = []; _candidates.Clear();
        SetStep(Step.Source);
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOperationId is null) return;
        var be = await App.Backend;
        var undone = await be.Pipeline.UndoAsync(_lastOperationId);
        ResultChip.State = undone.IsSuccess ? FmOpState.Cancelled : FmOpState.Failed;
        ResultText.Text = undone.IsSuccess
            ? "Operation reversed — files are back where they were."
            : $"Undo failed: {undone.Error!.Message}";
        UndoButton.Visibility = Visibility.Collapsed;
    }
}
