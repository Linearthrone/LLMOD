using System;
using System.Globalization;
using System.Windows.Data;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Converters
{
    public class CallStateToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CallState callState)
            {
                return callState switch
                {
                    CallState.Outgoing => "Calling...",
                    CallState.Incoming => "Incoming call...",
                    CallState.Connected => "Call connected",
                    CallState.Ended => "Call ended",
                    CallState.Missed => "Missed call",
                    _ => ""
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
