using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HouseVictoria.App.Converters
{
    /// <summary>Converts #RRGGBB hex strings to frozen brushes on the UI thread (binding target).</summary>
    public sealed class HexToBrushConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hex = value as string;
            if (string.IsNullOrWhiteSpace(hex))
                return Brushes.Cyan;

            try
            {
                var converted = ColorConverter.ConvertFromString(hex.Trim());
                if (converted is not Color color)
                    return Brushes.Cyan;

                var brush = new SolidColorBrush(color);
                if (brush.CanFreeze)
                    brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Cyan;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
