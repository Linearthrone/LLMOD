using System.Windows;

namespace HouseVictoria.App.Services
{
    /// <summary>
    /// Manages application color scheme/theming. Applies theme by merging the selected theme's ResourceDictionary.
    /// </summary>
    public static class ThemeManager
    {
        public const string ThemeIndexKey = "ThemeIndex";
        private const int ThemeDictMergeIndex = 5; // Index after MaterialDesign, Buttons, Controls, GlassEffect, OverlayStyles

        /// <summary>
        /// Available theme display names for the dropdown.
        /// </summary>
        public static readonly IReadOnlyList<ThemeInfo> Themes = new[]
        {
            new ThemeInfo("CyanBlueDark", "Cyan Blue (Dark)", false),
            new ThemeInfo("EmeraldDark", "Emerald (Dark)", false),
            new ThemeInfo("AmberDark", "Amber (Dark)", false),
            new ThemeInfo("VioletDark", "Violet (Dark)", false),
            new ThemeInfo("RoseDark", "Rose (Dark)", false),
            new ThemeInfo("CyanBlueLight", "Cyan Blue (Light)", true),
            new ThemeInfo("EmeraldLight", "Emerald (Light)", true),
            new ThemeInfo("AmberLight", "Amber (Light)", true),
            new ThemeInfo("VioletLight", "Violet (Light)", true),
            new ThemeInfo("RoseLight", "Rose (Light)", true),
        };

        /// <summary>
        /// Applies the theme by ID. Merges the theme's ResourceDictionary into Application.Resources.
        /// </summary>
        public static void ApplyTheme(string themeId)
        {
            if (Application.Current?.Resources?.MergedDictionaries == null)
                return;

            var dicts = Application.Current.Resources.MergedDictionaries;
            var normalizedId = NormalizeThemeId(themeId);

            // Remove existing theme dict if present (we add it last)
            while (dicts.Count > ThemeDictMergeIndex)
            {
                dicts.RemoveAt(dicts.Count - 1);
            }

            try
            {
                var uri = new Uri($"pack://application:,,,/HouseVictoria.App;component/Themes/Theme{normalizedId}.xaml");
                var themeDict = new ResourceDictionary { Source = uri };
                dicts.Add(themeDict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Could not load theme '{normalizedId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a theme ResourceDictionary for preview (e.g. in settings). Does not apply to the app.
        /// </summary>
        public static ResourceDictionary? LoadThemeForPreview(string themeId)
        {
            var normalizedId = NormalizeThemeId(themeId);
            try
            {
                var uri = new Uri($"pack://application:,,,/HouseVictoria.App;component/Themes/Theme{normalizedId}.xaml");
                return new ResourceDictionary { Source = uri };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets theme ID by index (0-based). Returns default if index is out of range.
        /// </summary>
        public static string GetThemeIdByIndex(int index)
        {
            if (index >= 0 && index < Themes.Count)
                return Themes[index].Id;
            return Themes[0].Id;
        }

        /// <summary>
        /// Gets theme index by ID. Returns 0 if not found.
        /// </summary>
        public static int GetThemeIndexById(string themeId)
        {
            var id = NormalizeThemeId(themeId);
            for (int i = 0; i < Themes.Count; i++)
            {
                if (string.Equals(Themes[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        private static string NormalizeThemeId(string? themeId)
        {
            if (string.IsNullOrWhiteSpace(themeId))
                return Themes[0].Id;
            var id = themeId.Trim();
            if (Themes.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)))
                return Themes.First(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)).Id;
            return Themes[0].Id;
        }
    }

    public record ThemeInfo(string Id, string DisplayName, bool IsLight);
}
