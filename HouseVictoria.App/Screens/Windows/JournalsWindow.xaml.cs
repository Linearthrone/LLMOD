using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.Converters;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class JournalsWindow : Window
    {
        public JournalsWindowViewModel ViewModel { get; }

        private bool _isMinimized;
        private bool _isClosed;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;

        public JournalsWindow()
        {
            InitializeComponent();
            Resources["BoolToVisibilityConverter"] = new BoolToVisibilityConverter();

            var journalService = App.GetService<IJournalService>();
            ViewModel = new JournalsWindowViewModel(journalService);
            DataContext = ViewModel;

            Loaded += JournalsWindow_Loaded;
            Closed += (_, _) => { _isClosed = true; };
        }

        public bool IsClosed() => _isClosed;
        public bool IsMinimized() => _isMinimized;

        public void RestoreFromMinimized()
        {
            WindowHelper.RestoreFromTray(this, ref _isMinimized, _savedWidth, _savedHeight, _savedLeft, _savedTop);
        }

        private async void JournalsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _savedWidth = Width;
            _savedHeight = Height;
            _savedLeft = Left;
            _savedTop = Top;
            await ViewModel.LoadJournalsAsync();
        }

        private void JournalCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string journalId)
            {
                ViewModel.OpenJournalCommand.Execute(journalId);
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowHelper.MinimizeToTray(this, ref _isMinimized, ref _savedWidth, ref _savedHeight, ref _savedLeft, ref _savedTop);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
