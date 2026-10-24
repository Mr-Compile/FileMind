using FileMind.WinUI.Controls;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Tools — truthful availability per implemented backend surface.</summary>
public sealed partial class ToolsPage : Page
{
    private sealed record Tool(string Name, string Glyph, FmOpState State,
        string StateDetail, string Description);

    public ToolsPage()
    {
        InitializeComponent();
        ToolList.ItemsSource = new Tool[]
        {
            new("Organize files", "\uE8F0", FmOpState.Ready, "Available",
                "Scan, classify, plan, and run organize operations."),
            new("Duplicate detection", "\uE8C7", FmOpState.Ready, "Available",
                "Content-hash based duplicate groups over the file index."),
            new("Search index", "\uE721", FmOpState.Ready, "Available",
                "Local FTS5 + structured filters over indexed files."),
            new("Profiles", "\uE748", FmOpState.Ready, "Available",
                "Export/import organizer profiles (host CLI verbs)."),
            new("Watch folders", "\uE81C", FmOpState.Ready, "Available",
                "Background watch jobs via the host CLI."),
            new("AI assistance (Groq)", "\uE945", FmOpState.Offline, "Off by default",
                "Opt-in advisory AI — configure a key via host `ai` verbs."),
            new("Android sync", "\uE8EA", FmOpState.Unsupported, "Planned",
                "Owned by PLAN-010; not available on Windows yet."),
        };
    }
}
