using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HouseVictoria.App.Screens.Trays
{
    public partial class CognitionVitalsDrawer : UserControl
    {
        private readonly CognitionVitalsDrawerViewModel _viewModel;
        private readonly DispatcherTimer _pollTimer;
        private bool _pollInProgress;
        private bool _isUnloaded;

        public CognitionVitalsDrawer()
        {
            InitializeComponent();
            _viewModel = new CognitionVitalsDrawerViewModel(DrawerPanel);
            DataContext = _viewModel;

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        private async void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (_isUnloaded || _pollInProgress)
                return;

            _pollInProgress = true;
            try
            {
                await _viewModel.PollTradingVitalsAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cognition vitals poll error: {ex.Message}");
            }
            finally
            {
                _pollInProgress = false;
            }
        }

        private void Control_Unloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _pollTimer.Stop();
            _viewModel.Dispose();
        }

        private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var source = e.OriginalSource as DependencyObject;
                if (source != null && (IsDescendantOf(source, CollapsedTab) || IsDescendantOf(source, DrawerPanel)))
                    return;

                _viewModel.IsDrawerOpen = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CognitionVitalsDrawer mouse down: {ex.Message}");
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
        {
            if (ancestor == null)
                return false;

            var current = child;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                current = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return false;
        }
    }
}
