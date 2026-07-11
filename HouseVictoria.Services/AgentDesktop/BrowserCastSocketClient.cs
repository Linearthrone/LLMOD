using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HouseVictoria.Services.AgentDesktop
{
    /// <summary>
    /// WebSocket consumer for ws://127.0.0.1:17891/ws/cast — receives pushed browser tab frames
    /// from the Chrome extension without HTTP polling.
    /// </summary>
    internal sealed class BrowserCastSocketClient : IDisposable
    {
        private const string CastUrl = "ws://127.0.0.1:17891/ws/cast";
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly object _gate = new();
        private ClientWebSocket? _socket;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private bool _disposed;

        private double _capturedAtUnix;
        private string? _url;
        private string? _title;
        private byte[]? _pngBytes;

        public bool IsConnected => _socket?.State == WebSocketState.Open;

        public event EventHandler<BrowserCastFrameEventArgs>? FrameReceived;

        public void EnsureConnected()
        {
            if (_disposed)
                return;

            if (_socket?.State == WebSocketState.Open)
                return;

            StopInternal();

            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            _receiveTask = Task.Run(() => RunReceiveLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            StopInternal();
        }

        public (int Width, int Height, byte[] Bgra32, string SourceLabel)? TryGetCachedFrame(
            int maxWidth = 960,
            double maxAgeSeconds = 8.0)
        {
            lock (_gate)
            {
                if (_pngBytes == null || _pngBytes.Length == 0)
                    return null;

                var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _capturedAtUnix;
                if (age > maxAgeSeconds)
                    return null;

                var decoded = WindowsScreenCapture.DecodePngToBgra(_pngBytes, maxWidth);
                if (decoded == null)
                    return null;

                var label = string.IsNullOrWhiteSpace(_title)
                    ? _url ?? "Browser tab"
                    : _title;
                return (decoded.Value.Width, decoded.Value.Height, decoded.Value.Bgra32, label);
            }
        }

        private async Task RunReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(5));
                    await _socket!.ConnectAsync(new Uri(CastUrl), connectCts.Token).ConfigureAwait(false);

                    var hello = Encoding.UTF8.GetBytes("""{"role":"consumer"}""");
                    await _socket.SendAsync(hello, WebSocketMessageType.Text, true, cancellationToken)
                        .ConfigureAwait(false);

                    var buffer = new byte[1024 * 512];
                    while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                    {
                        using var ms = new MemoryStream();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                            if (result.MessageType == WebSocketMessageType.Close)
                                break;
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        var json = Encoding.UTF8.GetString(ms.ToArray());
                        HandleFrameMessage(json);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Reconnect after brief delay.
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Delay(1200, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    if (_socket?.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", CancellationToken.None)
                            .ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }

                _socket?.Dispose();
                _socket = new ClientWebSocket();
            }
        }

        private void HandleFrameMessage(string json)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<CastFrameMessage>(json, JsonOptions);
                if (msg?.Type != "frame" || string.IsNullOrWhiteSpace(msg.Png))
                    return;

                var png = Convert.FromBase64String(msg.Png);
                if (png.Length == 0)
                    return;

                double capturedAt;
                lock (_gate)
                {
                    _pngBytes = png;
                    _url = msg.Url;
                    _title = msg.Title;
                    capturedAt = msg.CapturedAt ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    _capturedAtUnix = capturedAt;
                }

                var decoded = WindowsScreenCapture.DecodePngToBgra(png, maxWidth: 960);
                if (decoded == null)
                    return;

                var label = string.IsNullOrWhiteSpace(msg.Title)
                    ? msg.Url ?? "Browser tab"
                    : msg.Title;

                FrameReceived?.Invoke(this, new BrowserCastFrameEventArgs
                {
                    Width = decoded.Value.Width,
                    Height = decoded.Value.Height,
                    Bgra32 = decoded.Value.Bgra32,
                    SourceLabel = label,
                    CapturedAtUnix = capturedAt
                });
            }
            catch
            {
                // Malformed frame — skip.
            }
        }

        private void StopInternal()
        {
            try
            {
                _cts?.Cancel();
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_socket?.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None)
                        .GetAwaiter().GetResult();
            }
            catch
            {
                // ignore
            }

            _socket?.Dispose();
            _socket = null;
            _cts?.Dispose();
            _cts = null;
            _receiveTask = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            StopInternal();
        }

        private sealed class CastFrameMessage
        {
            public string? Type { get; set; }
            public string? Url { get; set; }
            public string? Title { get; set; }
            public string? Png { get; set; }
            public double? CapturedAt { get; set; }
        }
    }

    internal sealed class BrowserCastFrameEventArgs : EventArgs
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] Bgra32 { get; init; } = Array.Empty<byte>();
        public string SourceLabel { get; init; } = "Browser tab";
        public double CapturedAtUnix { get; init; }
    }
}
