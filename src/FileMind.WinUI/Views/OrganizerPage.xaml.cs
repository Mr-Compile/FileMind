using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class OrganizerPage : Page
{
    public OrganizerPage()
    {
        InitializeComponent();
        PageTitle.Text = "Organizer";
        Body.Title = "Plan and run organization";
        Body.Body = "The operation flow (select → pre-check → confirm → execute → result) wires to OrganizerPipeline in M4.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
