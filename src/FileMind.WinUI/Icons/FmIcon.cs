using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Icons;

/// <summary>
/// PLAN-014 M1.1 — semantic icon names (foundations/05-icons-svg-assets.md)
/// over one coherent family: Segoe Fluent Icons glyphs. SVG PathIcon slots
/// can replace individual glyphs later without touching call sites.
/// </summary>
public enum FmIcon
{
    Dashboard,
    Folder,
    File,
    Search,
    Filter,
    Sort,
    Organize,
    Duplicate,
    Delete,
    Storage,
    Device,
    Connect,
    Refresh,
    Play,
    Pause,
    Cancel,
    Retry,
    Success,
    Info,
    Warning,
    Error,
    Lock,
    Settings,
    History,
    Back,
    Forward,
    Up,
    More
}

public static class FmIcons
{
    private static string Glyph(FmIcon icon) => icon switch
    {
        FmIcon.Dashboard => "\uE80F",      // Home
        FmIcon.Folder => "\uE8B7",         // Folder
        FmIcon.File => "\uE8A5",           // Page
        FmIcon.Search => "\uE721",         // Search
        FmIcon.Filter => "\uE71C",         // Filter
        FmIcon.Sort => "\uE8CB",           // Sort
        FmIcon.Organize => "\uE8F0",       // MoveToFolder
        FmIcon.Duplicate => "\uE8C8",      // Copy
        FmIcon.Delete => "\uE74D",         // Delete
        FmIcon.Storage => "\uEDA2",        // HardDrive
        FmIcon.Device => "\uE772",         // ThisPC
        FmIcon.Connect => "\uE703",        // PlugConnected-ish
        FmIcon.Refresh => "\uE72C",        // Refresh
        FmIcon.Play => "\uE768",           // Play
        FmIcon.Pause => "\uE769",          // Pause
        FmIcon.Cancel => "\uE711",         // Cancel/ChromeClose-adjacent
        FmIcon.Retry => "\uE777",          // RotateRight-ish
        FmIcon.Success => "\uE73E",        // Completed/CheckMark
        FmIcon.Info => "\uE946",           // Info
        FmIcon.Warning => "\uE7BA",        // Warning
        FmIcon.Error => "\uE783",          // ErrorBadge
        FmIcon.Lock => "\uE72E",           // Lock
        FmIcon.Settings => "\uE713",       // Settings
        FmIcon.History => "\uE81C",        // History
        FmIcon.Back => "\uE72B",           // Back
        FmIcon.Forward => "\uE72A",        // Forward
        FmIcon.Up => "\uE74A",             // Up
        FmIcon.More => "\uE712",           // More
        _ => "\uE8A5"
    };

    public static IconElement Element(FmIcon icon, double size = 16) =>
        new FontIcon { Glyph = Glyph(icon), FontSize = size };
}
