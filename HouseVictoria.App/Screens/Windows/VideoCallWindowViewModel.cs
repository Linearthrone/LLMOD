using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class VideoCallWindowViewModel : ObservableObject
    {
        private readonly ICommunicationService _communicationService;
        private readonly DispatcherTimer _callTimer;
        private string _contactId = string.Empty;
        private string _contactName = "Unknown";
        private string? _conversationId;
        private CallState _callState = CallState.None;
        private TimeSpan _callDuration = TimeSpan.Zero;
        private bool _isMuted;
        private bool _isVideoEnabled = true;
        private bool _isSpeakerEnabled = true;

        public string TitleText => string.IsNullOrWhiteSpace(_contactName) ? "Video Call" : $"Video Call - {_contactName}";
        public string ContactName => _contactName;

        public string CallStatusText => _callState switch
        {
            CallState.Outgoing => "Calling...",
            CallState.Incoming => "Incoming call...",
            CallState.Connected => "Call connected",
            CallState.Ended => "Call ended",
            CallState.Missed => "Missed call",
            _ => "No active call"
        };

        public string CallDurationText => _callState == CallState.Connected
            ? _callDuration.ToString(@"hh\:mm\:ss")
            : "00:00:00";

        public string RemoteVideoStatusText => _callState switch
        {
            CallState.Connected => "Connected. Waiting for video stream...",
            CallState.Outgoing => "Connecting to contact...",
            CallState.Incoming => "Incoming call...",
            CallState.Ended => "Call ended",
            _ => "No active call"
        };

        public string LocalPreviewStatusText => _isVideoEnabled
            ? "Local preview (placeholder)"
            : "Camera off";

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (SetProperty(ref _isMuted, value))
                {
                    OnPropertyChanged(nameof(MuteButtonText));
                }
            }
        }

        public bool IsVideoEnabled
        {
            get => _isVideoEnabled;
            set
            {
                if (SetProperty(ref _isVideoEnabled, value))
                {
                    OnPropertyChanged(nameof(VideoButtonText));
                    OnPropertyChanged(nameof(LocalPreviewStatusText));
                }
            }
        }

        public bool IsSpeakerEnabled
        {
            get => _isSpeakerEnabled;
            set
            {
                if (SetProperty(ref _isSpeakerEnabled, value))
                {
                    OnPropertyChanged(nameof(SpeakerButtonText));
                }
            }
        }

        public string MuteButtonText => _isMuted ? "Unmute" : "Mute";
        public string VideoButtonText => _isVideoEnabled ? "Video On" : "Video Off";
        public string SpeakerButtonText => _isSpeakerEnabled ? "Speaker On" : "Speaker Off";

        public ICommand ToggleMuteCommand { get; }
        public ICommand ToggleVideoCommand { get; }
        public ICommand ToggleSpeakerCommand { get; }
        public ICommand HangUpCommand { get; }

        public VideoCallWindowViewModel(ICommunicationService communicationService, VideoCallContext? context = null)
        {
            _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));

            ToggleMuteCommand = new RelayCommand(() => IsMuted = !IsMuted);
            ToggleVideoCommand = new RelayCommand(() => IsVideoEnabled = !IsVideoEnabled);
            ToggleSpeakerCommand = new RelayCommand(() => IsSpeakerEnabled = !IsSpeakerEnabled);
            HangUpCommand = new RelayCommand(async () => await HangUpAsync());

            _callTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _callTimer.Tick += (_, _) =>
            {
                _callDuration = _callDuration.Add(TimeSpan.FromSeconds(1));
                OnPropertyChanged(nameof(CallDurationText));
            };

            _communicationService.CallStateChanged += OnCallStateChanged;

            if (context != null)
            {
                UpdateContext(context);
            }
        }

        public void UpdateContext(VideoCallContext? context)
        {
            if (context == null) return;

            _contactId = context.ContactId ?? string.Empty;
            _contactName = context.ContactName ?? "Unknown";
            _conversationId = context.ConversationId;

            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(ContactName));

            if (string.IsNullOrWhiteSpace(_conversationId))
            {
                _ = TryResolveConversationIdAsync();
            }
        }

        private async Task TryResolveConversationIdAsync()
        {
            if (string.IsNullOrWhiteSpace(_contactId))
                return;

            try
            {
                var conversations = await _communicationService.GetConversationsAsync();
                var conversation = conversations.FirstOrDefault(c => c.ContactId == _contactId);
                if (conversation != null)
                {
                    _conversationId = conversation.Id;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resolving conversation id: {ex.Message}");
            }
        }

        private void OnCallStateChanged(object? sender, CallStateChangedEventArgs e)
        {
            if (!IsMatchingCall(e.ConversationId))
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _callState = e.State;
                if (_callState == CallState.Connected)
                {
                    _callDuration = TimeSpan.Zero;
                    _callTimer.Start();
                }
                else if (_callState == CallState.Ended || _callState == CallState.Missed)
                {
                    _callTimer.Stop();
                }

                OnPropertyChanged(nameof(CallStatusText));
                OnPropertyChanged(nameof(CallDurationText));
                OnPropertyChanged(nameof(RemoteVideoStatusText));
            });
        }

        private bool IsMatchingCall(string conversationId)
        {
            if (!string.IsNullOrWhiteSpace(_conversationId))
            {
                return string.Equals(conversationId, _conversationId, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(_contactId))
            {
                return conversationId.Contains(_contactId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(conversationId, _contactId, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private async Task HangUpAsync()
        {
            if (string.IsNullOrWhiteSpace(_contactId))
            {
                MessageBox.Show("No active call to hang up.", "Call", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var conversationId = _conversationId ?? $"conv-{_contactId}-temp";
                await _communicationService.EndVideoCallAsync(conversationId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ending call: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            _communicationService.CallStateChanged -= OnCallStateChanged;
            _callTimer.Stop();
        }
    }
}
