using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Audiomatic;

/// <summary>
/// Resolves theme-aware brushes based on the current RequestedTheme,
/// since code-behind Application.Current.Resources lookups always use the system theme.
/// </summary>
internal static class ThemeHelper
{
    internal static ElementTheme CurrentTheme { get; set; } = ElementTheme.Default;

    // WinUI 3 ThemeDictionaries use "Default" for dark, "Light" for light
    private static readonly string[] DarkKeys = ["Dark", "Default"];
    private static readonly string[] LightKeys = ["Light"];

    internal static Brush Brush(string key)
    {
        var themeKeys = ResolveThemeKeys();
        foreach (var themeKey in themeKeys)
        {
            var result = FindInThemeDictionaries(Application.Current.Resources, key, themeKey);
            if (result != null) return result;
        }
        return (Brush)Application.Current.Resources[key];
    }

    private static string[] ResolveThemeKeys()
    {
        if (CurrentTheme == ElementTheme.Dark) return DarkKeys;
        if (CurrentTheme == ElementTheme.Light) return LightKeys;

        // Default (system): detect actual system theme
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var bg = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            return bg.R < 128 ? DarkKeys : LightKeys;
        }
        catch
        {
            return DarkKeys;
        }
    }

    private static Brush? FindInThemeDictionaries(ResourceDictionary dict, string key, string themeKey)
    {
        if (dict.ThemeDictionaries.TryGetValue(themeKey, out var td)
            && td is ResourceDictionary themeDict
            && themeDict.ContainsKey(key))
        {
            return themeDict[key] as Brush;
        }

        foreach (var merged in dict.MergedDictionaries)
        {
            var result = FindInThemeDictionaries(merged, key, themeKey);
            if (result != null) return result;
        }

        return null;
    }

    // -- Accent color overrides --

    private static ResourceDictionary? _accentOverrideDict;

    internal static readonly (string Name, string Hex)[] AccentPresets =
    [
        ("System", ""),
        ("Red", "#E81123"),
        ("Orange", "#F7630C"),
        ("Gold", "#FFB900"),
        ("Green", "#10893E"),
        ("Teal", "#038387"),
        ("Blue", "#0078D4"),
        ("Indigo", "#6B69D6"),
        ("Purple", "#744DA9"),
        ("Pink", "#E3008C"),
        ("Rose", "#EA005E"),
        ("Wine", "#9A0089"),
        ("Rust", "#DA3B01"),
        ("Amber", "#FF8C00"),
        ("Lime", "#00CC6A"),
        ("Seafoam", "#00B7C3"),
        ("Navy", "#0063B1"),
        ("Iris", "#8764B8"),
        ("Orchid", "#C239B3"),
        ("Brick", "#D13438"),
        ("Olive", "#498205"),
        ("Mint", "#00B294"),
        ("Sky", "#0099BC"),
        ("Steel", "#515C6B"),
    ];

    /// <summary>
    /// Applies a custom accent color. Safe to call at startup (before UI)
    /// and at runtime (saves setting, requires restart for full XAML effect).
    /// </summary>
    internal static void ApplyAccentColor(string? hexColor)
    {
        if (string.IsNullOrEmpty(hexColor))
        {
            // Clear overrides if dict exists
            if (_accentOverrideDict != null)
            {
                ClearDict(_accentOverrideDict);
                ClearDict(_accentOverrideDict.ThemeDictionaries["Default"] as ResourceDictionary);
                ClearDict(_accentOverrideDict.ThemeDictionaries["Light"] as ResourceDictionary);
            }
            return;
        }

        var color = ParseHexColor(hexColor);
        var l1 = Lighten(color, 0.15);
        var l2 = Lighten(color, 0.30);
        var l3 = Lighten(color, 0.45);
        var d1 = Darken(color, 0.15);
        var d2 = Darken(color, 0.30);
        var d3 = Darken(color, 0.45);

        // Create dictionary once, never remove it
        if (_accentOverrideDict == null)
        {
            _accentOverrideDict = new ResourceDictionary();
            _accentOverrideDict.ThemeDictionaries["Default"] = new ResourceDictionary();
            _accentOverrideDict.ThemeDictionaries["Light"] = new ResourceDictionary();
            Application.Current.Resources.MergedDictionaries.Add(_accentOverrideDict);
        }

        // Update values in place — colors + brushes for dark theme
        var darkDict = (_accentOverrideDict.ThemeDictionaries["Default"] as ResourceDictionary)!;
        SetAccentResources(darkDict, color, l1, l2, l3, d1, d2, d3, isDark: true);

        // Light theme
        var lightDict = (_accentOverrideDict.ThemeDictionaries["Light"] as ResourceDictionary)!;
        SetAccentResources(lightDict, color, l1, l2, l3, d1, d2, d3, isDark: false);

        // Top-level fallback
        SetAccentResources(_accentOverrideDict, color, l1, l2, l3, d1, d2, d3, isDark: true);
    }

    private static void ClearDict(ResourceDictionary? dict)
    {
        dict?.Clear();
    }

    private static void SetAccentResources(ResourceDictionary dict,
        Windows.UI.Color color, Windows.UI.Color l1, Windows.UI.Color l2, Windows.UI.Color l3,
        Windows.UI.Color d1, Windows.UI.Color d2, Windows.UI.Color d3, bool isDark)
    {
        // Color resources
        dict["SystemAccentColor"] = color;
        dict["SystemAccentColorLight1"] = l1;
        dict["SystemAccentColorLight2"] = l2;
        dict["SystemAccentColorLight3"] = l3;
        dict["SystemAccentColorDark1"] = d1;
        dict["SystemAccentColorDark2"] = d2;
        dict["SystemAccentColorDark3"] = d3;

        // Brush resources — WinUI dark uses Light variants, light uses Dark variants
        var fill = isDark ? l2 : d1;
        var fillSecondary = WithAlpha(fill, 0.9);
        var fillTertiary = WithAlpha(fill, 0.8);
        var fillDisabled = WithAlpha(fill, 0.4);

        var text = isDark ? l3 : d2;
        var textSecondary = isDark ? l2 : d2;
        var textTertiary = isDark ? l1 : d1;

        var fillBrush = new SolidColorBrush(fill);
        var fillSecBrush = new SolidColorBrush(fillSecondary);
        var fillTerBrush = new SolidColorBrush(fillTertiary);
        var fillDisBrush = new SolidColorBrush(fillDisabled);

        // General accent brushes
        dict["AccentFillColorDefaultBrush"] = fillBrush;
        dict["AccentFillColorSecondaryBrush"] = fillSecBrush;
        dict["AccentFillColorTertiaryBrush"] = fillTerBrush;
        dict["AccentFillColorDisabledBrush"] = fillDisBrush;
        dict["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(text);
        dict["AccentTextFillColorSecondaryBrush"] = new SolidColorBrush(textSecondary);
        dict["AccentTextFillColorTertiaryBrush"] = new SolidColorBrush(textTertiary);
        dict["AccentTextFillColorDisabledBrush"] = fillDisBrush;

        // Slider-specific lightweight styling resources
        dict["SliderTrackValueFill"] = fillBrush;
        dict["SliderTrackValueFillPointerOver"] = fillSecBrush;
        dict["SliderTrackValueFillPressed"] = fillTerBrush;
        dict["SliderTrackValueFillDisabled"] = fillDisBrush;
        dict["SliderThumbBackground"] = fillBrush;
        dict["SliderThumbBackgroundPointerOver"] = fillSecBrush;
        dict["SliderThumbBackgroundPressed"] = fillTerBrush;
        dict["SliderThumbBackgroundDisabled"] = fillDisBrush;

        // ToggleSwitch
        dict["ToggleSwitchFillOnBrush"] = fillBrush;
        dict["ToggleSwitchFillOnBrushPointerOver"] = fillSecBrush;
        dict["ToggleSwitchFillOnBrushPressed"] = fillTerBrush;
        dict["ToggleSwitchFillOnBrushDisabled"] = fillDisBrush;
    }

    private static Windows.UI.Color WithAlpha(Windows.UI.Color c, double alpha)
    {
        return Windows.UI.Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B);
    }

    internal static Windows.UI.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            return new Windows.UI.Color { A = 255, R = 0, G = 120, B = 212 };
        return new Windows.UI.Color
        {
            A = 255,
            R = Convert.ToByte(hex[..2], 16),
            G = Convert.ToByte(hex[2..4], 16),
            B = Convert.ToByte(hex[4..6], 16)
        };
    }

    private static Windows.UI.Color Lighten(Windows.UI.Color c, double amount)
    {
        var (h, s, l) = RgbToHsl(c.R, c.G, c.B);
        l = Math.Min(1.0, l + amount);
        return HslToRgb(h, s, l, c.A);
    }

    private static Windows.UI.Color Darken(Windows.UI.Color c, double amount)
    {
        var (h, s, l) = RgbToHsl(c.R, c.G, c.B);
        l = Math.Max(0.0, l - amount);
        return HslToRgb(h, s, l, c.A);
    }

    private static (double h, double s, double l) RgbToHsl(byte r, byte g, byte b)
    {
        double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double h = 0, s = 0, l = (max + min) / 2;

        if (max != min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == rd) h = ((gd - bd) / d + (gd < bd ? 6 : 0)) / 6;
            else if (max == gd) h = ((bd - rd) / d + 2) / 6;
            else h = ((rd - gd) / d + 4) / 6;
        }
        return (h, s, l);
    }

    private static Windows.UI.Color HslToRgb(double h, double s, double l, byte a = 255)
    {
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3);
        }
        return Windows.UI.Color.FromArgb(a, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
