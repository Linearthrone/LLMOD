using System.Text.Json;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Persists the most recent market-watch scan state and offline reason so the
    /// scanner can resume gracefully after process restarts or bridge outages.
    /// </summary>
    public sealed class MarketWatchStateStore : IDisposable
    {
        private readonly string _basePath;
        private readonly object _lock = new();
        private readonly string _stateFile;
        private readonly string _logFile;

        public MarketWatchStateStore(string basePath)
        {
            _basePath = basePath;
            Directory.CreateDirectory(_basePath);
            _stateFile = Path.Combine(_basePath, "market-watch-state.json");
            _logFile = Path.Combine(_basePath, "market-watch-events.jsonl");
        }

        public void SaveState(MarketWatchPersistedState state)
        {
            try
            {
                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                var tmp = _stateFile + ".writing";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _stateFile, overwrite: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarketWatchStateStore.SaveState failed: {ex.Message}");
            }
        }

        public MarketWatchPersistedState? LoadState()
        {
            try
            {
                if (!File.Exists(_stateFile))
                    return null;

                var json = File.ReadAllText(_stateFile);
                return JsonSerializer.Deserialize<MarketWatchPersistedState>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarketWatchStateStore.LoadState failed: {ex.Message}");
                return null;
            }
        }

        public void AppendEvent(string category, string message, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var line = JsonSerializer.Serialize(new
                {
                    utc = DateTime.UtcNow,
                    category,
                    message,
                    metadata
                });
                lock (_lock)
                {
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // non-fatal
            }
        }

        public void RecordOfflineReason(string reason) => AppendEvent("offline", reason);

        public void RecordError(string error) => AppendEvent("error", error);

        public void Dispose()
        {
            // no unmanaged resources
        }
    }

    public class MarketWatchPersistedState
    {
        public DateTime SavedUtc { get; set; } = DateTime.UtcNow;
        public bool BridgeActive { get; set; }
        public DateTime? LastQuoteScanUtc { get; set; }
        public DateTime? LastTechnicalScanUtc { get; set; }
        public List<MarketWatchAlert> LastAlerts { get; set; } = new();
        public List<TechnicalSignalResult> LastTechnicalSignals { get; set; } = new();
        public List<MarketWatchQuoteSnapshot> LastQuotes { get; set; } = new();
        public string? OfflineReason { get; set; }
    }

    public class MarketWatchQuoteSnapshot
    {
        public string Symbol { get; set; } = string.Empty;
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double SpreadPips { get; set; }
        public DateTime CapturedUtc { get; set; }
    }
}
