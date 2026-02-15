using System.Windows.Controls;
using HouseVictoria.App.Screens.Windows;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Utils;

namespace HouseVictoria.App.Screens.Trays
{
    public partial class MainTray : UserControl
    {
        private readonly IEventAggregator _eventAggregator;

        public MainTrayViewModel ViewModel { get; }

        public MainTray()
        {
            InitializeComponent();
            try
            {
                _eventAggregator = App.GetService<IEventAggregator>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get IEventAggregator: {ex.Message}");
                _eventAggregator = new EventAggregator();
            }

            ViewModel = new MainTrayViewModel(_eventAggregator);
            DataContext = ViewModel;
        }
    }
}
