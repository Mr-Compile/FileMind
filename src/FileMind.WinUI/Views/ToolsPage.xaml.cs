using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class ToolsPage : Page
{
    public ToolsPage()
    {
        InitializeComponent();
        PageTitle.Text = "Tools";
        Body.Title = "Utilities";
        Body.Body = "Tool availability surfaces truthfully in M4.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
