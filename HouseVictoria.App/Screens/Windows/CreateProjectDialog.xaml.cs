using System.Windows;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class CreateProjectDialog : Window
    {
        public CreateProjectDialogViewModel ViewModel { get; } = null!;
        public Project? CreatedProject { get; private set; }

        public CreateProjectDialog()
        {
            InitializeComponent();
            
            try
            {
                var projectManagementService = App.GetService<IProjectManagementService>();
                var persistenceService = App.GetService<IPersistenceService>();
                
                ViewModel = new CreateProjectDialogViewModel(projectManagementService, persistenceService);
                DataContext = ViewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in CreateProjectDialog constructor: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var project = await ViewModel.SaveProjectAsync();
            if (project != null)
            {
                CreatedProject = project;
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
