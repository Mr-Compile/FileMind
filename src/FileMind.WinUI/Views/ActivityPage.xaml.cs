using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
    {
        InitializeComponent();
        PageTitle.Text = "Activity";
        Body.Title = "Operations and history";
        Body.Body = "Live operation records wire in M4.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
