using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Utils;
using HouseVictoria.App.Screens.Windows;

namespace HouseVictoria.App.Screens.Trays
{
    public class MainTrayViewModel : ObservableObject
    {
        private readonly IEventAggregator _eventAggregator;

        private bool _isExpanded;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public ICommand OpenSMSCommand { get; }
        public ICommand OpenAIModelsCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand CloseTrayCommand { get; }

        public MainTrayViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            OpenSMSCommand = new RelayCommand(() =>
            {
                ShowWindow("SMS/MMS");
                IsExpanded = false;
            });
            OpenAIModelsCommand = new RelayCommand(() =>
            {
                ShowWindow("AIModels");
                IsExpanded = false;
            });
            OpenSettingsCommand = new RelayCommand(() =>
            {
                ShowWindow("Settings");
                IsExpanded = false;
            });
            CloseTrayCommand = new RelayCommand(() => ToggleTray());

            // Start expanded; user can collapse to a slim pull-tab.
            IsExpanded = true;
        }

        private void ToggleTray()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainTrayViewModel: Toggling MainTray visibility");
                _eventAggregator.Publish(new ToggleTrayEvent { TrayName = "MainTray" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error publishing ToggleTrayEvent: {ex.Message}");
            }
        }

        private void ShowWindow(string windowType)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"MainTrayViewModel: Showing window '{windowType}'");
            _eventAggregator.Publish(new ShowWindowEvent { WindowType = windowType });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error publishing ShowWindowEvent for '{windowType}': {ex.Message}");
            }
        }
    }
}
