namespace FileMind.Branding;

/// <summary>
/// Platform-neutral brand color tokens. Source of truth: branding/colors.json.
/// Parity with that file is enforced by FileMind.Branding.Tests — update the JSON first.
/// </summary>
public static class BrandTokens
{
    public static class Brand
    {
        public const string Primary = "#2563EB";
        public const string Secondary = "#4F46E5";
        public const string Accent = "#7C3AED";
        public static IReadOnlyList<string> Gradient { get; } = [Primary, Secondary, Accent];
    }

    public static class Light
    {
        public const string Background = "#F8FAFC";
        public const string Surface = "#FFFFFF";
        public const string SurfaceElevated = "#F1F5F9";
        public const string TextPrimary = "#0F172A";
        public const string TextSecondary = "#64748B";
        public const string Border = "#E2E8F0";
    }

    public static class Dark
    {
        public const string Background = "#0B1120";
        public const string Surface = "#111827";
        public const string SurfaceElevated = "#1E293B";
        public const string TextPrimary = "#F8FAFC";
        public const string TextSecondary = "#94A3B8";
        public const string Border = "#334155";
    }

    public static class Semantic
    {
        public const string Success = "#16A34A";
        public const string Info = "#0284C7";
        public const string Warning = "#D97706";
        public const string Error = "#DC2626";
        public const string Destructive = "#B91C1C";
    }

    /// <summary>
    /// Resolves a theme palette. System maps to Light here; real OS theme
    /// detection is a UI-layer concern when a host project exists.
    /// </summary>
    public static ThemePalette Palette(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => new ThemePalette(
            Dark.Background, Dark.Surface, Dark.SurfaceElevated,
            Dark.TextPrimary, Dark.TextSecondary, Dark.Border),
        _ => new ThemePalette(
            Light.Background, Light.Surface, Light.SurfaceElevated,
            Light.TextPrimary, Light.TextSecondary, Light.Border),
    };
}

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed record ThemePalette(
    string Background,
    string Surface,
    string SurfaceElevated,
    string TextPrimary,
    string TextSecondary,
    string Border);
