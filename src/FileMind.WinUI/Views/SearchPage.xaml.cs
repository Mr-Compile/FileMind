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

    private string _kind = "All";
    private int _sizeIdx;

    public SearchPage()
    {
        InitializeComponent();
        KindChips.ItemsSource = Kinds.Select(k => Chip(k, _kind == k,
            () => { _kind = k; RefreshChips(); _ = RunAsync(); })).ToList();
        SizeChips.ItemsSource = Sizes.Select((s, i) => Chip(s.Label, i == _sizeIdx,
            () => { _sizeIdx = i; RefreshChips(); _ = RunAsync(); })).ToList();
    }

    private void RefreshChips()
    {
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
        var rows = result.Value!
            .Where(r => r.SizeBytes >= lo && r.SizeBytes < hi)
            .Select(r => new Row(r.DisplayName, r.Kind,
                DashboardPage.FormatBytes(r.SizeBytes),
                r.ModifiedAt.LocalDateTime.ToString("d"), r.Path))
            .ToList();
        ResultList.ItemsSource = rows;
        var searched = QueryBox.Text.Trim().Length > 0 || _kind != "All" || _sizeIdx != 0;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Empty.Title = searched ? "Nothing matches" : "Search your files";
        Empty.Body = searched
            ? "No indexed file matches those filters."
            : "Type a name, or filter by kind and size — results come from the local FTS5 index.";
        Summary.Text = searched ? $"{rows.Count} result(s)" : "";
    }
}
