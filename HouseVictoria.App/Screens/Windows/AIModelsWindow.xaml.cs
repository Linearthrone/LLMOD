using System.Windows;
using System.Windows.Controls;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class AIModelsWindow : Window
    {
        public AIModelsWindowViewModel ViewModel { get; }
        
        private bool _isMinimized = false;
        private bool _isClosed = false;
        private double _savedWidth;
        private double _savedHeight;
        private double _savedLeft;
        private double _savedTop;

        public AIModelsWindow()
        {
            InitializeComponent();
            
            var aiService = App.GetService<IAIService>();
            var persistenceService = App.GetService<IPersistenceService>();
            var memoryService = App.GetService<IMemoryService>();
            var mcpService = App.GetService<IMCPService>();
            var appConfig = App.GetService<AppConfig>();
            
            ViewModel = new AIModelsWindowViewModel(aiService, persistenceService, memoryService, appConfig, mcpService);
            DataContext = ViewModel;
            
            Loaded += AIModelsWindow_Loaded;
            
            Closed += (s, e) => { _isClosed = true; };
        }

        public bool IsClosed() => _isClosed;
        public bool IsMinimized() => _isMinimized;
        
        public void RestoreFromMinimized()
        {
            WindowHelper.RestoreFromTray(this, ref _isMinimized, _savedWidth, _savedHeight, _savedLeft, _savedTop);
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

        private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox)
            {
                // Update the property when an item is selected from dropdown
                if (comboBox.SelectedItem is string selectedModel)
                {
                    ViewModel.NewPersonaModel = selectedModel;
                    System.Diagnostics.Debug.WriteLine($"Model selected from dropdown: {selectedModel}");
                }
                // Also update when text is typed (for editable ComboBox)
                else if (!string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    ViewModel.NewPersonaModel = comboBox.Text;
                    System.Diagnostics.Debug.WriteLine($"Model text changed: {comboBox.Text}");
                }
            }
        }

        private void LoadModelLogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }

        private void AIModelsWindow_Loaded(object sender, RoutedEventArgs e)
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
                    System.Diagnostics.Debug.WriteLine($"Error fitting AIModelsWindow on screen: {ex.Message}");
                }
            }));
        }
    }
}
