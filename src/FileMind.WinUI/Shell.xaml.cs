using FileMind.Branding;
using FileMind.WinUI.Services;
using FileMind.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI;

/// <summary>Shell (platforms/windows.md): titlebar + sidebar nav + content frame.</summary>
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
        Loaded += Shell_Loaded;
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    private async void Shell_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= Shell_Loaded;
        var be = await App.Backend;
        var badge = be.RecoveredOnStart;
        // op-center badge mirrors needs-attention ops (mockup .op-btn .dot count)
        var ops = await be.Pipeline.RecentOperationsAsync(8);
        if (ops.IsSuccess)
            badge += ops.Value!.Count(o =>
                o.Status == FileMind.Domain.Contracts.OperationStatus.NeedsAttention);
        OpBadge.Value = badge;
        OpBadge.Visibility = badge > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Navigate by route tag (used by page quick-actions too).</summary>
    public void NavigateTo(string tag, object? parameter = null)
    {
        if (!Routes.TryGetValue(tag, out var route)) return;
        ContentFrame.Navigate(route.Page, parameter);
        var item = Nav.MenuItems.Concat(Nav.FooterMenuItems)
            .OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item is not null) Nav.SelectedItem = item;
        Crumbs.ItemsSource = new[] { "FileMind", route.Crumb };
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
        var item = Nav.MenuItems.OfType<NavigationViewItem>()
            .First(i => (string)i.Tag == "search");
        Nav.SelectedItem = item;
        Crumbs.ItemsSource = new[] { "FileMind", "Search" };
    }

    private void OpCenterButton_Click(object sender, RoutedEventArgs e) => NavigateTo("activity");

    private void DensityButton_Click(object sender, RoutedEventArgs e)
    {
        var t = App.Theme;
        t.SetDensity(t.Density == FmDensity.Compact ? FmDensity.Comfortable : FmDensity.Compact);
        // mockup body.compact: sidebar collapses to the 56px icon rail
        Nav.IsPaneOpen = t.Density != FmDensity.Compact;
        // density affects new layouts; navigate to refresh the current page
        if (ContentFrame.SourcePageType is { } p) ContentFrame.Navigate(p);
    }

    private void ThemeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem item) return;
        var mode = (string)item.Tag switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
        App.Theme.ApplyTheme(this, mode);
    }
}
