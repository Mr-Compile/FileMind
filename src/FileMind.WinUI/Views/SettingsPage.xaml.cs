using FileMind.Branding;
using FileMind.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileMind.WinUI.Views;

/// <summary>Settings — theme/density are real (live-applied); the
/// organization mode is displayed truthfully and edited via the host.</summary>
public sealed partial class SettingsPage : Page
{
    private bool _ready;

    public SettingsPage()
    {
        InitializeComponent();
        ThemePicker.SelectedIndex = 0;
        DensityPicker.SelectedIndex = (int)App.Theme.Density;
        _ready = true;
        _ = ShowModeAsync();
    }

    private async Task ShowModeAsync()
    {
        var be = await App.Backend;
        ModeText.Text = $"Organization mode: {be.ModePolicy.Mode}";
        MotionNote.Text = App.Theme.ReducedMotion
            ? "Reduced motion is on (Windows “Show animations” off) — non-essential motion is skipped."
            : "";
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var mode = ThemePicker.SelectedIndex switch
        {
            1 => ThemeMode.Light,
            2 => ThemeMode.Dark,
            _ => ThemeMode.System
        };
        if (App.MainAppWindow?.Content is FrameworkElement root)
            App.Theme.ApplyTheme(root, mode);
    }

    private void DensityPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        App.Theme.SetDensity(DensityPicker.SelectedIndex == 1
            ? FmDensity.Compact : FmDensity.Comfortable);
    }
}
