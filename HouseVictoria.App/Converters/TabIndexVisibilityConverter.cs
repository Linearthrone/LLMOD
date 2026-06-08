using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HouseVictoria.App.Converters
{
    /// <summary>
    /// Shows content when selected tab index matches any converter parameter index.
    /// Parameter format: "0|1|3"
    /// </summary>
    public class TabIndexVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not int selectedIndex)
                return Visibility.Visible;

            var paramText = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(paramText))
                return Visibility.Visible;

            var allowed = paramText
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var idx) ? idx : -1)
                .ToHashSet();

            return allowed.Contains(selectedIndex) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
