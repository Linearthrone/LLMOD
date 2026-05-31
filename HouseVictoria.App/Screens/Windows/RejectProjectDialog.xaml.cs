using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.Converters;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class RejectProjectDialog : Window
    {
        public RejectProjectDialogViewModel ViewModel { get; }
        public AarRejectionFeedback? Feedback { get; private set; }

        public RejectProjectDialog(AfterActionReport report)
        {
            InitializeComponent();
            Resources["BoolToVisibilityConverter"] = new BoolToVisibilityConverter();

            ViewModel = new RejectProjectDialogViewModel(report);
            DataContext = ViewModel;
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Validate())
                return;

            Feedback = ViewModel.BuildFeedback();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
