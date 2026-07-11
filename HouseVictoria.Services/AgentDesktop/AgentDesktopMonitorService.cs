using System.IO;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;

namespace HouseVictoria.Services.AgentDesktop
{
    public sealed class AgentDesktopMonitorService : IAgentDesktopMonitorService, IDisposable
    {
        private const int MaxActivityLines = 120;
        private static readonly TimeSpan CaptureInterval = TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan LogPollInterval = TimeSpan.FromMilliseconds(400);

        private readonly AppConfig _config;
        private readonly string _hermesLogPath;
        private readonly object _gate = new();
        private readonly List<AgentDesktopActivityEntry> _activity = new();

        private CancellationTokenSource? _sessionCts;
        private long _logOffset;
        private int _previewRequests;
        private bool _disposed;
        private DateTime _lastCaptureErrorUtc = DateTime.MinValue;
        private string? _lastStreamSourceLabel;
        private bool _streamEnabled;

        public bool IsSessionActive { get; private set; }
        public string? ActiveContactName { get; private set; }
        public AgentDesktopFrame? LatestFrame { get; private set; }

        private bool _shareScreenWithAI;
        public bool ShareScreenWithAI
        {
            get => _shareScreenWithAI;
            set
            {
                if (_shareScreenWithAI == value)
                    return;
                _shareScreenWithAI = value;
                AddActivity(AgentDesktopActivityKind.Info,
                    value ? "Screen sharing ON — Victoria will see your screen with each message."
                          : "Screen sharing OFF.");
                ShareScreenChanged?.Invoke(this, value);
            }
        }
        public event EventHandler<bool>? ShareScreenChanged;

        private bool _allowComputerControl;
        public bool AllowComputerControl
        {
            get => _allowComputerControl;
            set
            {
                if (_allowComputerControl == value)
                    return;
                _allowComputerControl = value;
                _config.AllowComputerControl = value;
                try
                {
                    UserSettingsStore.Save(_config);
                }
                catch (Exception ex)
                {
                    AddActivity(AgentDesktopActivityKind.Error, $"Could not persist control setting: {ex.Message}");
                }
                AddActivity(AgentDesktopActivityKind.Info,
                    value ? "Desktop CONTROL ON — Victoria may act on your screen (computer_use)."
                          : "Desktop control OFF — Victoria can look but not act.");
                AllowComputerControlChanged?.Invoke(this, value);
            }
        }
        public event EventHandler<bool>? AllowComputerControlChanged;

        public byte[]? CaptureScreenPng(int maxWidth = 1280)
        {
            try
            {
                var browser = BrowserCaptureBridgeClient.TryGetLatestStreamFrame(maxWidth, maxAgeSeconds: 5.0)
                    ?? BrowserCaptureBridgeClient.TryCaptureTabNow(maxWidth, timeoutSeconds: 6.0);
                if (browser != null)
                {
                    return PngFromBgra(browser.Value.Bgra32, browser.Value.Width, browser.Value.Height);
                }

                if (!BrowserCaptureBridgeClient.IsHealthy())
                {
                    return WindowsScreenCapture.CapturePrimaryScreenPng(maxWidth)
                        ?? WindowsScreenCapture.CaptureVirtualScreenPng(maxWidth);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
        public IReadOnlyList<AgentDesktopActivityEntry> RecentActivity
        {
            get
            {
                lock (_gate)
                    return _activity.ToList();
            }
        }

        public event EventHandler<AgentDesktopSessionChangedEventArgs>? SessionChanged;
        public event EventHandler<AgentDesktopActivityEntry>? ActivityAdded;
        public event EventHandler<AgentDesktopFrame>? FrameCaptured;

        public AgentDesktopMonitorService(AppConfig config)
        {
            _config = config;
            _allowComputerControl = config.AllowComputerControl;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var mediaRoot = AppDataRootResolver.ResolveDataPath(appDir, config.MediaPath ?? "Media");
            _hermesLogPath = Path.Combine(mediaRoot, "hermes-gateway.log");

            BrowserCaptureBridgeClient.Cast.FrameReceived += OnCastFrameReceived;
        }

        public void BeginSession(string? contactName = null)
        {
            if (_disposed)
                return;

            IsSessionActive = true;
            ActiveContactName = contactName;
            _logOffset = File.Exists(_hermesLogPath) ? new FileInfo(_hermesLogPath).Length : 0;

            AddActivity(AgentDesktopActivityKind.Info, $"Watching desktop for {contactName ?? "AI"}…");
            EnsureCaptureLoop();
            TryPublishBrowserFrame(forceOnDemand: true);

            SessionChanged?.Invoke(this, new AgentDesktopSessionChangedEventArgs
            {
                IsActive = true,
                ContactName = contactName
            });
        }

        public void EndSession()
        {
            if (!IsSessionActive)
                return;

            IsSessionActive = false;
            ActiveContactName = null;

            AddActivity(AgentDesktopActivityKind.Info, "Desktop watch ended.");
            SessionChanged?.Invoke(this, new AgentDesktopSessionChangedEventArgs { IsActive = false });
            StopCaptureLoopIfIdle();
        }

        public void RequestPreview()
        {
            if (_disposed)
                return;

            _previewRequests++;
            EnsureCaptureLoop();
            TryPublishBrowserFrame(forceOnDemand: true);

            if (LatestFrame?.IsBrowserTab == true)
                FrameCaptured?.Invoke(this, LatestFrame);
        }

        public void ReleasePreview()
        {
            if (_previewRequests <= 0)
                return;

            _previewRequests--;
            StopCaptureLoopIfIdle();
        }

        private void EnsureCaptureLoop()
        {
            EnsureBrowserStreamEnabled();

            if (_sessionCts != null)
                return;

            _sessionCts = new CancellationTokenSource();
            _ = Task.Run(() => RunSessionLoopAsync(_sessionCts.Token));
        }

        private void EnsureBrowserStreamEnabled()
        {
            var enabled = BrowserCaptureBridgeClient.TrySetStreamEnabled(true);
            if (enabled)
                BrowserCaptureBridgeClient.StartCastConsumer();

            if (enabled && !_streamEnabled)
            {
                AddActivity(AgentDesktopActivityKind.Info,
                    "Browser cast ON (WebSocket) - live view from active Chrome/Edge tab.");
            }

            _streamEnabled = enabled;
        }

        private void StopCaptureLoopIfIdle()
        {
            if (IsSessionActive || _previewRequests > 0)
                return;

            if (_streamEnabled)
            {
                BrowserCaptureBridgeClient.TrySetStreamEnabled(false);
                BrowserCaptureBridgeClient.StopCastConsumer();
                _streamEnabled = false;
            }

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;
        }

        private async Task RunSessionLoopAsync(CancellationToken cancellationToken)
        {
            var nextCapture = DateTime.UtcNow;
            var nextLog = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                if (now >= nextLog && IsSessionActive)
                {
                    PollHermesLog();
                    nextLog = now.Add(LogPollInterval);
                }

                if (now >= nextCapture)
                {
                    CaptureFrame();
                    nextCapture = now.Add(CaptureInterval);
                }

                try
                {
                    await Task.Delay(120, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void CaptureFrame()
        {
            try
            {
                if (BrowserCaptureBridgeClient.IsHealthy())
                {
                    TryPublishBrowserFrame(forceOnDemand: false);
                    return;
                }

                if (_previewRequests > 0)
                    return;

                if (!IsSessionActive)
                    return;

                var captured = WindowsScreenCapture.CaptureVirtualScreen();
                if (captured == null)
                    return;

                _lastStreamSourceLabel = null;
                var (deskW, deskH, deskBgra) = captured.Value;
                PublishDesktopFrame(deskW, deskH, deskBgra);
            }
            catch (Exception ex)
            {
                AddActivity(AgentDesktopActivityKind.Error, $"Screen capture failed: {ex.Message}");
            }
        }

        private void TryPublishBrowserFrame(bool forceOnDemand)
        {
            var browser = BrowserCaptureBridgeClient.TryGetLatestStreamFrame(maxWidth: 960, maxAgeSeconds: 5.0);
            if (browser == null && (forceOnDemand || BrowserCaptureBridgeClient.IsStreamEnabled()))
                browser = BrowserCaptureBridgeClient.TryCaptureTabNow(maxWidth: 960, timeoutSeconds: 6.0);

            if (browser != null)
            {
                PublishBrowserFrame(browser.Value);
                return;
            }

            if (_previewRequests > 0 && BrowserCaptureBridgeClient.IsHealthy())
            {
                var now = DateTime.UtcNow;
                if ((now - _lastCaptureErrorUtc).TotalSeconds >= 6)
                {
                    _lastCaptureErrorUtc = now;
                    AddActivity(AgentDesktopActivityKind.Error,
                        "Browser tab capture failed. Keep Chrome in front, reload HV Browser Capture v1.1, run install-browser-capture.ps1.");
                }
                return;
            }

            if (_previewRequests > 0)
                return;

            if (!IsSessionActive)
                return;

            var captured = WindowsScreenCapture.CaptureVirtualScreen();
            if (captured == null)
                return;

            _lastStreamSourceLabel = null;
            PublishDesktopFrame(captured.Value.Width, captured.Value.Height, captured.Value.Bgra32);
        }

        private void PublishDesktopFrame(int deskW, int deskH, byte[] deskBgra)
        {
            var desktopFrame = new AgentDesktopFrame
            {
                Width = deskW,
                Height = deskH,
                Bgra32 = deskBgra,
                CapturedAt = DateTime.Now,
                SourceLabel = "Desktop",
                IsBrowserTab = false
            };

            LatestFrame = desktopFrame;
            FrameCaptured?.Invoke(this, desktopFrame);
        }

        private void PublishBrowserFrame(
            (int Width, int Height, byte[] Bgra32, string SourceLabel) bf)
        {
            var frame = new AgentDesktopFrame
            {
                Width = bf.Width,
                Height = bf.Height,
                Bgra32 = bf.Bgra32,
                CapturedAt = DateTime.Now,
                SourceLabel = bf.SourceLabel,
                IsBrowserTab = true
            };

            if (!string.Equals(_lastStreamSourceLabel, bf.SourceLabel, StringComparison.Ordinal))
            {
                _lastStreamSourceLabel = bf.SourceLabel;
                AddActivity(AgentDesktopActivityKind.Screenshot,
                    $"Live view: browser tab - {bf.SourceLabel}");
            }

            LatestFrame = frame;
            FrameCaptured?.Invoke(this, frame);
        }

        private void OnCastFrameReceived(object? sender, BrowserCastFrameEventArgs e)
        {
            if (_disposed)
                return;

            PublishBrowserFrame((e.Width, e.Height, e.Bgra32, e.SourceLabel));
        }

        private static byte[]? PngFromBgra(byte[] bgra, int width, int height)
        {
            try
            {
                using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var rect = new System.Drawing.Rectangle(0, 0, width, height);
                var data = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var stride = data.Stride;
                    for (var row = 0; row < height; row++)
                        System.Runtime.InteropServices.Marshal.Copy(
                            bgra, row * width * 4, data.Scan0 + row * stride, width * 4);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private void PollHermesLog()
        {
            if (!File.Exists(_hermesLogPath))
                return;

            try
            {
                using var stream = new FileStream(_hermesLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (_logOffset > stream.Length)
                    _logOffset = 0;

                stream.Seek(_logOffset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var summary = ClassifyLogLine(line);
                    if (summary != null)
                        AddActivity(summary.Value.Kind, summary.Value.Text);
                }

                _logOffset = stream.Position;
            }
            catch
            {
                // Log may be locked briefly while gateway writes; ignore transient errors.
            }
        }

        private static (AgentDesktopActivityKind Kind, string Text)? ClassifyLogLine(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 4)
                return null;

            if (Regex.IsMatch(trimmed, @"\b(ERROR|CRITICAL|Traceback)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.Error, trimmed);

            if (Regex.IsMatch(trimmed, @"mcp_+computer[-_ ]use|computer[-_ ]use|computer-use-mcp", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.ToolStart, trimmed);

            if (Regex.IsMatch(trimmed, @"\b(screenshot|screen_capture|capture_screen)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.Screenshot, trimmed);

            if (Regex.IsMatch(trimmed, @"\b(click|mouse_click|double_click|right_click)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.ToolStart, trimmed);

            if (Regex.IsMatch(trimmed, @"\b(type_text|keyboard|key_press|scroll)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.ToolStart, trimmed);

            if (Regex.IsMatch(trimmed, @"\b(tool_call|calling tool|executing tool|mcp_[a-z0-9_]+)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.ToolStart, trimmed);

            if (Regex.IsMatch(trimmed, @"\b(tool result|tool_response|finished tool)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.ToolEnd, trimmed);

            if (Regex.IsMatch(trimmed, @"\b(terminal|browser|web_search|file_)\b", RegexOptions.IgnoreCase)
                && Regex.IsMatch(trimmed, @"\b(start|run|invoke|call)\b", RegexOptions.IgnoreCase))
                return (AgentDesktopActivityKind.ToolStart, trimmed);

            return null;
        }

        private void AddActivity(AgentDesktopActivityKind kind, string text)
        {
            var entry = new AgentDesktopActivityEntry
            {
                Timestamp = DateTime.Now,
                Kind = kind,
                Text = text
            };

            lock (_gate)
            {
                _activity.Add(entry);
                if (_activity.Count > MaxActivityLines)
                    _activity.RemoveRange(0, _activity.Count - MaxActivityLines);
            }

            ActivityAdded?.Invoke(this, entry);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _previewRequests = 0;
            if (IsSessionActive)
            {
                IsSessionActive = false;
                ActiveContactName = null;
            }

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;

            BrowserCaptureBridgeClient.Cast.FrameReceived -= OnCastFrameReceived;
            BrowserCaptureBridgeClient.StopCastConsumer();
            BrowserCaptureBridgeClient.Cast.Dispose();
        }
    }
}
