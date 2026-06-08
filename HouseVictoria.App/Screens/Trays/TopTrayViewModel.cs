using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.App.Screens.Windows;

namespace HouseVictoria.App.Screens.Trays
{
    public class TopTrayViewModel : ObservableObject
    {
        private readonly IEventAggregator _eventAggregator;

        public ICommand OpenProjectsCommand { get; }
        public ICommand OpenDataBankManagementCommand { get; }
        public ICommand OpenJournalsCommand { get; }
        public ICommand OpenAfterActionReportsCommand { get; }

        public TopTrayViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            OpenProjectsCommand = new RelayCommand(() => ShowWindow("Projects"));
            OpenDataBankManagementCommand = new RelayCommand(() => ShowWindow("DataBankManagement"));
            OpenJournalsCommand = new RelayCommand(() => ShowWindow("Journals"));
            OpenAfterActionReportsCommand = new RelayCommand(() => ShowWindow("AAR"));
        }

        private void ShowWindow(string windowType)
        {
            _eventAggregator.Publish(new ShowWindowEvent { WindowType = windowType });
        }
    }
}
