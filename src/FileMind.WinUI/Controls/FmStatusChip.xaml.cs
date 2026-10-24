using FileMind.WinUI.Icons;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FileMind.WinUI.Controls;

/// <summary>Canonical operation states (components/03).</summary>
public enum FmOpState
{
    Ready, Queued, Checking, Running, Paused, Completed,
    CompletedWithWarnings, Failed, Cancelled, Offline,
    Unsupported, PermissionRequired
}

public sealed partial class FmStatusChip : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(FmOpState),
            typeof(FmStatusChip), new PropertyMetadata(FmOpState.Ready, OnChanged));

    public FmOpState State
    {
        get => (FmOpState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Optional short supporting text (spec item 4).</summary>
    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(string),
            typeof(FmStatusChip), new PropertyMetadata("", OnChanged));

    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public FmStatusChip()
    {
        InitializeComponent();
        Render();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((FmStatusChip)d).Render();

    private (string Label, FmIcon Icon, string BrushKey) Map() => State switch
    {
        FmOpState.Ready => ("Ready", FmIcon.Info, "StNeutral"),
        FmOpState.Queued => ("Queued", FmIcon.History, "StNeutral"),
        FmOpState.Checking => ("Checking", FmIcon.Search, "StInfo"),
        FmOpState.Running => ("Running", FmIcon.Play, "StInfo"),
        FmOpState.Paused => ("Paused", FmIcon.Pause, "StNeutral"),
        FmOpState.Completed => ("Completed", FmIcon.Success, "StSuccess"),
        FmOpState.CompletedWithWarnings => ("Completed with warnings", FmIcon.Warning, "StWarning"),
        FmOpState.Failed => ("Failed", FmIcon.Error, "StError"),
        FmOpState.Cancelled => ("Cancelled", FmIcon.Cancel, "StNeutral"),
        FmOpState.Offline => ("Offline", FmIcon.Cancel, "StOffline"),
        FmOpState.Unsupported => ("Unsupported", FmIcon.Info, "StNeutral"),
        FmOpState.PermissionRequired => ("Permission required", FmIcon.Lock, "StWarning"),
        _ => (State.ToString(), FmIcon.Info, "StNeutral")
    };

    private void Render()
    {
        var (label, icon, brushKey) = Map();
        ChipIcon.Glyph = ((FontIcon)FmIcons.Element(icon)).Glyph;
        ChipLabel.Text = Detail is { Length: > 0 } ? $"{label} — {Detail}" : label;
        var brush = (Brush)Resources[brushKey];
        ChipIcon.Foreground = brush;
        ChipLabel.Foreground = brush;
        ChipRoot.BorderBrush = brush;
    }
}
