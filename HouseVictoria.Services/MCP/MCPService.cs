using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Text.Json;

namespace HouseVictoria.Services.MCP
{
    /// <summary>
    /// Service for communicating with MCP (Model Context Protocol) servers
    /// </summary>
    public class MCPService : IMCPService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, DateTime> _lastHealthCheck = new();
        private readonly Dictionary<string, MCPServerHealth> _healthCache = new();
        private readonly TimeSpan _healthCacheTimeout = TimeSpan.FromSeconds(30);

        public event EventHandler<MCPServerStatusChangedEventArgs>? ServerStatusChanged;

        public MCPService()
        {
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 5,
                UseCookies = false
            };
            
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10),
                MaxResponseContentBufferSize = 1024 * 1024 // 1MB
            };
        }

        public async Task<bool> IsServerAvailableAsync(string endpoint)
        {
            try
            {
                var health = await GetServerHealthAsync(endpoint).ConfigureAwait(false);
                return health.IsHealthy;
            }
            catch
            {
                return false;
            }
        }

        public async Task<MCPServerHealth> GetServerHealthAsync(string endpoint)
        {
            // Check cache first
            if (_healthCache.TryGetValue(endpoint, out var cachedHealth) &&
                _lastHealthCheck.TryGetValue(endpoint, out var lastCheck) &&
                DateTime.Now - lastCheck < _healthCacheTimeout)
            {
                return cachedHealth;
            }

            try
            {
                var healthUrl = EnsureEndpointHasPath(endpoint, "/health");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                
                // Use Task.Run to ensure exception is observed even in background operations
                HttpResponseMessage response;
                try
                {
                    response = await Task.Run(async () => 
                        await _httpClient.GetAsync(healthUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false),
                        cts.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Re-throw to be caught by outer handlers
                    throw;
                }
                
                var health = new MCPServerHealth
                {
                    LastChecked = DateTime.Now,
                    IsHealthy = response.IsSuccessStatusCode
                };

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var healthData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                            if (healthData != null)
                            {
                                health.Status = healthData.TryGetValue("status", out var status) 
                                    ? status.GetString() ?? "unknown" 
                                    : "ok";
                                health.Version = healthData.TryGetValue("version", out var version) 
                                    ? version.GetString() 
                                    : null;
                                
                                var details = new Dictionary<string, object>();
                                foreach (var kvp in healthData)
                                {
                                    if (kvp.Key != "status" && kvp.Key != "version")
                                    {
                                        details[kvp.Key] = kvp.Value.ValueKind switch
                                        {
                                            JsonValueKind.String => kvp.Value.GetString() ?? string.Empty,
                                            JsonValueKind.Number => kvp.Value.GetDouble(),
                                            JsonValueKind.True => true,
                                            JsonValueKind.False => false,
                                            _ => kvp.Value.ToString()
                                        };
                                    }
                                }
                                health.Details = details;
                            }
                        }
                    }
                    catch
                    {
                        health.Status = response.IsSuccessStatusCode ? "ok" : "error";
                    }
                }
                else
                {
                    health.Status = $"http_{response.StatusCode}";
                }

                _healthCache[endpoint] = health;
                _lastHealthCheck[endpoint] = DateTime.Now;

                ServerStatusChanged?.Invoke(this, new MCPServerStatusChangedEventArgs
                {
                    Endpoint = endpoint,
                    IsAvailable = health.IsHealthy,
                    Health = health
                });

                return health;
            }
            catch (TaskCanceledException ex)
            {
                // Connection timeout or cancellation - expected when server is down
                var health = new MCPServerHealth
                {
                    IsHealthy = false,
                    Status = "timeout",
                    LastChecked = DateTime.Now
                };
                _healthCache[endpoint] = health;
                _lastHealthCheck[endpoint] = DateTime.Now;
                
                // Log only if not a cancellation (timeout is expected)
                if (ex.CancellationToken != default && !ex.CancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"MCP health check timeout for {endpoint}: {ex.Message}");
                }
                
                return health;
            }
            catch (System.Net.Http.HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
            {
                // Wrapped socket exception - check this BEFORE the general HttpRequestException
                var health = new MCPServerHealth
                {
                    IsHealthy = false,
                    Status = "connection_failed",
                    LastChecked = DateTime.Now
                };
                _healthCache[endpoint] = health;
                _lastHealthCheck[endpoint] = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"MCP connection failed for {endpoint}: {ex.InnerException?.Message ?? ex.Message}");
                return health;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // HTTP request failed - server unreachable
                var health = new MCPServerHealth
                {
                    IsHealthy = false,
                    Status = "unreachable",
                    LastChecked = DateTime.Now
                };
                _healthCache[endpoint] = health;
                _lastHealthCheck[endpoint] = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"MCP server unreachable at {endpoint}: {ex.Message}");
                return health;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                // Socket connection failed
                var health = new MCPServerHealth
                {
                    IsHealthy = false,
                    Status = "connection_failed",
                    LastChecked = DateTime.Now
                };
                _healthCache[endpoint] = health;
                _lastHealthCheck[endpoint] = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"MCP socket connection failed for {endpoint}: {ex.Message}");
                return health;
            }
            catch (Exception ex)
            {
                // Catch-all for any other exceptions
                var health = new MCPServerHealth
                {
                    IsHealthy = false,
                    Status = $"error: {ex.GetType().Name}",
                    LastChecked = DateTime.Now
                };
                _healthCache[endpoint] = health;
                _lastHealthCheck[endpoint] = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"MCP health check error for {endpoint}: {ex.GetType().Name} - {ex.Message}");
                return health;
            }
        }

        public async Task<MCPServerInfo?> GetServerInfoAsync(string endpoint)
        {
            try
            {
                var infoUrl = EnsureEndpointHasPath(endpoint, "/info");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await _httpClient.GetAsync(infoUrl, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"MCP Server info request failed: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var infoData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                if (infoData == null)
                    return null;

                var info = new MCPServerInfo();

                if (infoData.TryGetValue("name", out var name))
                    info.Name = name.GetString() ?? string.Empty;
                
                if (infoData.TryGetValue("version", out var version))
                    info.Version = version.GetString() ?? string.Empty;
                
                if (infoData.TryGetValue("description", out var desc))
                    info.Description = desc.GetString();

                if (infoData.TryGetValue("capabilities", out var caps) && caps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cap in caps.EnumerateArray())
                    {
                        if (cap.ValueKind == JsonValueKind.String)
                            info.Capabilities.Add(cap.GetString() ?? string.Empty);
                    }
                }

                var metadata = new Dictionary<string, object>();
                foreach (var kvp in infoData)
                {
                    if (kvp.Key != "name" && kvp.Key != "version" && kvp.Key != "description" && kvp.Key != "capabilities")
                    {
                        metadata[kvp.Key] = kvp.Value.ValueKind switch
                        {
                            JsonValueKind.String => kvp.Value.GetString() ?? string.Empty,
                            JsonValueKind.Number => kvp.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Array => kvp.Value.ToString(),
                            JsonValueKind.Object => kvp.Value.ToString(),
                            _ => kvp.Value.ToString()
                        };
                    }
                }
                info.Metadata = metadata;

                return info;
            }
            catch (TaskCanceledException)
            {
                // Timeout - server not responding
                System.Diagnostics.Debug.WriteLine($"MCP server info timeout for {endpoint}");
                return null;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // Connection failed
                System.Diagnostics.Debug.WriteLine($"MCP server info request failed for {endpoint}: {ex.Message}");
                return null;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                // Socket error
                System.Diagnostics.Debug.WriteLine($"MCP server socket error for {endpoint}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting MCP server info for {endpoint}: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        public async Task<MCPResponse?> SendCommandAsync(string endpoint, MCPCommand command)
        {
            try
            {
                var commandUrl = EnsureEndpointHasPath(endpoint, "/command");
                
                var requestBody = new Dictionary<string, object>
                {
                    ["command"] = command.Command
                };

                if (command.Parameters != null && command.Parameters.Count > 0)
                    requestBody["parameters"] = command.Parameters;

                if (!string.IsNullOrEmpty(command.ContextId))
                    requestBody["contextId"] = command.ContextId;

                if (!string.IsNullOrEmpty(command.PersonaId))
                    requestBody["personaId"] = command.PersonaId;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await _httpClient.PostAsJsonAsync(commandUrl, requestBody, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return new MCPResponse
                    {
                        Success = false,
                        Message = $"HTTP {response.StatusCode}: {errorContent}",
                        ErrorCode = response.StatusCode.ToString()
                    };
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new MCPResponse
                    {
                        Success = true,
                        Message = "Command executed successfully"
                    };
                }

                var responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                if (responseData == null)
                {
                    return new MCPResponse
                    {
                        Success = true,
                        Message = content
                    };
                }

                var mcpResponse = new MCPResponse
                {
                    Success = responseData.TryGetValue("success", out var success) && success.GetBoolean(),
                    Message = responseData.TryGetValue("message", out var msg) ? msg.GetString() : null,
                    ErrorCode = responseData.TryGetValue("errorCode", out var errCode) ? errCode.GetString() : null
                };

                if (responseData.TryGetValue("data", out var data))
                    mcpResponse.Data = data.ToString();

                var metadata = new Dictionary<string, object>();
                foreach (var kvp in responseData)
                {
                    if (kvp.Key != "success" && kvp.Key != "message" && kvp.Key != "errorCode" && kvp.Key != "data")
                    {
                        metadata[kvp.Key] = kvp.Value.ToString();
                    }
                }
                if (metadata.Count > 0)
                    mcpResponse.Metadata = metadata;

                return mcpResponse;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending MCP command: {ex.Message}");
                return new MCPResponse
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCode = "exception"
                };
            }
        }

        public async Task<bool> InitializeContextAsync(string endpoint, string personaId, string personaName, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var contextUrl = EnsureEndpointHasPath(endpoint, "/context/init");
                
                var requestBody = new Dictionary<string, object>
                {
                    ["personaId"] = personaId,
                    ["personaName"] = personaName
                };

                if (metadata != null && metadata.Count > 0)
                    requestBody["metadata"] = metadata;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PostAsJsonAsync(contextUrl, requestBody, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize MCP context: {response.StatusCode} - {errorContent}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing MCP context: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateContextAsync(string endpoint, string personaId, string contextData)
        {
            try
            {
                var contextUrl = EnsureEndpointHasPath(endpoint, $"/context/{personaId}");
                
                var requestBody = new Dictionary<string, object>
                {
                    ["data"] = contextData
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.PutAsJsonAsync(contextUrl, requestBody, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"Failed to update MCP context: {response.StatusCode} - {errorContent}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating MCP context: {ex.Message}");
                return false;
            }
        }

        public async Task<MCPContext?> GetContextAsync(string endpoint, string personaId)
        {
            try
            {
                var contextUrl = EnsureEndpointHasPath(endpoint, $"/context/{personaId}");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await _httpClient.GetAsync(contextUrl, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get MCP context: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var contextData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content);
                if (contextData == null)
                    return null;

                var context = new MCPContext
                {
                    PersonaId = personaId,
                    ContextId = contextData.TryGetValue("contextId", out var ctxId) 
                        ? ctxId.GetString() ?? personaId 
                        : personaId
                };

                if (contextData.TryGetValue("data", out var data))
                    context.Data = data.GetString();

                if (contextData.TryGetValue("createdAt", out var createdAt) && 
                    DateTime.TryParse(createdAt.GetString(), out var created))
                    context.CreatedAt = created;
                else
                    context.CreatedAt = DateTime.Now;

                if (contextData.TryGetValue("lastUpdated", out var lastUpdated) && 
                    DateTime.TryParse(lastUpdated.GetString(), out var updated))
                    context.LastUpdated = updated;
                else
                    context.LastUpdated = DateTime.Now;

                var metadata = new Dictionary<string, object>();
                foreach (var kvp in contextData)
                {
                    if (kvp.Key != "contextId" && kvp.Key != "personaId" && kvp.Key != "data" && 
                        kvp.Key != "createdAt" && kvp.Key != "lastUpdated")
                    {
                        metadata[kvp.Key] = kvp.Value.ToString();
                    }
                }
                if (metadata.Count > 0)
                    context.Metadata = metadata;

                return context;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting MCP context: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ClearContextAsync(string endpoint, string personaId)
        {
            try
            {
                var contextUrl = EnsureEndpointHasPath(endpoint, $"/context/{personaId}");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var response = await _httpClient.DeleteAsync(contextUrl, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"Failed to clear MCP context: {response.StatusCode} - {errorContent}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing MCP context: {ex.Message}");
                return false;
            }
        }

        private string EnsureEndpointHasPath(string endpoint, string path)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));

            endpoint = endpoint.TrimEnd('/');
            path = path.TrimStart('/');
            return $"{endpoint}/{path}";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
