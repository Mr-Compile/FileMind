using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FileMind.Branding.Tests;

public class TokenParityTests
{
    private static readonly JsonDocument Tokens = LoadJson();
    private static readonly string Css = LoadText("FileMind.Branding.Tests.branding.colors.css");

    private static JsonDocument LoadJson()
    {
        var stream = typeof(TokenParityTests).Assembly
            .GetManifestResourceStream("FileMind.Branding.Tests.branding.colors.json")
            ?? throw new FileNotFoundException("Embedded branding colors.json not found.");
        return JsonDocument.Parse(stream);
    }

    private static string LoadText(string name)
    {
        using var stream = typeof(TokenParityTests).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded resource '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Json(string section, string key) =>
        Tokens.RootElement.GetProperty(section).GetProperty(key).GetString()
        ?? throw new InvalidOperationException($"Missing token {section}.{key}");

    [Fact]
    public void SchemaVersion_IsOne()
    {
        Assert.Equal(1, Tokens.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void BrandTokens_MatchJson()
    {
        Assert.Equal(BrandTokens.Brand.Primary, Json("brand", "primary"));
        Assert.Equal(BrandTokens.Brand.Secondary, Json("brand", "secondary"));
        Assert.Equal(BrandTokens.Brand.Accent, Json("brand", "accent"));

        var gradient = Tokens.RootElement.GetProperty("brand").GetProperty("gradient")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal<string>(gradient, BrandTokens.Brand.Gradient);
        Assert.Equal(3, BrandTokens.Brand.Gradient.Count);
    }

    [Fact]
    public void ThemeTokens_MatchJson()
    {
        Assert.Equal(BrandTokens.Light.Background, Json("light", "background"));
        Assert.Equal(BrandTokens.Light.Surface, Json("light", "surface"));
        Assert.Equal(BrandTokens.Light.SurfaceElevated, Json("light", "surfaceElevated"));
        Assert.Equal(BrandTokens.Light.TextPrimary, Json("light", "textPrimary"));
        Assert.Equal(BrandTokens.Light.TextSecondary, Json("light", "textSecondary"));
        Assert.Equal(BrandTokens.Light.Border, Json("light", "border"));

        Assert.Equal(BrandTokens.Dark.Background, Json("dark", "background"));
        Assert.Equal(BrandTokens.Dark.Surface, Json("dark", "surface"));
        Assert.Equal(BrandTokens.Dark.SurfaceElevated, Json("dark", "surfaceElevated"));
        Assert.Equal(BrandTokens.Dark.TextPrimary, Json("dark", "textPrimary"));
        Assert.Equal(BrandTokens.Dark.TextSecondary, Json("dark", "textSecondary"));
        Assert.Equal(BrandTokens.Dark.Border, Json("dark", "border"));
    }

    [Fact]
    public void SemanticTokens_MatchJson()
    {
        Assert.Equal(BrandTokens.Semantic.Success, Json("semantic", "success"));
        Assert.Equal(BrandTokens.Semantic.Info, Json("semantic", "info"));
        Assert.Equal(BrandTokens.Semantic.Warning, Json("semantic", "warning"));
        Assert.Equal(BrandTokens.Semantic.Error, Json("semantic", "error"));
        Assert.Equal(BrandTokens.Semantic.Destructive, Json("semantic", "destructive"));
    }

    [Fact]
    public void AllJsonTokens_AreHexColors()
    {
        var hex = new Regex("^#[0-9A-Fa-f]{6}$");
        foreach (var section in Tokens.RootElement.EnumerateObject())
        {
            if (section.Name == "schemaVersion") continue;
            foreach (var token in section.Value.EnumerateObject())
            {
                if (token.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var stop in token.Value.EnumerateArray())
                        Assert.Matches(hex, stop.GetString());
                }
                else
                {
                    Assert.Matches(hex, token.Value.GetString());
                }
            }
        }
    }

    [Fact]
    public void LightAndDark_HaveIdenticalKeySets()
    {
        var light = Tokens.RootElement.GetProperty("light").EnumerateObject().Select(p => p.Name).OrderBy(n => n);
        var dark = Tokens.RootElement.GetProperty("dark").EnumerateObject().Select(p => p.Name).OrderBy(n => n);
        Assert.Equal(light, dark);
    }

    [Fact]
    public void Palette_ResolvesModes()
    {
        var light = BrandTokens.Palette(ThemeMode.Light);
        Assert.Equal(BrandTokens.Light.Background, light.Background);
        Assert.Equal(BrandTokens.Light.Border, light.Border);

        var dark = BrandTokens.Palette(ThemeMode.Dark);
        Assert.Equal(BrandTokens.Dark.Background, dark.Background);
        Assert.Equal(BrandTokens.Dark.Border, dark.Border);

        Assert.Equal(light, BrandTokens.Palette(ThemeMode.System));
    }

    [Fact]
    public void CssCustomProperties_MatchJson()
    {
        var rootProps = CssProps(CssBlock(Css, ":root"));
        var darkProps = CssProps(CssBlock(Css, "[data-theme=\"dark\"]"));

        Assert.Equal(Json("brand", "primary"), rootProps["--fm-primary"]);
        Assert.Equal(Json("brand", "secondary"), rootProps["--fm-secondary"]);
        Assert.Equal(Json("brand", "accent"), rootProps["--fm-accent"]);
        Assert.Equal(Json("light", "background"), rootProps["--fm-bg"]);
        Assert.Equal(Json("light", "surface"), rootProps["--fm-surface"]);
        Assert.Equal(Json("light", "textPrimary"), rootProps["--fm-text"]);
        Assert.Equal(Json("semantic", "success"), rootProps["--fm-success"]);
        Assert.Equal(Json("semantic", "info"), rootProps["--fm-info"]);
        Assert.Equal(Json("semantic", "warning"), rootProps["--fm-warning"]);
        Assert.Equal(Json("semantic", "error"), rootProps["--fm-error"]);
        Assert.Equal(Json("semantic", "destructive"), rootProps["--fm-destructive"]);

        Assert.Equal(Json("dark", "background"), darkProps["--fm-bg"]);
        Assert.Equal(Json("dark", "surface"), darkProps["--fm-surface"]);
        Assert.Equal(Json("dark", "textPrimary"), darkProps["--fm-text"]);
    }

    private static string CssBlock(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(start >= 0, $"CSS selector '{selector}' not found");
        var open = css.IndexOf('{', start);
        var close = css.IndexOf('}', open);
        return css[(open + 1)..close];
    }

    private static Dictionary<string, string> CssProps(string block) =>
        Regex.Matches(block, "(--fm-[a-z-]+)\\s*:\\s*(#[0-9A-Fa-f]{6})")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
}
