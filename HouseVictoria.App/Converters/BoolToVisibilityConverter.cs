using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HouseVictoria.App.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if parameter is "Inverse" to invert the logic
                if (parameter is string param && param == "Inverse")
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Handle string values (for PullModelStatus visibility)
            if (value is string strValue)
            {
                return string.IsNullOrWhiteSpace(strValue) ? Visibility.Collapsed : Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }
}
