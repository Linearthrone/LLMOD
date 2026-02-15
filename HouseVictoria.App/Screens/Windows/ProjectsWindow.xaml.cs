using System;
using System.Windows;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.App.Converters;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class ProjectsWindow : Window
    {
        private readonly IProjectManagementService _projectManagementService;

        public ProjectsWindowViewModel ViewModel { get; }
        
        private bool _isMinimized = false;
        private bool _isClosed = false;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;

        public ProjectsWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ProjectsWindow XAML: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing Projects Window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            try
            {
                InitializeConverters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing converters: {ex.Message}");
                // Continue without converters - window will still work
            }
            
            try
            {
                _projectManagementService = App.GetService<IProjectManagementService>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get IProjectManagementService: {ex.Message}");
                _projectManagementService = new HouseVictoria.Services.ProjectManagement.ProjectManagementService();
            }
            
            try
            {
                ViewModel = new ProjectsWindowViewModel(_projectManagementService);
                DataContext = ViewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating ProjectsWindowViewModel: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error initializing Projects Window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            Loaded += ProjectsWindow_Loaded;
            
            Closed += (s, e) => { _isClosed = true; };
        }

        public bool IsClosed() => _isClosed;
        public bool IsMinimized() => _isMinimized;
        
        public void RestoreFromMinimized()
        {
            WindowHelper.RestoreFromTray(this, ref _isMinimized, _savedWidth, _savedHeight, _savedLeft, _savedTop);
        }

        private void InitializeConverters()
        {
            var boolToVisibilityConverter = new BoolToVisibilityConverter();
            Resources["BoolToVisibilityConverter"] = boolToVisibilityConverter;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowHelper.MinimizeToTray(this, ref _isMinimized, ref _savedWidth, ref _savedHeight, ref _savedLeft, ref _savedTop);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private async void ProjectCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && element.Tag is string projectId)
            {
                await ViewModel.OpenProjectDetailAsync(projectId);
                
                // Refresh projects list after detail dialog closes (in case project was updated or deleted)
                _ = ViewModel.LoadProjectsAsync();
            }
        }

        private void ProjectsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _savedWidth = Width;
                _savedHeight = Height;
                _savedLeft = Left;
                _savedTop = Top;

                // Ensure window fits on screen (preserves XAML sizes, adjusts if off-screen or too large)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    try
                    {
                        WindowHelper.EnsureWindowFitsOnScreen(this);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error fitting ProjectsWindow on screen: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Loaded event: {ex.Message}");
            }
        }
    }
}
