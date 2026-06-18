using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HouseVictoria.App.Converters
{
    /// <summary>
    /// Shows content when SelectedSettingsCategory matches the converter parameter id.
    /// </summary>
    public class SettingsCategoryVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var selected = value as string;
            var categoryId = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(categoryId))
                return Visibility.Visible;

            return string.Equals(selected, categoryId, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
