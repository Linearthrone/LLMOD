namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Current system performance metrics
    /// </summary>
    public class SystemMetrics
    {
        public double CPUUsage { get; set; }
        public double CPUTemperature { get; set; }
        public double CPUFanSpeed { get; set; }
        public double GPUUsage { get; set; }
        public double GPUTemperature { get; set; }
        public double GPUFanSpeed { get; set; }
        public long RAMUsed { get; set; } // in MB
        public long RAMTotal { get; set; } // in MB
        public double RAMUsagePercentage => (double)RAMUsed / RAMTotal * 100;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Status of an individual server
    /// </summary>
    public class ServerStatus
    {
        public string Name { get; set; } = string.Empty;
        public bool IsRunning { get; set; }
        public string? Endpoint { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime? LastStarted { get; set; }
        public DateTime? LastStopped { get; set; }
        public ServerType Type { get; set; }
        public double? CpuUsage { get; set; }
        public double? MemoryUsage { get; set; }
    }

    /// <summary>
    /// Status of an AI contact
    /// </summary>
    public class AIStatus
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLoaded { get; set; }
        public string? CurrentTask { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Status of the virtual environment
    /// </summary>
    public class VirtualEnvironmentStatus
    {
        public bool IsConnected { get; set; }
        public string? Endpoint { get; set; }
        public string? CurrentScene { get; set; }
        public int AvatarCount { get; set; }
        public bool IsRendering { get; set; }
        public double FrameRate { get; set; }
        public TimeSpan Uptime { get; set; }
    }

    public enum ServerType
    {
        LLM,
        MCP,
        TTS,
        UnrealEngine,
        DataBank,
        Other
    }
}
