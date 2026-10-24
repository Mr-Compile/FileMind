using FileMind.Application.Search;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

/// <summary>Search — real FTS5 + structured filters via SearchUseCase.</summary>
public sealed partial class SearchPage : Page
{
    private sealed record Row(string DisplayName, string Path, string Meta);

    public SearchPage()
    {
        InitializeComponent();
        KindFilter.ItemsSource = new[]
        {
            "(all kinds)", "Document", "Image", "Video", "Audio", "Archive",
            "Code", "Spreadsheet", "Presentation", "Executable", "Font", "Other"
        };
        KindFilter.SelectedIndex = 0;
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

    private async void Search_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        var be = await App.Backend;
        var kind = KindFilter.SelectedIndex > 0 ? KindFilter.SelectedItem as string : null;
        var result = await be.Search.RunAsync(new SearchQuery(
            Text: QueryBox.Text.Trim().Length > 0 ? QueryBox.Text.Trim() : null,
            Kind: kind, Limit: 100));
        if (!result.IsSuccess)
        {
            Summary.Text = $"Search failed: {result.Error!.Message}";
            return;
        }
        var rows = result.Value!.Select(r => new Row(r.DisplayName, r.Path,
            $"{r.Kind} · {DashboardPage.FormatBytes(r.SizeBytes)} · {r.ModifiedAt.LocalDateTime:d}")).ToList();
        ResultList.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Summary.Text = $"{rows.Count} result(s)";
    }
}
