using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LLMOD.ViewModels; // <--- CRITICAL: The Converter needs this to know what 'MonitorView' is
using LLMOD.SystemMonitorViewModels;

namespace LLMOD.Helpers
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)(value ?? false) ? Colors.LimeGreen : Colors.Red;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TrendConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)(value ?? false) ? "▲" : "▼";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseTrayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Added a try-catch safety
            if (value is MonitorView v)
            {
                return (v != MonitorView.Tray && v != MonitorView.GaugesOnly) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible; // Default fallback
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}