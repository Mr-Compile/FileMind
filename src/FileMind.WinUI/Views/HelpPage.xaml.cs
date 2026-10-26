using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Help &amp; About hub — the 9 canonical destinations
/// (crossplatform-ux/04_HELP_ABOUT), mirrored from the Android HelpScreens.Hub.</summary>
public sealed partial class HelpPage : Page
{
    private sealed record TopicRow(string Glyph, string Title, string Sub, string Route);

    private static readonly TopicRow[] Topics =
    [
        new("\uE946", "About FileMind", "Version, purpose, links", "help/about"),
        new("\uE8F1", "Getting Started", "Your first organize run", "help/getting-started"),
        new("\uE82D", "User Guide", "Tasks and features", "help/guide"),
        new("\uE8BC", "What's New", "Release highlights", "help/whats-new"),
        new("\uE90F", "Troubleshooting", "Common problems and fixes", "help/troubleshooting"),
        new("\uE9D9", "Diagnostics", "Support info and health checks", "help/diagnostics"),
        new("\uE8F2", "Feedback", "Report a problem or suggest an idea", "help/feedback"),
        new("\uE716", "Credits", "The people and project", "help/credits"),
        new("\uE8A5", "Licenses", "Third-party notices", "help/licenses"),
    ];

    public HelpPage()
    {
        InitializeComponent();
        TopicList.ItemsSource = Topics;
    }

    private void Topic_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TopicRow t &&
            App.MainAppWindow?.Content is Shell shell)
            shell.NavigateTo(t.Route);
    }
}
