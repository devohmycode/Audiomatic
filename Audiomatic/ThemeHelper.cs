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
        if (CurrentTheme != ElementTheme.Default)
        {
            var themeKeys = CurrentTheme == ElementTheme.Dark ? DarkKeys : LightKeys;
            foreach (var themeKey in themeKeys)
            {
                var result = FindInThemeDictionaries(Application.Current.Resources, key, themeKey);
                if (result != null) return result;
            }
        }
        return (Brush)Application.Current.Resources[key];
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
}
