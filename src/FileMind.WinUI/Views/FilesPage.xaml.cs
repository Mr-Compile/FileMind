using FileMind.Application.Ports;
using FileMind.Application.UseCases;
using FileMind.Domain;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace FileMind.WinUI.Views;

/// <summary>Files screen matched to the mockup: scope/kind chips, headered table,
/// inspector with a real Analyze decision, and a selection bar.</summary>
public sealed partial class FilesPage : Page
{
    private sealed record FileRow(string Name, string Kind, string Size, string Modified,
        string Decision, Brush DecisionBrush, FileCandidate Candidate)
    {
        public override string ToString() => $"{Name} — {Kind} — {Size} — {Decision} — {Path_}";
        private string Path_ => Candidate.SourceUri;
    }

    private IReadOnlyList<FileCandidate> _all = [];
    private int _gen;
    private string _scopePrefix = "";         // "" = all scopes
    private string _kind = "All";
    // decision cache keyed by path — ApplyFilters and the inspector share
    // it so AnalyzeAsync doesn't run twice for the same file
    private readonly Dictionary<string, AnalyzedFile> _decisions =
        new(StringComparer.OrdinalIgnoreCase);

    public FilesPage()
    {
        InitializeComponent();
        SortBox.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadIndexAsync();
    }

    private void GoTo(string tag)
    {
        if (App.MainAppWindow?.Content is Shell shell) shell.NavigateTo(tag);
    }

    private async Task LoadIndexAsync()
    {
        Spinner.Visibility = Visibility.Visible;
        var be = await App.Backend;
        var all = await ((IFileIndexRepository)be.Store).GetAllAsync();
        Spinner.Visibility = Visibility.Collapsed;

        if (!all.IsSuccess)
        {
            ItemCount.Text = $"Index unavailable: {all.Error!.Message}";
            Empty.Visibility = Visibility.Visible;
            return;
        }
        _all = all.Value!;
        RescanBtn.IsEnabled = _all.Count > 0;
        _decisions.Clear();
        BuildScopeChips();
        BuildKindChips();
        ApplyFilters();
    }

    /// <summary>Scope chips = distinct first-level parent folders in the index
    /// (mockup scope chips: Downloads / Documents / Desktop).</summary>
    private void BuildScopeChips()
    {
        ScopeChips.Children.Clear();
        ScopeChips.Children.Add(MakeChip("All", _scopePrefix.Length == 0));
        var scopes = _all
            .Select(c => ParentDir(c.SourceUri))
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .Take(8);
        foreach (var p in scopes)
            ScopeChips.Children.Add(MakeChip(FolderName(p), _scopePrefix == p, p));
    }

    private void BuildKindChips()
    {
        var kinds = new List<string> { "All" };
        kinds.AddRange(_all
            .Select(c => FileKindMap.FromExtension(c.Extension).ToString())
            .Distinct()
            .Order());
        KindChips.ItemsSource = kinds
            .Select(k => MakeChip(k, _kind == k, k))
            .ToList();
    }

    private ToggleButton MakeChip(string label, bool on, string? value = null)
    {
        var chip = new ToggleButton
        {
            Content = label,
            IsChecked = on,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmChipToggle"],
            Tag = value ?? ""
        };
        chip.Click += (_, _) =>
        {
            var v = (string)chip.Tag;
            if (KindChips.ItemsSource is IEnumerable<ToggleButton> kindChips &&
                kindChips.Contains(chip))
            {
                _kind = v.Length == 0 ? "All" : v;
                foreach (var c in kindChips) c.IsChecked = c == chip;
            }
            else
            {
                _scopePrefix = v;
                foreach (var c in ScopeChips.Children.OfType<ToggleButton>()) c.IsChecked = c == chip;
            }
            ApplyFilters();
        };
        return chip;
    }

    private async void ApplyFilters()
    {
        if (FileList is null || ItemCount is null || Empty is null) return;
        var gen = ++_gen;
        var nameFilter = FilterBox.Text.Trim();
        var scoped = _scopePrefix.Length == 0 ? _all
            : _all.Where(c => c.SourceUri.StartsWith(_scopePrefix, StringComparison.OrdinalIgnoreCase)).ToList();

        var q = scoped
            .Where(c => (nameFilter.Length == 0 ||
                c.DisplayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                && (_kind == "All" || FileKindMap.FromExtension(c.Extension).ToString() == _kind));
        var sort = (SortBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "name";
        q = sort switch
        {
            "size" => q.OrderByDescending(c => c.SizeBytes),
            "modified" => q.OrderByDescending(c => c.ModifiedAt),
            "kind" => q.OrderBy(c => FileKindMap.FromExtension(c.Extension).ToString())
                       .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => q.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase),
        };

        var muted = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"];
        var rows = q.Take(1000)
            .Select(c => new FileRow(c.DisplayName,
                FileKindMap.FromExtension(c.Extension).ToString(),
                DashboardPage.FormatBytes(c.SizeBytes),
                c.ModifiedAt.LocalDateTime.ToString("g"), "—", muted, c))
            .ToList();
        FileList.ItemsSource = rows;
        Empty.Title = nameFilter.Length > 0 || _kind != "All"
            ? "Nothing matches the current filters." : "Nothing indexed";
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemCount.Text = $"{rows.Count} of {scoped.Count} item(s)";
        ClearBtn.Visibility = nameFilter.Length > 0 || _kind != "All"
            ? Visibility.Visible : Visibility.Collapsed;

        // mockup Decision column: real pipeline decisions for the visible set,
        // served from the cache where possible
        var toAnalyze = rows.Take(200)
            .Select(r => r.Candidate)
            .Where(c => !_decisions.ContainsKey(c.SourceUri))
            .ToList();
        if (toAnalyze.Count > 0)
        {
            var be = await App.Backend;
            var analyzed = await be.Pipeline.AnalyzeAsync(toAnalyze);
            if (gen != _gen) return;
            if (analyzed.IsSuccess)
                foreach (var a in analyzed.Value!)
                    _decisions[a.Candidate.SourceUri] = a;
        }
        FileList.ItemsSource = rows.Select(r =>
            _decisions.TryGetValue(r.Candidate.SourceUri, out var a)
                ? r with { Decision = a.Decision.Kind.ToString(), DecisionBrush = DecisionBrushFor(a.Decision.Kind) }
                : r).ToList();
    }

    private static Brush DecisionBrushFor(DecisionKind kind)
    {
        var res = Microsoft.UI.Xaml.Application.Current.Resources;
        var key = kind switch
        {
            DecisionKind.Organize => "FmStatusSuccessBrush",
            DecisionKind.Review => "FmStatusWarningBrush",
            _ => "FmTextSecondaryBrush"
        };
        return (Brush)res[key];
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        FilterBox.Text = "";
        _kind = "All";
        BuildKindChips();
        ApplyFilters();
    }

    private void ClearSel_Click(object sender, RoutedEventArgs e)
        => FileList.SelectedItems.Clear();

    private async void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelBar.Visibility = FileList.SelectedItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SelCount.Text = $"{FileList.SelectedItems.Count} selected";

        if (FileList.SelectedItem is not FileRow row)
        {
            InsHint.Visibility = Visibility.Visible;
            InsBody.Visibility = Visibility.Collapsed;
            InsName.Text = "";
            return;
        }
        var c = row.Candidate;
        InsName.Text = c.DisplayName;
        InsPath.Text = c.SourceUri;
        InsMeta.Text =
            $"{FileKindMap.FromExtension(c.Extension)} · {c.Extension} · {DashboardPage.FormatBytes(c.SizeBytes)}" +
            (c.MimeType is { } m ? $" · {m}" : "") +
            $"\nModified {c.ModifiedAt.LocalDateTime:g} · Created {c.CreatedAt.LocalDateTime:g}";
        InsHash.Text = c.ContentHash is { } h
            ? $"SHA-256 {h[..Math.Min(16, h.Length)]}… ({c.HashState})"
            : $"Hash: {c.HashState}";
        InsHint.Visibility = Visibility.Collapsed;
        InsBody.Visibility = Visibility.Visible;

        InsConfRow.Visibility = Visibility.Collapsed;
        InsEvidenceBtn.Visibility = Visibility.Collapsed;
        InsEvidence.Visibility = Visibility.Collapsed;
        InsRenameBtn.Visibility = Visibility.Visible;

        // real decision from the live pipeline (mockup inspector shows
        // decision + confBar + expandable evidence)
        InsDecision.State = FmOpState.Queued;
        InsReason.Text = "Analyzing…";
        if (!_decisions.TryGetValue(c.SourceUri, out var a))
        {
            var be = await App.Backend;
            var analyzed = await be.Pipeline.AnalyzeAsync([c]);
            if (analyzed.IsSuccess && analyzed.Value!.Count > 0)
            {
                a = analyzed.Value[0];
                _decisions[c.SourceUri] = a;
            }
        }
        if (a is not null)
        {
            InsDecision.State = a.Decision.Kind switch
            {
                DecisionKind.Organize => FmOpState.Completed,
                DecisionKind.Review => FmOpState.CompletedWithWarnings,
                DecisionKind.Ignore => FmOpState.Cancelled,
                _ => FmOpState.Queued
            };
            var subj = a.Classification.Subject is { Length: > 0 } s ? $" · {s}" : "";
            InsReason.Text =
                $"{a.Decision.Kind} — {a.Classification.Explanation}{subj}. " +
                string.Join(" ", a.Decision.Reasons.Take(2));
            InsConfBar.Value = a.Classification.Confidence;
            InsConfText.Text = $"{a.Classification.Confidence:0}%";
            InsConfRow.Visibility = Visibility.Visible;
            if (a.Classification.Evidence.Count > 0)
            {
                InsEvidence.Text = string.Join("\n", a.Classification.Evidence.Select(ev =>
                    $"{ev.Kind,-22} {(ev.ScoreContribution > 0 ? "+" : "")}{ev.ScoreContribution:0}  {ev.Detail}"));
                InsEvidenceBtn.Visibility = Visibility.Visible;
            }
        }
        else
        {
            InsReason.Text = "Could not analyze this file.";
        }
    }

    private void InsEvidence_Click(object sender, RoutedEventArgs e)
        => InsEvidence.Visibility = InsEvidence.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not FileRow row) return;
        var path = row.Candidate.SourceUri;
        if (path.StartsWith("file:///", StringComparison.Ordinal))
            path = Uri.UnescapeDataString(new Uri(path).LocalPath);
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not FileRow row) return;
        var pkg = new DataPackage();
        pkg.SetText(row.Candidate.SourceUri);
        Clipboard.SetContent(pkg);
    }

    private void Organize_Click(object sender, RoutedEventArgs e) => GoTo("organizer");

    private async void Index_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        Spinner.Visibility = Visibility.Visible;
        ItemCount.Text = $"Indexing {folder.Path}…";
        var be = await App.Backend;
        var candidates = new List<FileCandidate>();
        await foreach (var c in be.Pipeline.ScanAsync(folder.Path, recursive: true))
            candidates.Add(c);
        var result = await be.Pipeline.IndexAsync(candidates);
        ItemCount.Text = result.IsSuccess
            ? $"Indexed {result.Value!.Indexed} file(s) ({result.Value.Hashed} hashed)."
            : $"Indexing failed: {result.Error!.Message}";
        await LoadIndexAsync();
    }

    /// <summary>Real rename via the backend: a one-item ActionPlan executed
    /// through the transaction engine, so it is safety-checked, verified,
    /// recorded in Activity and undoable like any organizer op.</summary>
    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not FileRow row) return;
        var c = row.Candidate;
        var nameBox = new TextBox { Text = c.DisplayName, Width = 320 };
        var dlg = new ContentDialog
        {
            Title = "Rename file",
            Content = nameBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var newName = nameBox.Text.Trim();
        if (newName.Length == 0 || newName == c.DisplayName) return;

        var be = await App.Backend;
        var plan = new ActionPlan
        {
            PlanId = $"rename-{Guid.NewGuid():N}",
            CreatedAt = DateTimeOffset.Now,
            SourceDecision = "manual rename from Files",
            Items =
            [
                new ActionItem
                {
                    Operation = OperationType.Rename,
                    Source = c.SourceUri,
                    Destination = newName,
                    Reason = "User-initiated rename from the Files inspector."
                }
            ],
            DryRun = false,
            ReversibleEstimate = true
        };
        var record = await be.Pipeline.ExecuteAsync(plan);
        if (!record.IsSuccess)
        {
            InsReason.Text = $"Rename failed: {record.Error!.Message}";
            return;
        }
        // keep the index + decision cache truthful for the new path
        _decisions.Remove(c.SourceUri);
        var renamed = record.Value!.Items.FirstOrDefault()?.ResultingLocation;
        await RescanDirAsync(ParentDir(c.SourceUri));
        InsReason.Text = renamed is { } r ? $"Renamed → {r}" : "Renamed.";
    }

    /// <summary>Re-enumerates a scope folder and merges it into the index —
    /// mockup's rescan affordance on the selected scope.</summary>
    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        var root = _scopePrefix.Length > 0 ? _scopePrefix
            : _all.Select(c => ParentDir(c.SourceUri))
                  .OrderBy(p => p.Length).FirstOrDefault();
        if (root is null || root.Length == 0) return;
        await RescanDirAsync(root);
    }

    private async Task RescanDirAsync(string dirUri)
    {
        var path = dirUri;
        if (path.StartsWith("file:///", StringComparison.Ordinal))
            path = Uri.UnescapeDataString(new Uri(path).LocalPath);
        ItemCount.Text = $"Rescanning {path}…";
        var be = await App.Backend;
        var candidates = new List<FileCandidate>();
        await foreach (var cand in be.Pipeline.ScanAsync(path, recursive: true))
            candidates.Add(cand);
        var result = await be.Pipeline.IndexAsync(candidates);
        ItemCount.Text = result.IsSuccess
            ? $"Rescanned — indexed {result.Value!.Indexed} file(s)."
            : $"Rescan failed: {result.Error!.Message}";
        await LoadIndexAsync();
    }

    private static string ParentDir(string uri)
    {
        var i = uri.LastIndexOf('/');
        return i > 0 ? uri[..i] : "";
    }

    private static string FolderName(string dir)
    {
        var name = dir.TrimEnd('/');
        var i = name.LastIndexOf('/');
        return i >= 0 ? name[(i + 1)..] : name;
    }
}
