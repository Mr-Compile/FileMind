using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace FileMind.WinUI.Views;

/// <summary>Files screen — real file index (IFileIndexRepository),
/// name/extension filters + sort, multi-select, context menu, inspector.</summary>
public sealed partial class FilesPage : Page
{
    private sealed record FileRow(string Name, string Path, string Size, string Modified,
        FileCandidate Candidate)
    {
        public override string ToString() => $"{Name} — {Size} — {Path}";
    }
    private IReadOnlyList<FileCandidate> _all = [];

    public FilesPage()
    {
        InitializeComponent();
        SortBox.SelectedIndex = 0;
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
        // SelectionChanged can fire during InitializeComponent before the
        // list/summary fields exist — bail until the page is built.
        if (FileList is null || Summary is null || Empty is null) return;
        var nameFilter = FilterBox.Text.Trim();
        var ext = (ExtFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var sort = (SortBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "name";

        var q = _all
            .Where(c => (nameFilter.Length == 0 ||
                c.DisplayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                && (ext.Length == 0 || c.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)));
        q = sort switch
        {
            "size" => q.OrderByDescending(c => c.SizeBytes),
            "modified" => q.OrderByDescending(c => c.ModifiedAt),
            _ => q.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase),
        };

        var rows = q.Take(1000)
            .Select(c => new FileRow(c.DisplayName, c.SourceUri,
                DashboardPage.FormatBytes(c.SizeBytes), c.ModifiedAt.LocalDateTime.ToString("g"), c))
            .ToList();
        FileList.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Summary.Text = $"{rows.Count} of {_all.Count} indexed file(s)";
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void ExtFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void FileList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) { }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not FileRow row)
        {
            Inspector.Visibility = Visibility.Collapsed;
            return;
        }
        var c = row.Candidate;
        InsName.Text = c.DisplayName;
        InsPath.Text = c.SourceUri;
        InsMeta.Text =
            $"{c.Extension} · {DashboardPage.FormatBytes(c.SizeBytes)}" +
            (c.MimeType is { } m ? $" · {m}" : "") +
            $"\nModified {c.ModifiedAt.LocalDateTime:g} · Created {c.CreatedAt.LocalDateTime:g}" +
            (FileList.SelectedItems.Count > 1
                ? $"\n{FileList.SelectedItems.Count} items selected" : "");
        InsHash.Text = c.ContentHash is { } h
            ? $"SHA-256 {h[..16]}… ({c.HashState})"
            : $"Hash: {c.HashState}";
        Inspector.Visibility = Visibility.Visible;
    }

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
