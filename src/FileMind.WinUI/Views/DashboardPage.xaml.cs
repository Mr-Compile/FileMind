using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        PageTitle.Text = "Dashboard";
        Body.Title = "Storage, activity, and quick actions";
        Body.Body = "Bento cards wire to real backend state in M4.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
