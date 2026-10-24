using FileMind.Branding;
using Microsoft.UI.Xaml;

namespace FileMind.WinUI.Services;

public enum FmDensity { Comfortable, Compact }

/// <summary>
/// PLAN-014 M1.2 — theme/density service over the canonical palette.
/// System/Light/Dark map to ElementTheme; density flips a resource flag
/// file-heavy views read; ReducedMotion follows the OS "Show animations"
/// setting — screens check it before starting non-essential motion.
/// </summary>
public sealed class FmThemeService
{
    private readonly Windows.UI.ViewManagement.UISettings _ui = new();

    public ThemeMode Mode { get; private set; } = ThemeMode.System;
    public FmDensity Density { get; private set; } = FmDensity.Comfortable;

    /// <summary>OS prefers-reduced-motion ("Show animations in Windows" off).</summary>
    public bool ReducedMotion => !_ui.AnimationsEnabled;

    /// <summary>Canonical palette for the resolved mode (System → OS dark/light).</summary>
    public ThemePalette ResolvedPalette()
    {
        var dark = Mode == ThemeMode.Dark
            || (Mode == ThemeMode.System && _ui.GetColorValue(
                Windows.UI.ViewManagement.UIColorType.Background) is { R: < 128 });
        return dark
            ? new ThemePalette(BrandTokens.Dark.Background, BrandTokens.Dark.Surface,
                BrandTokens.Dark.SurfaceElevated, BrandTokens.Dark.TextPrimary,
                BrandTokens.Dark.TextSecondary, BrandTokens.Dark.Border)
            : new ThemePalette(BrandTokens.Light.Background, BrandTokens.Light.Surface,
                BrandTokens.Light.SurfaceElevated, BrandTokens.Light.TextPrimary,
                BrandTokens.Light.TextSecondary, BrandTokens.Light.Border);
    }

    /// <summary>Apply mode to the visual root; ThemeDictionaries follow automatically.</summary>
    public void ApplyTheme(FrameworkElement root, ThemeMode mode)
    {
        Mode = mode;
        root.RequestedTheme = mode switch
        {
            ThemeMode.Light => ElementTheme.Light,
            ThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    public void SetDensity(FmDensity density) => Density = density;
}
