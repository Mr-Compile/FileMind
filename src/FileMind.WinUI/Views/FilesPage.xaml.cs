using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class FilesPage : Page
{
    public FilesPage()
    {
        InitializeComponent();
        PageTitle.Text = "Files";
        Body.Title = "Browse and organize files";
        Body.Body = "Scope navigation, table, and inspector wire in M4.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
