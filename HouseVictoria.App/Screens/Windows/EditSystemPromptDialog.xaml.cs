using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class EditSystemPromptDialog : Window, INotifyPropertyChanged
    {
        private string _systemPrompt = string.Empty;
        private string _piperVoiceId = string.Empty;

        public AIContact Contact { get; set; }
        public string ContactName => Contact?.Name ?? "Unknown";
        public ObservableCollection<string> AvailablePiperVoices { get; } = new();
        
        public string SystemPrompt
        {
            get => _systemPrompt;
            set
            {
                _systemPrompt = value;
                OnPropertyChanged();
            }
        }

        public string PiperVoiceId
        {
            get => _piperVoiceId;
            set
            {
                _piperVoiceId = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public EditSystemPromptDialog(AIContact contact)
        {
            InitializeComponent();
            Contact = contact ?? throw new ArgumentNullException(nameof(contact));
            SystemPrompt = contact.SystemPrompt ?? string.Empty;
            PiperVoiceId = contact.PiperVoiceId ?? string.Empty;
            DataContext = this;
            OnPropertyChanged(nameof(ContactName));
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            try
            {
                await LoadPiperVoicesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EditSystemPromptDialog LoadPiperVoicesAsync: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadPiperVoicesAsync()
        {
            try
            {
                var ttsService = App.ServiceProvider?.GetService<ITTSService>();
                if (ttsService != null)
                {
                    var voices = await ttsService.GetAvailablePiperVoicesAsync();
                    AvailablePiperVoices.Clear();
                    foreach (var v in voices)
                        AvailablePiperVoices.Add(v);
                }
            }
            catch { }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
