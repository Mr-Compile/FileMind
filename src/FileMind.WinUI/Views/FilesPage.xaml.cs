using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace FileMind.WinUI.Views;

/// <summary>Files screen — real file index (IFileIndexRepository),
/// name/extension filters, multi-select via ListView, index-folder action.</summary>
public sealed partial class FilesPage : Page
{
    private sealed record FileRow(string Name, string Path, string Size, string Modified);
    private IReadOnlyList<FileCandidate> _all = [];

    public FilesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadIndexAsync();
    }

    private async Task LoadIndexAsync()
    {
        Spinner.Visibility = Visibility.Visible;
        var be = await App.Backend;
        var all = await ((IFileIndexRepository)be.Store).GetAllAsync();
        Spinner.Visibility = Visibility.Collapsed;

        if (!all.IsSuccess)
        {
            Summary.Text = $"Index unavailable: {all.Error!.Message}";
            return;
        }
        _all = all.Value!;
        ExtFilter.Items.Clear();
        ExtFilter.Items.Add(new ComboBoxItem { Content = "(all)", Tag = "" });
        foreach (var ext in _all.Select(c => c.Extension).Distinct(StringComparer.OrdinalIgnoreCase).Order())
            ExtFilter.Items.Add(new ComboBoxItem { Content = ext, Tag = ext });
        ExtFilter.SelectedIndex = 0;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var nameFilter = FilterBox.Text.Trim();
        var ext = (ExtFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var rows = _all
            .Where(c => (nameFilter.Length == 0 ||
                c.DisplayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                && (ext.Length == 0 || c.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(1000)
            .Select(c => new FileRow(c.DisplayName, c.SourceUri,
                DashboardPage.FormatBytes(c.SizeBytes), c.ModifiedAt.LocalDateTime.ToString("g")))
            .ToList();
        FileList.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Summary.Text = $"{rows.Count} of {_all.Count} indexed file(s)";
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void ExtFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void FileList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) { }

    private async void Index_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        Spinner.Visibility = Visibility.Visible;
        Summary.Text = $"Indexing {folder.Path}…";
        var be = await App.Backend;
        var candidates = new List<FileCandidate>();
        await foreach (var c in be.Pipeline.ScanAsync(folder.Path, recursive: true))
            candidates.Add(c);
        var result = await be.Pipeline.IndexAsync(candidates);
        Summary.Text = result.IsSuccess
            ? $"Indexed {result.Value!.Indexed} file(s) ({result.Value.Hashed} hashed)."
            : $"Indexing failed: {result.Error!.Message}";
        await LoadIndexAsync();
    }
}
