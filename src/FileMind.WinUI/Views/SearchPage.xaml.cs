using FileMind.Application.Search;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

/// <summary>Search matched to the mockup: input + kind/size chips + result table
/// with Location — real FTS5 + structured filters via SearchUseCase.</summary>
public sealed partial class SearchPage : Page
{
    private sealed record Row(string DisplayName, string Kind, string Size,
        string Modified, string Path)
    {
        public override string ToString() => $"{DisplayName} — {Kind} — {Size} — {Path}";
    }

    private static readonly (string Label, long Lo, long Hi)[] Sizes =
    [
        ("Any", 0, long.MaxValue),
        ("small <1 MB", 0, 1_000_000),
        ("medium", 1_000_000, 100_000_000),
        ("large >100 MB", 100_000_000, long.MaxValue),
    ];

    private static readonly string[] Kinds =
        ["All", "Document", "Image", "Video", "Audio", "Archive",
         "Code", "Spreadsheet", "Presentation", "Executable", "Font", "Other"];

    // mockup scope chips: all / downloads / documents / desktop — mapped to
    // the real Windows well-known folders
    private static readonly (string Label, string? Folder)[] Scopes =
    [
        ("Any", null),
        ("Downloads", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"),
        ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
        ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
    ];

    private string _kind = "All";
    private int _sizeIdx;
    private int _scopeIdx;

    public SearchPage()
    {
        InitializeComponent();
        ScopeChips.ItemsSource = Scopes.Select((s, i) => Chip(s.Label, i == _scopeIdx,
            () => { _scopeIdx = i; RefreshChips(); _ = RunAsync(); })).ToList();
        KindChips.ItemsSource = Kinds.Select(k => Chip(k, _kind == k,
            () => { _kind = k; RefreshChips(); _ = RunAsync(); })).ToList();
        SizeChips.ItemsSource = Sizes.Select((s, i) => Chip(s.Label, i == _sizeIdx,
            () => { _sizeIdx = i; RefreshChips(); _ = RunAsync(); })).ToList();
    }

    private void RefreshChips()
    {
        if (ScopeChips.ItemsSource is IEnumerable<ToggleButton> oc)
            foreach (var c in oc) c.IsChecked = (int)c.Tag == _scopeIdx;
        if (KindChips.ItemsSource is IEnumerable<ToggleButton> kc)
            foreach (var c in kc) c.IsChecked = (string)c.Tag == _kind;
        if (SizeChips.ItemsSource is IEnumerable<ToggleButton> sc)
            foreach (var c in sc) c.IsChecked = (int)c.Tag == _sizeIdx;
    }

    private ToggleButton Chip(string label, bool on, Action act)
    {
        var chip = new ToggleButton
        {
            Content = label, IsChecked = on,
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmChipToggle"],
        };
        chip.Click += (_, _) =>
        {
            if (Kinds.Contains(label)) chip.Tag = label;
            else if (Scopes.Any(s => s.Label == label))
                chip.Tag = Array.FindIndex(Scopes, s => s.Label == label);
            else chip.Tag = Array.FindIndex(Sizes, s => s.Label == label);
            act();
        };
        return chip;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string { Length: > 0 } q)
        {
            QueryBox.Text = q;
            _ = RunAsync();
        }
    }

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) _ = RunAsync();
    }

    private async Task RunAsync()
    {
        var be = await App.Backend;
        var result = await be.Search.RunAsync(new SearchQuery(
            Text: QueryBox.Text.Trim().Length > 0 ? QueryBox.Text.Trim() : null,
            Kind: _kind == "All" ? null : _kind,
            Limit: 200));
        if (!result.IsSuccess)
        {
            Summary.Text = $"Search failed: {result.Error!.Message}";
            return;
        }
        var (lo, hi) = (Sizes[_sizeIdx].Lo, Sizes[_sizeIdx].Hi);
        var scope = Scopes[_scopeIdx].Folder;
        var rows = result.Value!
            .Where(r => r.SizeBytes >= lo && r.SizeBytes < hi
                && (scope is null || r.Path.StartsWith(scope, StringComparison.OrdinalIgnoreCase)))
            .Select(r => new Row(r.DisplayName, r.Kind,
                DashboardPage.FormatBytes(r.SizeBytes),
                r.ModifiedAt.LocalDateTime.ToString("d"), r.Path))
            .ToList();
        ResultList.ItemsSource = rows;
        var searched = QueryBox.Text.Trim().Length > 0 || _kind != "All"
            || _sizeIdx != 0 || _scopeIdx != 0;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Empty.Title = searched ? "Nothing matches" : "Search your files";
        Empty.Body = searched
            ? "No indexed file matches those filters in this scope."
            : "Type a name, or filter by kind, scope and size — results come from the local FTS5 index.";
        ClearAllBtn.Visibility = searched && rows.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        Summary.Text = searched ? $"{rows.Count} result(s)" : "";
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Text = "";
        _kind = "All"; _sizeIdx = 0; _scopeIdx = 0;
        RefreshChips();
        _ = RunAsync();
    }

    private void Result_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultList.SelectedItem is not Row row)
        {
            InsHint.Visibility = Visibility.Visible;
            InsBody.Visibility = Visibility.Collapsed;
            InsName.Text = "";
            return;
        }
        InsName.Text = row.DisplayName;
        InsMeta.Text = $"{row.Kind} · {row.Size} · modified {row.Modified}";
        InsPath.Text = row.Path;
        InsHint.Visibility = Visibility.Collapsed;
        InsBody.Visibility = Visibility.Visible;
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not Row row) return;
        var path = row.Path.StartsWith("file:///", StringComparison.Ordinal)
            ? Uri.UnescapeDataString(new Uri(row.Path).LocalPath) : row.Path;
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (ResultList.SelectedItem is not Row row) return;
        var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
        pkg.SetText(row.Path);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
    }
}
