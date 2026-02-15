using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Converters
{
    public class IncomingToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is MessageDirection.Incoming ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
