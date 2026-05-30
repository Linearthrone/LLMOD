using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.Converters;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class JournalReaderWindow : Window
    {
        public JournalReaderWindowViewModel ViewModel { get; }

        public JournalReaderWindow(string journalId)
        {
            InitializeComponent();
            Resources["BoolToVisibilityConverter"] = new BoolToVisibilityConverter();

            var journalService = App.GetService<IJournalService>();
            ViewModel = new JournalReaderWindowViewModel(journalId, journalService);
            DataContext = ViewModel;

            Loaded += JournalReaderWindow_Loaded;
        }

        private async void JournalReaderWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Title = "Journal";
            await ViewModel.LoadAsync();
            Title = ViewModel.Title;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
