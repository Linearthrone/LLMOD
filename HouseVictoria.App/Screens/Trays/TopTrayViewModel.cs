using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Utils;
using HouseVictoria.App.Screens.Windows;

namespace HouseVictoria.App.Screens.Trays
{
    public class TopTrayViewModel : ObservableObject
    {
        private readonly IEventAggregator _eventAggregator;

        public ICommand OpenProjectsCommand { get; }
        public ICommand OpenDataBankManagementCommand { get; }

        public TopTrayViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            OpenProjectsCommand = new RelayCommand(() => ShowWindow("Projects"));
            OpenDataBankManagementCommand = new RelayCommand(() => ShowWindow("DataBankManagement"));
        }

        private void ShowWindow(string windowType)
        {
            _eventAggregator.Publish(new ShowWindowEvent { WindowType = windowType });
        }
    }
}
