using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

        private void RootGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Auto-collapse to pull-tab when the mouse leaves the tray area.
            // NOTE: expanding/collapsing changes layout bounds; defer the check to avoid
            // a "toggle open then immediately close" race during re-measure.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (RootGrid.IsMouseOver)
                    return;

                if (DataContext is MainTrayViewModel vm)
                    vm.IsExpanded = false;
            }));
        }
    }
}
