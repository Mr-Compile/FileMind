using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class SearchPage : Page
{
    public SearchPage()
    {
        InitializeComponent();
        PageTitle.Text = "Search";
        Body.Title = "Search the file index";
        Body.Body = "Run a scan first; FTS results wire in M4.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
