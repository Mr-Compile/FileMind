using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace FileMind.WinUI.Views;

/// <summary>
/// Static Help &amp; About destinations (crossplatform-ux/04_HELP_ABOUT),
/// content mirrored from the Android HelpScreens with Windows-true wording
/// (e.g. FTS search and duplicates are real here, "planned" only where they are).
/// </summary>
public sealed partial class HelpTopicPage : Page
{
    public HelpTopicPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var topic = e.Parameter as string ?? "about";
        Body.Children.Clear();
        switch (topic)
        {
            case "about": RenderAbout(); break;
            case "getting-started": RenderGettingStarted(); break;
            case "guide": RenderGuide(); break;
            case "whats-new": RenderWhatsNew(); break;
            case "troubleshooting": RenderTroubleshooting(); break;
            case "feedback": RenderFeedback(); break;
            case "credits": RenderCredits(); break;
            case "licenses": RenderLicenses(); break;
            default: TitleText.Text = "Help & About"; break;
        }
    }

    // ------------------------------------------------------------ builders

    private void RenderAbout()
    {
        TitleText.Text = "About FileMind";
        var ver = global::System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
                typeof(App).Assembly)?.InformationalVersion
            ?? typeof(App).Assembly.GetName().Version?.ToString() ?? "dev";
        SubText.Text = $"Version {ver} · offline-first, privacy-first file organizer";
        Para("FileMind organizes your files with rules you control — it analyzes " +
            "first, shows the plan, and only acts after you confirm. Everything " +
            "runs on your PC; nothing is uploaded.");
        Link("Getting Started", "Learn the workflow", "help/getting-started");
        Link("What's New", "Latest changes", "help/whats-new");
        Link("Diagnostics", "Support information", "help/diagnostics");
        Link("Licenses", "Open-source notices", "help/licenses");
    }

    private void RenderGettingStarted()
    {
        TitleText.Text = "Getting Started";
        SubText.Text = "How FileMind works — analyze first, act only after you confirm.";
        Steps(
            ("1. Pick a folder", "Choose a source in the Organizer — FileMind only touches folders you pick."),
            ("2. Analyze", "FileMind scans and suggests where each file belongs, with a confidence score and the signals behind it."),
            ("3. Review", "Check the plan. Anything unclear lands in Needs Review instead of moving."),
            ("4. Confirm", "See exact counts — and the promise that nothing gets deleted."),
            ("5. Result & Undo", "Watch progress, then undo the whole run from Activity if needed."));
        var btn = new Button
        {
            Content = "Start organizing",
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Left
        };
        btn.Click += (_, _) => Go("organizer");
        Body.Children.Add(btn);
    }

    private void RenderGuide()
    {
        TitleText.Text = "User Guide";
        SubText.Text = "Organized by task, not by internal module.";
        Steps(
            ("Browsing", "Files lists everything FileMind has indexed — multi-select, right-click for actions, and inspect any row for its decision evidence."),
            ("Search", "Search runs real SQLite FTS5 over names, paths and indexed metadata."),
            ("Organizing", "The organizer analyzes, previews, then executes after your confirm. Smart Inbox catches files FileMind isn't sure about."),
            ("Duplicates", "Tools → Find duplicates hashes the index into duplicate groups with reclaimable-space estimates."),
            ("Activity & Undo", "Activity shows running and finished operations with undo where a reversible recipe exists."),
            ("Privacy", "No account, no telemetry. AI suggestions are opt-in and off by default; diagnostics never include file names or paths unless you opt in."),
            ("Settings", "Organization mode, conflict policy, appearance and notification preferences live under Settings."));
    }

    private void RenderWhatsNew()
    {
        TitleText.Text = "What's New";
        SubText.Text = "Release highlights.";
        Para("Native Windows app — the full FileMind experience on the real " +
            "organizer pipeline: organize flow with dry-run preview, activity " +
            "center with undo, smart inbox, FTS search, duplicate and storage " +
            "tools, and this Help & About section — everything running locally " +
            "on your PC.");
    }

    private void RenderTroubleshooting()
    {
        TitleText.Text = "Troubleshooting";
        SubText.Text = "Common problems and how to recover.";
        Steps(
            ("A scan finds nothing", "The folder may be empty, or nothing is indexed yet — use Files → Index folder, or run the Organizer on a folder that contains files."),
            ("Items need attention", "Open Activity — failed items keep their reason and can be undone where a recipe exists."),
            ("Files didn't move", "Manual mode only previews until you confirm. Nothing moves without an explicit confirm."),
            ("Watched folders not reacting", "Folder watching runs through the host watch verbs — check the watch list under Settings → Privacy & safety."));
        Link("Diagnostics", "Copy safe support info", "help/diagnostics");
    }

    private void RenderFeedback()
    {
        TitleText.Text = "Feedback";
        SubText.Text = "Report a problem or suggest an idea.";
        Para("Feedback collection isn't wired yet — this release keeps " +
            "everything local. Use Diagnostics → Copy diagnostic info and " +
            "share it through your usual channel.");
        Link("Diagnostics", "Copy safe support info", "help/diagnostics");
    }

    private void RenderCredits()
    {
        TitleText.Text = "Credits";
        SubText.Text = "The people and the project.";
        Para("FileMind — local-first file organization.");
        Muted("Built with WinUI 3 (Windows App SDK), .NET, and SQLite.");
    }

    private void RenderLicenses()
    {
        TitleText.Text = "Licenses";
        SubText.Text = "Third-party notices for the libraries this build uses.";
        Steps(
            (".NET / Windows App SDK (WinUI 3)", "MIT License"),
            ("Microsoft.Data.Sqlite", "MIT License"),
            ("Microsoft.Extensions.Hosting", "MIT License"),
            ("SQLite", "Public domain"));
    }

    // ------------------------------------------------------------ widgets

    private void Steps(params (string Title, string Body)[] items)
    {
        foreach (var (t, b) in items)
        {
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock
            {
                Text = t,
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextH3"]
            });
            sp.Children.Add(new TextBlock
            {
                Text = b, TextWrapping = TextWrapping.Wrap,
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextBodySmall"],
                Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
            });
            Body.Children.Add(sp);
        }
    }

    private void Para(string text) => Body.Children.Add(new TextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap,
        Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextBody"]
    });

    private void Muted(string text) => Body.Children.Add(new TextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap,
        Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["FmTextBodySmall"],
        Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
    });

    /// <summary>Row that navigates to another help route — the same shape as
    /// the hub rows (icon + title/sub + chevron inside a bordered card).</summary>
    private void Link(string title, string sub, string route)
    {
        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 10, 12, 10)
        };
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new FontIcon
        {
            Glyph = "\uE946", FontSize = 16, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
        });
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        texts.Children.Add(new TextBlock
        {
            Text = sub, FontSize = 12,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
        });
        Grid.SetColumn(texts, 1);
        g.Children.Add(texts);
        var chevron = new FontIcon
        {
            Glyph = "\uE76C", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["FmTextSecondaryBrush"]
        };
        Grid.SetColumn(chevron, 2);
        g.Children.Add(chevron);
        btn.Content = g;
        btn.Click += (_, _) => Go(route);
        Body.Children.Add(btn);
    }

    private void Go(string tag)
    {
        if (App.MainAppWindow?.Content is Shell shell) shell.NavigateTo(tag);
    }
}
