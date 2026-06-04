using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Polls MT4 bridge quotes across a watchlist and surfaces move/spread alerts for autonomy and MCP.
    /// </summary>
    public interface IMarketWatchScanner
    {
        bool IsRunning { get; }
        IReadOnlyList<string> WatchSymbols { get; }
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
        /// <summary>Runs one scan immediately (also called on the background timer).</summary>
        Task<MarketWatchScanSummary> ScanOnceAsync(CancellationToken cancellationToken = default);
        /// <summary>Alerts since last consume; clears the pending queue.</summary>
        IReadOnlyList<MarketWatchAlert> ConsumePendingAlerts();
        /// <summary>Peek pending alerts without clearing.</summary>
        IReadOnlyList<MarketWatchAlert> PeekPendingAlerts();
        MarketWatchStatus GetStatus();
        event EventHandler<MarketWatchAlert>? AlertRaised;
    }
}
