using System;
using System.Windows;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class ProjectDetailDialog : Window
    {
        public ProjectDetailDialogViewModel ViewModel { get; }
        public bool ProjectWasDeleted { get; private set; }
        public bool ProjectWasUpdated { get; private set; }

        public ProjectDetailDialog(Project project, IProjectManagementService projectManagementService, IPersistenceService persistenceService)
        {
            InitializeComponent();
            
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            try
            {
                ViewModel = new ProjectDetailDialogViewModel(project, projectManagementService, persistenceService);
                DataContext = ViewModel;
                
                // Subscribe to events
                ViewModel.ProjectDeleted += (s, e) => 
                { 
                    ProjectWasDeleted = true; 
                    DialogResult = false; 
                    Close(); 
                };
                ViewModel.ProjectSaved += (s, e) => 
                { 
                    ProjectWasUpdated = true; 
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Project Detail Dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in ProjectDetailDialog constructor: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsEditable)
            {
                var result = MessageBox.Show("You have unsaved changes. Are you sure you want to close?", "Unsaved Changes", 
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            DialogResult = false;
            Close();
        }
    }
}
