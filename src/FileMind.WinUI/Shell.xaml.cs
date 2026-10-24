using FileMind.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI;

/// <summary>Shell (platforms/windows.md): sidebar nav + content frame.</summary>
public sealed partial class Shell : Page
{
    private static readonly IReadOnlyDictionary<string, (Type Page, string Crumb)> Routes =
        new Dictionary<string, (Type, string)>
        {
            ["dashboard"] = (typeof(DashboardPage), "Dashboard"),
            ["files"] = (typeof(FilesPage), "Files"),
            ["organizer"] = (typeof(OrganizerPage), "Organizer"),
            ["tools"] = (typeof(ToolsPage), "Tools"),
            ["search"] = (typeof(SearchPage), "Search"),
            ["devices"] = (typeof(DevicesPage), "Devices"),
            ["activity"] = (typeof(ActivityPage), "Activity"),
            ["settings"] = (typeof(SettingsPage), "Settings"),
            ["gallery"] = (typeof(GalleryPage), "Component gallery"),
        };

    /// <summary>Titlebar strip handed to <see cref="Window.SetTitleBar"/>.</summary>
    public UIElement TitleBarElement => TitleBar;

    public Shell()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = (string)item.Tag;
        if (!Routes.TryGetValue(tag, out var route)) return;
        ContentFrame.Navigate(route.Page);
        Crumbs.ItemsSource = new[] { "FileMind", route.Crumb };
    }

    private void GlobalSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ContentFrame.Navigate(typeof(SearchPage), args.QueryText);
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>()
            .First(i => (string)i.Tag == "search");
        Crumbs.ItemsSource = new[] { "FileMind", "Search" };
    }

    private void OpCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ActivityPage));
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>()
            .First(i => (string)i.Tag == "activity");
        Crumbs.ItemsSource = new[] { "FileMind", "Activity" };
    }
}
