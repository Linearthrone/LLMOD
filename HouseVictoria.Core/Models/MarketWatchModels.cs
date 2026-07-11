namespace HouseVictoria.Core.Models
{
    /// <summary>Alert raised when a watched symbol moves enough to warrant attention.</summary>
    public class MarketWatchAlert
    {
        public DateTime RaisedUtc { get; set; } = DateTime.UtcNow;
        public string Symbol { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double SpreadPips { get; set; }
        public double MovePips { get; set; }
        public string? Direction { get; set; }
        public string? SuggestedStrategy { get; set; }
        public double? IndicatorValue { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class TechnicalSignalResult
    {
        public string Symbol { get; set; } = string.Empty;
        public TimeFrame TimeFrame { get; set; } = TimeFrame.H1;
        public DateTime BarTime { get; set; }
        public string SignalType { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string SuggestedStrategy { get; set; } = string.Empty;
        public double? IndicatorValue { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MarketWatchScanSummary
    {
        public DateTime ScannedUtc { get; set; } = DateTime.UtcNow;
        public int SymbolsPolled { get; set; }
        public int QuotesAvailable { get; set; }
        public int NewAlerts { get; set; }
        public int TechnicalSignalsFound { get; set; }
        public IReadOnlyList<MarketWatchAlert> Alerts { get; set; } = Array.Empty<MarketWatchAlert>();
        public string? OfflineReason { get; set; }
    }

    public class MarketWatchStatus
    {
        public bool ScannerRunning { get; set; }
        public bool BridgeActive { get; set; }
        public IReadOnlyList<string> WatchSymbols { get; set; } = Array.Empty<string>();
        public int PendingAlertCount { get; set; }
        public DateTime? LastQuoteScanUtc { get; set; }
        public DateTime? LastTechnicalScanUtc { get; set; }
        public string? MarketWatchProjectId { get; set; }
        public string? MarketWatchProjectName { get; set; }
        public IReadOnlyList<MarketWatchAlert> PendingAlerts { get; set; } = Array.Empty<MarketWatchAlert>();
        public IReadOnlyList<TechnicalSignalResult> RecentTechnicalSignals { get; set; } = Array.Empty<TechnicalSignalResult>();
        public string? OfflineReason { get; set; }
    }
}
