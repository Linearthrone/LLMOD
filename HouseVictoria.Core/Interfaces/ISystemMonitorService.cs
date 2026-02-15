using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for system monitoring and server management
    /// </summary>
    public interface ISystemMonitorService
    {
        SystemMetrics GetCurrentMetrics();
        Task<ServerStatus> GetServerStatusAsync(string serverName);
        Task<Dictionary<string, ServerStatus>> GetAllServerStatusesAsync();
        Task RestartServerAsync(string serverName);
        Task StopServerAsync(string serverName);
        Task StartServerAsync(string serverName);
        Task ShutdownAllServersAsync();
        TimeSpan GetSystemUptime();
        AIStatus GetPrimaryAIStatus();
        AIStatus GetCurrentAIContactStatus();
        VirtualEnvironmentStatus GetVirtualEnvironmentStatus();
        
        event EventHandler<SystemMetricsUpdatedEventArgs>? MetricsUpdated;
        event EventHandler<ServerStatusChangedEventArgs>? ServerStatusChanged;
    }

    public class SystemMetricsUpdatedEventArgs : EventArgs
    {
        public SystemMetrics Metrics { get; set; } = null!;
    }

    public class ServerStatusChangedEventArgs : EventArgs
    {
        public string ServerName { get; set; } = string.Empty;
        public ServerStatus PreviousStatus { get; set; } = null!;
        public ServerStatus CurrentStatus { get; set; } = null!;
    }
}
