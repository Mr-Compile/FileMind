using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

public sealed partial class DevicesPage : Page
{
    public DevicesPage()
    {
        InitializeComponent();
        PageTitle.Text = "Devices";
        Body.Title = "This PC and connected devices";
        Body.Body = "This PC is real; Android shows as Planned.";
        Body.ActionLabel = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is string q && q.Length > 0 && PageTitle.Text == "Search")
            Body.Body = $"Results for '{q}' — the search index wires in M4.";
    }
}
