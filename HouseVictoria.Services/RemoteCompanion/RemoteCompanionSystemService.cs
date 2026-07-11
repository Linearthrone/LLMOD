using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.RemoteCompanion
{
    public sealed class RemoteCompanionSystemService
    {
        private readonly ISystemMonitorService _systemMonitor;

        public RemoteCompanionSystemService(ISystemMonitorService systemMonitor)
        {
            _systemMonitor = systemMonitor;
        }

        public async Task<RemoteSystemStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metrics = _systemMonitor.GetCurrentMetrics();
            var uptime = _systemMonitor.GetSystemUptime();
            var servers = await _systemMonitor.GetAllServerStatusesAsync().ConfigureAwait(false);

            return new RemoteSystemStatusDto
            {
                CpuUsagePercent = Math.Round(metrics.CPUUsage, 1),
                CpuTemperatureC = Math.Round(metrics.CPUTemperature, 1),
                GpuUsagePercent = Math.Round(metrics.GPUUsage, 1),
                GpuTemperatureC = Math.Round(metrics.GPUTemperature, 1),
                RamUsedMb = metrics.RAMUsed,
                RamTotalMb = metrics.RAMTotal,
                RamUsagePercent = Math.Round(metrics.RAMUsagePercentage, 1),
                UptimeSeconds = (long)uptime.TotalSeconds,
                UptimeLabel = FormatUptime(uptime),
                Servers = servers.Values
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new RemoteServerStatusDto
                    {
                        Name = s.Name,
                        IsRunning = s.IsRunning,
                        Endpoint = s.Endpoint,
                        UptimeSeconds = s.IsRunning ? (long)s.Uptime.TotalSeconds : 0,
                        Type = s.Type.ToString()
                    })
                    .ToList()
            };
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
                return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
            if (uptime.TotalHours >= 1)
                return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
            return $"{(int)uptime.TotalMinutes}m";
        }
    }

    public sealed class RemoteSystemStatusDto
    {
        public double CpuUsagePercent { get; init; }
        public double CpuTemperatureC { get; init; }
        public double GpuUsagePercent { get; init; }
        public double GpuTemperatureC { get; init; }
        public long RamUsedMb { get; init; }
        public long RamTotalMb { get; init; }
        public double RamUsagePercent { get; init; }
        public long UptimeSeconds { get; init; }
        public string UptimeLabel { get; init; } = string.Empty;
        public IReadOnlyList<RemoteServerStatusDto> Servers { get; init; } = Array.Empty<RemoteServerStatusDto>();
    }

    public sealed class RemoteServerStatusDto
    {
        public string Name { get; init; } = string.Empty;
        public bool IsRunning { get; init; }
        public string? Endpoint { get; init; }
        public long UptimeSeconds { get; init; }
        public string Type { get; init; } = string.Empty;
    }
}
