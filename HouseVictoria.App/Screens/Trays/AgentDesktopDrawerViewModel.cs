using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Trays
{
    public sealed class AgentDesktopActivityLineViewModel
    {
        public string Timestamp { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public string KindLabel { get; init; } = string.Empty;
    }

    public class AgentDesktopDrawerViewModel : ObservableObject, IDisposable
    {
        private readonly IAgentDesktopMonitorService? _monitor;
        private readonly Dispatcher _dispatcher;

        private ImageSource? _liveFrame;
        private string _statusText = "Open this tab to see a live preview";
        private bool _isWatching;
        private bool _hasLiveFrame;
        private string _liveSourceLabel = string.Empty;

        public ObservableCollection<AgentDesktopActivityLineViewModel> ActivityLines { get; } = new();

        public ImageSource? LiveFrame
        {
            get => _liveFrame;
            private set
            {
                if (SetProperty(ref _liveFrame, value))
                    HasLiveFrame = value != null;
            }
        }

        public bool HasLiveFrame
        {
            get => _hasLiveFrame;
            private set => SetProperty(ref _hasLiveFrame, value);
        }

        public string LiveSourceLabel
        {
            get => _liveSourceLabel;
            private set => SetProperty(ref _liveSourceLabel, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public bool IsWatching
        {
            get => _isWatching;
            private set => SetProperty(ref _isWatching, value);
        }

        public bool ShareScreenWithAI
        {
            get => _monitor?.ShareScreenWithAI ?? false;
            set
            {
                if (_monitor == null || _monitor.ShareScreenWithAI == value)
                    return;
                _monitor.ShareScreenWithAI = value;
                OnPropertyChanged();
            }
        }

        public bool AllowComputerControl
        {
            get => _monitor?.AllowComputerControl ?? false;
            set
            {
                if (_monitor == null || _monitor.AllowComputerControl == value)
                    return;
                _monitor.AllowComputerControl = value;
                OnPropertyChanged();
            }
        }

        public ICommand ClearActivityCommand { get; }

        public AgentDesktopDrawerViewModel(IAgentDesktopMonitorService? monitor = null)
        {
            _monitor = monitor ?? TryGetMonitor();
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            ClearActivityCommand = new RelayCommand(() => ActivityLines.Clear());

            if (_monitor != null)
            {
                _monitor.SessionChanged += Monitor_SessionChanged;
                _monitor.ActivityAdded += Monitor_ActivityAdded;
                _monitor.FrameCaptured += Monitor_FrameCaptured;
                _monitor.ShareScreenChanged += Monitor_ShareScreenChanged;
                _monitor.AllowComputerControlChanged += Monitor_AllowComputerControlChanged;
                IsWatching = _monitor.IsSessionActive;
                StatusText = BuildStatusText(_monitor.IsSessionActive, _monitor.ActiveContactName);

                foreach (var entry in _monitor.RecentActivity)
                    ActivityLines.Add(ToLine(entry));

                if (_monitor.LatestFrame != null)
                    LiveFrame = ToImageSource(_monitor.LatestFrame);
            }
        }

        private static IAgentDesktopMonitorService? TryGetMonitor()
        {
            try
            {
                return App.GetService<IAgentDesktopMonitorService>();
            }
            catch
            {
                return null;
            }
        }

        private void Monitor_SessionChanged(object? sender, AgentDesktopSessionChangedEventArgs e)
        {
            _dispatcher.BeginInvoke(() =>
            {
                IsWatching = e.IsActive;
                StatusText = BuildStatusText(e.IsActive, e.ContactName);
            });
        }

        private void Monitor_ActivityAdded(object? sender, AgentDesktopActivityEntry e)
        {
            _dispatcher.BeginInvoke(() =>
            {
                ActivityLines.Add(ToLine(e));
                while (ActivityLines.Count > 80)
                    ActivityLines.RemoveAt(0);
            });
        }

        private void Monitor_FrameCaptured(object? sender, AgentDesktopFrame frame)
        {
            _dispatcher.BeginInvoke(() =>
            {
                try
                {
                    LiveFrame = ToImageSource(frame);
                    LiveSourceLabel = frame.IsBrowserTab
                        ? $"Browser tab — {frame.SourceLabel}"
                        : frame.SourceLabel ?? "Desktop";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Desktop frame UI update: {ex.Message}");
                }
            });
        }

        private void Monitor_ShareScreenChanged(object? sender, bool value)
        {
            _dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(ShareScreenWithAI)));
        }

        private void Monitor_AllowComputerControlChanged(object? sender, bool value)
        {
            _dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(AllowComputerControl)));
        }

        private static string BuildStatusText(bool active, string? contactName)
        {
            if (!active)
                return "Live preview — tool log fills in when Hermes is working";
            return string.IsNullOrWhiteSpace(contactName)
                ? "Live — AI is using the desktop"
                : $"Live — {contactName} is using the desktop";
        }

        private static AgentDesktopActivityLineViewModel ToLine(AgentDesktopActivityEntry entry)
        {
            return new AgentDesktopActivityLineViewModel
            {
                Timestamp = entry.Timestamp.ToString("HH:mm:ss"),
                Text = entry.Text,
                KindLabel = entry.Kind switch
                {
                    AgentDesktopActivityKind.ToolStart => "tool",
                    AgentDesktopActivityKind.ToolEnd => "done",
                    AgentDesktopActivityKind.Screenshot => "shot",
                    AgentDesktopActivityKind.Error => "err",
                    _ => "info"
                }
            };
        }

        private static BitmapSource ToImageSource(AgentDesktopFrame frame)
        {
            var stride = frame.Width * 4;
            var source = BitmapSource.Create(
                frame.Width,
                frame.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                frame.Bgra32,
                stride);
            source.Freeze();
            return source;
        }

        public void Dispose()
        {
            if (_monitor == null)
                return;

            _monitor.SessionChanged -= Monitor_SessionChanged;
            _monitor.ActivityAdded -= Monitor_ActivityAdded;
            _monitor.FrameCaptured -= Monitor_FrameCaptured;
            _monitor.ShareScreenChanged -= Monitor_ShareScreenChanged;
            _monitor.AllowComputerControlChanged -= Monitor_AllowComputerControlChanged;
        }
    }
}
