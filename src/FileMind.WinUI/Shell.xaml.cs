using FileMind.Branding;
using FileMind.WinUI.Services;
using FileMind.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI;

/// <summary>Shell (platforms/windows.md): titlebar + sidebar nav + content frame.</summary>
public sealed partial class Shell : Page
{
    // Route IDs mirror the canonical shared IA (crossplatform-ux SHARED_UX_SYSTEM):
    // home, files, search, tools, activity, settings + help/* destinations —
    // the same route identifiers the Android app uses (android Routes.kt).
    private static readonly IReadOnlyDictionary<string, (Type Page, string Crumb, object? Param)> Routes =
        new Dictionary<string, (Type, string, object?)>
        {
            ["home"] = (typeof(DashboardPage), "Home", null),
            ["files"] = (typeof(FilesPage), "Files", null),
            ["organizer"] = (typeof(OrganizerPage), "Organizer", null),
            ["tools"] = (typeof(ToolsPage), "Tools", null),
            ["search"] = (typeof(SearchPage), "Search", null),
            ["devices"] = (typeof(DevicesPage), "Devices", null),
            ["activity"] = (typeof(ActivityPage), "Activity", null),
            ["settings"] = (typeof(SettingsPage), "Settings", null),
            ["help"] = (typeof(HelpPage), "Help & About", null),
            ["help/about"] = (typeof(HelpTopicPage), "About FileMind", "about"),
            ["help/getting-started"] = (typeof(HelpTopicPage), "Getting Started", "getting-started"),
            ["help/guide"] = (typeof(HelpTopicPage), "User Guide", "guide"),
            ["help/whats-new"] = (typeof(HelpTopicPage), "What's New", "whats-new"),
            ["help/troubleshooting"] = (typeof(HelpTopicPage), "Troubleshooting", "troubleshooting"),
            ["help/diagnostics"] = (typeof(DiagnosticsPage), "Diagnostics", null),
            ["help/feedback"] = (typeof(HelpTopicPage), "Feedback", "feedback"),
            ["help/credits"] = (typeof(HelpTopicPage), "Credits", "credits"),
            ["help/licenses"] = (typeof(HelpTopicPage), "Licenses", "licenses"),
            ["gallery"] = (typeof(GalleryPage), "Component gallery", null),
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
    public async void NavigateTo(string tag, object? parameter = null)
    {
        if (!Routes.TryGetValue(tag, out var route)) return;
        if (!await ConfirmLeavingOrganizerAsync(tag)) return;
        ContentFrame.Navigate(route.Page, parameter ?? route.Param);
        var item = Nav.MenuItems.Concat(Nav.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => (string)i.Tag == tag)
            ?? Nav.MenuItems.Concat(Nav.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => tag.StartsWith((string)i.Tag + "/", StringComparison.Ordinal));
        if (item is not null) Nav.SelectedItem = item;
        // Sub-routes (help/*) keep their parent in the breadcrumb trail.
        Crumbs.ItemsSource = tag.Contains('/')
            ? new[] { "FileMind", "Help & About", route.Crumb }
            : new[] { "FileMind", route.Crumb };
    }

    private bool _suppressNav;

    /// <summary>
    /// Leave-guard (PLAN-028): while the organizer is mid-execute, leaving asks
    /// "Keep running / Cancel run / Stay". The page is NavigationCacheMode=Required
    /// so "Keep running" leaves a live run that resumes rendering on return.
    /// </summary>
    private async Task<bool> ConfirmLeavingOrganizerAsync(string destTag)
    {
        if (destTag == "organizer") return true;
        if (ContentFrame.Content is not OrganizerPage org || !org.IsExecuting) return true;
        var dlg = new ContentDialog
        {
            Title = "Run in progress",
            Content = $"{org.RunDone} of {org.RunTotal} item(s) done. The run can keep " +
                "going in the background — the result lands in Activity.",
            PrimaryButtonText = "Keep running",
            SecondaryButtonText = "Cancel run",
            CloseButtonText = "Stay",
            XamlRoot = XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };
        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary) return true;
        if (r == ContentDialogResult.Secondary) { org.RequestCancel(); return true; }
        return false;
    }

    private string CurrentTag() =>
        Routes.FirstOrDefault(kv => kv.Value.Page == ContentFrame.SourcePageType).Key;

    private async void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNav) return;
        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = (string)item.Tag;
        if (!Routes.TryGetValue(tag, out var route)) return;
        if (await ConfirmLeavingOrganizerAsync(tag))
        {
            ContentFrame.Navigate(route.Page, route.Param);
            Crumbs.ItemsSource = tag.Contains('/')
                ? new[] { "FileMind", "Help & About", route.Crumb }
                : new[] { "FileMind", route.Crumb };
        }
        else
        {
            // Stay — restore the sidebar selection to the current page.
            var back = Nav.MenuItems.Concat(Nav.FooterMenuItems)
                .OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == CurrentTag());
            if (back is not null)
            {
                _suppressNav = true;
                Nav.SelectedItem = back;
                _suppressNav = false;
            }
        }
    }

    private async void GlobalSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!await ConfirmLeavingOrganizerAsync("search")) return;
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
