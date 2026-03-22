using System;
using System.Globalization;
using System.Windows.Data;
using HouseVictoria.App.Screens.Windows;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Converters
{
    /// <summary>
    /// Converts (message, viewModel) to whether the message is selected.
    /// Use with MultiBinding: bind Message and SelectedMessageCount (to trigger refresh).
    /// ConverterParameter is the ViewModel.
    /// </summary>
    public class MessageSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3) return false;
            var message = values[0] as ConversationMessage;
            if (message == null) return false;
            var vm = values[2] as SMSMMSWindowViewModel;
            if (vm == null) return false;
            return vm.IsMessageSelected(message.Id);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
