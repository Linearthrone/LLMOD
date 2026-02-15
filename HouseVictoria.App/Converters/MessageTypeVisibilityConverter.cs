using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Converters
{
    public class MessageTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageType messageType && parameter is string targetTypeStr)
            {
                if (Enum.TryParse<MessageType>(targetTypeStr, out var targetMessageType))
                {
                    return messageType == targetMessageType ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
