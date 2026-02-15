using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HouseVictoria.Services.VirtualEnvironment
{
    /// <summary>
    /// Service for interacting with Unreal Engine virtual environment
    /// </summary>
    public class UnrealEnvironmentService : IVirtualEnvironmentService
    {
        private ClientWebSocket? _webSocket;
        private string? _endpoint;
        private VirtualEnvironmentStatus _status = new();
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private DateTime _connectionStartTime;
        private bool _autoReconnectEnabled = true;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 5;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public event EventHandler<VirtualEnvironmentEventArgs>? StatusChanged;
        public event EventHandler<SceneUpdateEventArgs>? SceneUpdated;

        public async Task<bool> ConnectAsync(string endpoint)
        {
            try
            {
                // Disconnect existing connection if any
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await DisconnectAsync();
                }

                _endpoint = endpoint;
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(endpoint), CancellationToken.None);

                _status.IsConnected = true;
                _status.Endpoint = endpoint;
                _connectionStartTime = DateTime.Now;
                _status.Uptime = TimeSpan.Zero;
                _reconnectAttempts = 0;

                // Start background message receiving
                _receiveCts = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveMessagesAsync(_receiveCts.Token));

                StatusChanged?.Invoke(this, new VirtualEnvironmentEventArgs { Status = _status });
                
                System.Diagnostics.Debug.WriteLine($"Virtual Environment connected to {endpoint}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to connect to Virtual Environment: {ex.Message}");
                _status.IsConnected = false;
                StatusChanged?.Invoke(this, new VirtualEnvironmentEventArgs { Status = _status });
                
                // Attempt auto-reconnect if enabled
                if (_autoReconnectEnabled && _reconnectAttempts < MaxReconnectAttempts)
                {
                    _ = Task.Run(async () => await AttemptReconnectAsync());
                }
                
                return false;
            }
        }

        private async Task AttemptReconnectAsync()
        {
            if (string.IsNullOrEmpty(_endpoint)) return;

            _reconnectAttempts++;
            var delay = TimeSpan.FromSeconds(Math.Min(5 * _reconnectAttempts, 30)); // Exponential backoff, max 30s
            
            System.Diagnostics.Debug.WriteLine($"Attempting to reconnect to Virtual Environment (attempt {_reconnectAttempts}/{MaxReconnectAttempts}) in {delay.TotalSeconds}s...");
            
            await Task.Delay(delay);
            
            if (_webSocket?.State != WebSocketState.Open && !string.IsNullOrEmpty(_endpoint))
            {
                var connected = await ConnectAsync(_endpoint);
                if (!connected && _reconnectAttempts < MaxReconnectAttempts)
                {
                    // Will retry on next failure
                }
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            
            while (!cancellationToken.IsCancellationRequested && _webSocket != null)
            {
                try
                {
                    if (_webSocket.State != WebSocketState.Open)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        System.Diagnostics.Debug.WriteLine("Virtual Environment WebSocket closed by server");
                        await HandleDisconnectionAsync();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessReceivedMessageAsync(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error receiving message from Virtual Environment: {ex.Message}");
                    if (_webSocket?.State != WebSocketState.Open)
                    {
                        await HandleDisconnectionAsync();
                        break;
                    }
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task ProcessReceivedMessageAsync(string message)
        {
            try
            {
                // Try to parse as JSON first
                if (message.TrimStart().StartsWith("{"))
                {
                    var jsonDoc = JsonDocument.Parse(message);
                    var root = jsonDoc.RootElement;

                    // Update status from message
                    if (root.TryGetProperty("type", out var typeElement))
                    {
                        var messageType = typeElement.GetString();
                        
                        if (messageType == "status")
                        {
                            UpdateStatusFromMessage(root);
                        }
                        else if (messageType == "scene_update")
                        {
                            HandleSceneUpdate(root);
                        }
                    }
                }
                else
                {
                    // Plain text response - might be a command response
                    System.Diagnostics.Debug.WriteLine($"Virtual Environment message: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing Virtual Environment message: {ex.Message}");
            }
        }

        private void UpdateStatusFromMessage(JsonElement root)
        {
            if (root.TryGetProperty("scene", out var sceneElement))
                _status.CurrentScene = sceneElement.GetString();
            
            if (root.TryGetProperty("avatar_count", out var avatarCountElement))
                _status.AvatarCount = avatarCountElement.GetInt32();
            
            if (root.TryGetProperty("fps", out var fpsElement))
                _status.FrameRate = fpsElement.GetDouble();
            
            if (root.TryGetProperty("rendering", out var renderingElement))
                _status.IsRendering = renderingElement.GetBoolean();
            
            _status.Uptime = DateTime.Now - _connectionStartTime;
            
            StatusChanged?.Invoke(this, new VirtualEnvironmentEventArgs { Status = _status });
        }

        private void HandleSceneUpdate(JsonElement root)
        {
            var sceneName = root.TryGetProperty("scene", out var sceneElement) ? sceneElement.GetString() : _status.CurrentScene;
            var updateType = root.TryGetProperty("update_type", out var typeElement) ? typeElement.GetString() : "Unknown";
            
            SceneUpdated?.Invoke(this, new SceneUpdateEventArgs
            {
                SceneName = sceneName ?? "Unknown",
                UpdateType = updateType ?? "Unknown",
                Data = root
            });
        }

        private async Task HandleDisconnectionAsync()
        {
            _status.IsConnected = false;
            _status.Uptime = TimeSpan.Zero;
            StatusChanged?.Invoke(this, new VirtualEnvironmentEventArgs { Status = _status });
            
            if (_autoReconnectEnabled && _reconnectAttempts < MaxReconnectAttempts && !string.IsNullOrEmpty(_endpoint))
            {
                await AttemptReconnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            _autoReconnectEnabled = false; // Disable auto-reconnect when manually disconnecting
            
            // Stop receiving messages
            _receiveCts?.Cancel();
            if (_receiveTask != null)
            {
                try
                {
                    await _receiveTask;
                }
                catch { }
            }
            _receiveCts?.Dispose();
            _receiveCts = null;
            _receiveTask = null;

            if (_webSocket != null)
            {
                try
                {
                    if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error closing WebSocket: {ex.Message}");
                }
                finally
                {
                    _webSocket.Dispose();
                    _webSocket = null;
                }
            }

            _status.IsConnected = false;
            _status.Uptime = TimeSpan.Zero;
            StatusChanged?.Invoke(this, new VirtualEnvironmentEventArgs { Status = _status });
            
            System.Diagnostics.Debug.WriteLine("Virtual Environment disconnected");
        }

        public async Task<VirtualEnvironmentStatus> GetStatusAsync()
        {
            // Update uptime
            if (_status.IsConnected && _connectionStartTime != default)
            {
                _status.Uptime = DateTime.Now - _connectionStartTime;
            }

            // Refresh status from Unreal Engine if connected
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await SendCommandAsync("status");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error requesting status: {ex.Message}");
                    // If send fails, connection might be broken
                    if (_webSocket.State != WebSocketState.Open)
                    {
                        await HandleDisconnectionAsync();
                    }
                }
            }
            else if (_status.IsConnected)
            {
                // Connection state mismatch - update status
                _status.IsConnected = false;
                StatusChanged?.Invoke(this, new VirtualEnvironmentEventArgs { Status = _status });
            }
            
            return await Task.FromResult(_status);
        }

        public async Task<string> SendCommandAsync(string command)
        {
            await _sendLock.WaitAsync();
            try
            {
                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException("Not connected to virtual environment");
                }

                var buffer = Encoding.UTF8.GetBytes(command);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                
                // Note: Response will be handled by the background receive task
                // For synchronous responses, we'd need a request-response pattern with TaskCompletionSource
                // For now, return a simple acknowledgment
                return "Command sent";
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<string> GetSceneInformationAsync()
        {
            return await SendCommandAsync("get_scene_info");
        }

        public async Task<byte[]> CaptureSceneAsync()
        {
            var response = await SendCommandAsync("capture_scene");
            // Assuming response contains base64 image data
            return Convert.FromBase64String(response);
        }

        public async Task<string> SpawnAvatarAsync(AvatarDefinition avatar)
        {
            var command = $"spawn_avatar {avatar.Name} {avatar.ModelPath} {avatar.Position.X} {avatar.Position.Y} {avatar.Position.Z}";
            var response = await SendCommandAsync(command);

            _status.AvatarCount++;
            SceneUpdated?.Invoke(this, new SceneUpdateEventArgs
            {
                SceneName = _status.CurrentScene ?? "Default",
                UpdateType = "AvatarSpawned",
                Data = avatar
            });

            return response;
        }

        public async Task<string> UpdateAvatarPoseAsync(string avatarId, Pose pose, string? facialExpression = null)
        {
            var command = $"update_pose {avatarId} {pose.Position.X} {pose.Position.Y} {pose.Position.Z} " +
                         $"{pose.Rotation.X} {pose.Rotation.Y} {pose.Rotation.Z}" +
                         (string.IsNullOrEmpty(facialExpression) ? "" : $" {facialExpression}");
            return await SendCommandAsync(command);
        }

        public async Task<string> MoveAvatarAsync(string avatarId, float x, float y, float z, float rotationY)
        {
            var command = $"move_avatar {avatarId} {x} {y} {z} {rotationY}";
            return await SendCommandAsync(command);
        }

        public async Task<string> AnimateAvatarAsync(string avatarId, string animationName)
        {
            var command = $"animate_avatar {avatarId} {animationName}";
            return await SendCommandAsync(command);
        }

        public async Task<Dictionary<string, object>> GetAvatarStateAsync(string avatarId)
        {
            var response = await SendCommandAsync($"get_avatar_state {avatarId}");
            // Parse response into dictionary
            return new Dictionary<string, object>
            {
                { "AvatarId", avatarId },
                { "State", response }
            };
        }
    }
}
