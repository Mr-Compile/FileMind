using Microsoft.UI.Xaml;

namespace FileMind.WinUI.Services;

public enum FmDensity { Comfortable, Compact }

public enum ThemeMode { System, Light, Dark }

/// <summary>Resolved-mode palette snapshot (design/TOKEN_MAP.md).</summary>
public sealed record ThemePalette(
    string Background, string Surface, string SurfaceElevated,
    string TextPrimary, string TextSecondary, string Border);

/// <summary>Canonical FileMind palette (design/TOKEN_MAP.md; mirrors
/// FileMind.Branding.BrandTokens — kept local until the M4 backend
/// reference lands, which needs a signed/allowed binary path).</summary>
public static class FmPalette
{
    public const string LightBackground = "#F8FAFC", LightSurface = "#FFFFFF",
        LightSurfaceElevated = "#F1F5F9", LightTextPrimary = "#0F172A",
        LightTextSecondary = "#64748B", LightBorder = "#E2E8F0";
    public const string DarkBackground = "#0B1120", DarkSurface = "#111827",
        DarkSurfaceElevated = "#1E293B", DarkTextPrimary = "#F8FAFC",
        DarkTextSecondary = "#94A3B8", DarkBorder = "#334155";
}

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
            ? new ThemePalette(FmPalette.DarkBackground, FmPalette.DarkSurface,
                FmPalette.DarkSurfaceElevated, FmPalette.DarkTextPrimary,
                FmPalette.DarkTextSecondary, FmPalette.DarkBorder)
            : new ThemePalette(FmPalette.LightBackground, FmPalette.LightSurface,
                FmPalette.LightSurfaceElevated, FmPalette.LightTextPrimary,
                FmPalette.LightTextSecondary, FmPalette.LightBorder);
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
