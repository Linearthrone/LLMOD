using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class EditSystemPromptDialog : Window, INotifyPropertyChanged
    {
        private string _systemPrompt = string.Empty;
        private string _piperVoiceId = string.Empty;
        private string _contactName = "Unknown";
        private string _avatarModelPath = string.Empty;
        private double _avatarVoiceSpeed = 1.0;
        private double _avatarVoicePitch = 1.0;
        private bool _shareUserBasics = true;
        private bool _shareOwnMemories = true;
        private bool _shareOwnDataBank = true;
        private bool _shareHouseJournals;
        private bool _shareOtherPersonaMemories;
        private bool _shareSharedDataBanks;

        public AIContact Contact { get; set; }
        public PersonaKnowledgeSharing KnowledgeSharing { get; private set; } = new();
        public string ContactName
        {
            get => _contactName;
            private set
            {
                _contactName = value;
                OnPropertyChanged();
            }
        }
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

        public string AvatarModelPath
        {
            get => _avatarModelPath;
            set { _avatarModelPath = value ?? string.Empty; OnPropertyChanged(); }
        }

        public double AvatarVoiceSpeed
        {
            get => _avatarVoiceSpeed;
            set { _avatarVoiceSpeed = value; OnPropertyChanged(); }
        }

        public double AvatarVoicePitch
        {
            get => _avatarVoicePitch;
            set { _avatarVoicePitch = value; OnPropertyChanged(); }
        }

        public bool ShareUserBasics
        {
            get => _shareUserBasics;
            set { _shareUserBasics = value; OnPropertyChanged(); }
        }

        public bool ShareOwnMemories
        {
            get => _shareOwnMemories;
            set { _shareOwnMemories = value; OnPropertyChanged(); }
        }

        public bool ShareOwnDataBank
        {
            get => _shareOwnDataBank;
            set { _shareOwnDataBank = value; OnPropertyChanged(); }
        }

        public bool ShareHouseJournals
        {
            get => _shareHouseJournals;
            set { _shareHouseJournals = value; OnPropertyChanged(); }
        }

        public bool ShareOtherPersonaMemories
        {
            get => _shareOtherPersonaMemories;
            set { _shareOtherPersonaMemories = value; OnPropertyChanged(); }
        }

        public bool ShareSharedDataBanks
        {
            get => _shareSharedDataBanks;
            set { _shareSharedDataBanks = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public EditSystemPromptDialog(AIContact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));

            Contact = contact;
            ContactName = contact.Name ?? "Unknown";
            SystemPrompt = contact.SystemPrompt ?? string.Empty;
            PiperVoiceId = contact.PiperVoiceId ?? string.Empty;
            AvatarModelPath = contact.AvatarModelPath ?? string.Empty;
            AvatarVoiceSpeed = contact.AvatarVoiceSpeed;
            AvatarVoicePitch = contact.AvatarVoicePitch;

            var sharing = PersonaKnowledgeSharing.Resolve(contact);
            ShareUserBasics = sharing.ShareUserBasics;
            ShareOwnMemories = sharing.ShareOwnMemories;
            ShareOwnDataBank = sharing.ShareOwnDataBank;
            ShareHouseJournals = sharing.ShareHouseJournals;
            ShareOtherPersonaMemories = sharing.ShareOtherPersonaMemories;
            ShareSharedDataBanks = sharing.ShareSharedDataBanks;

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing EditSystemPromptDialog XAML: {ex.Message}\n{ex.StackTrace}");
                throw;
            }

            DataContext = this;
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
                AvailablePiperVoices.Clear();
                foreach (var v in HouseVictoria.Services.Voice.VoiceCatalog.GetKokoroVoices())
                    AvailablePiperVoices.Add(v);
            }
            catch { }
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            KnowledgeSharing = new PersonaKnowledgeSharing
            {
                ShareUserBasics = ShareUserBasics,
                ShareOwnMemories = ShareOwnMemories,
                ShareOwnDataBank = ShareOwnDataBank,
                ShareHouseJournals = ShareHouseJournals,
                ShareOtherPersonaMemories = ShareOtherPersonaMemories,
                ShareSharedDataBanks = ShareSharedDataBanks,
                IsConfigured = true
            };
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
