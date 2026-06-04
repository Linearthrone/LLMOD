using System.Text.Json;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Trading.Backtest;

namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Background scanner: quotes, technical H1 signals, alerts, and status file for MCP.
    /// </summary>
    public sealed class MarketWatchScannerService : IMarketWatchScanner, IDisposable
    {
        private readonly AppConfig _config;
        private readonly ITradingService _trading;
        private readonly IProjectManagementService? _projects;
        private readonly object _lock = new();
        private readonly List<MarketWatchAlert> _pendingAlerts = new();
        private readonly Dictionary<string, double> _lastMidBySymbol = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lastTechnicalSignalKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TechnicalSignalResult> _recentTechnicalSignals = new();

        private Timer? _timer;
        private bool _running;
        private string[] _watchSymbols = Array.Empty<string>();
        private DateTime? _lastQuoteScanUtc;
        private DateTime? _lastTechnicalScanUtc;
        private string? _marketWatchProjectId;
        private bool _bridgeActive;

        public event EventHandler<MarketWatchAlert>? AlertRaised;

        public bool IsRunning => _running;
        public IReadOnlyList<string> WatchSymbols => _watchSymbols;

        public MarketWatchScannerService(
            AppConfig config,
            ITradingService trading,
            IProjectManagementService? projects = null)
        {
            _config = config;
            _trading = trading;
            _projects = projects;
            _watchSymbols = ParseWatchSymbols(_config.TradingWatchSymbols);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!_config.TradingWatchEnabled)
                return;

            if (_projects != null)
            {
                var project = await MarketWatchProjectBootstrap.EnsureAsync(_projects, _config, cancellationToken)
                    .ConfigureAwait(false);
                _marketWatchProjectId = project?.Id;
            }

            lock (_lock)
            {
                if (_running)
                    return;

                _watchSymbols = ParseWatchSymbols(_config.TradingWatchSymbols);
                var intervalMs = Math.Max(15, _config.TradingWatchIntervalSeconds) * 1000;
                _timer = new Timer(async _ => await RunTimerScanAsync().ConfigureAwait(false), null, 2000, intervalMs);
                _running = true;
            }

            _ = Task.Run(async () =>
            {
                await SyncWatchlistToBridgeAsync(cancellationToken).ConfigureAwait(false);
                await ScanOnceAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
                _running = false;
            }

            return Task.CompletedTask;
        }

        public async Task<MarketWatchScanSummary> ScanOnceAsync(CancellationToken cancellationToken = default)
        {
            var alerts = new List<MarketWatchAlert>();
            var polled = 0;
            var available = 0;
            var technicalFound = 0;

            if (!_config.TradingWatchEnabled)
            {
                await WriteStatusFileAsync().ConfigureAwait(false);
                return new MarketWatchScanSummary();
            }

            var status = await _trading.GetStatusAsync().ConfigureAwait(false);
            _bridgeActive = status.IsConnected && status.IsBridgeActive;
            if (!_bridgeActive)
            {
                await WriteStatusFileAsync().ConfigureAwait(false);
                return new MarketWatchScanSummary();
            }

            await SyncWatchlistToBridgeAsync(cancellationToken).ConfigureAwait(false);

            foreach (var symbol in _watchSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                polled++;

                var quote = await _trading.GetMarketDataAsync(symbol).ConfigureAwait(false);
                if (quote == null)
                    continue;

                available++;
                var mid = (quote.Bid + quote.Ask) / 2.0;
                var pip = GetPipSize(symbol);
                var spreadPips = (quote.Ask - quote.Bid) / pip;

                MarketWatchAlert? alert = null;
                if (_lastMidBySymbol.TryGetValue(symbol, out var prevMid))
                {
                    var movePips = Math.Abs(mid - prevMid) / pip;
                    if (movePips >= _config.TradingWatchPipMoveThreshold)
                    {
                        var direction = mid > prevMid ? "up" : "down";
                        alert = new MarketWatchAlert
                        {
                            Symbol = symbol,
                            AlertType = "price_move",
                            Direction = direction,
                            Bid = quote.Bid,
                            Ask = quote.Ask,
                            SpreadPips = spreadPips,
                            MovePips = movePips,
                            Message =
                                $"{symbol} moved {movePips:F1} pips {direction} " +
                                $"(mid {prevMid:F5} → {mid:F5}, spread {spreadPips:F1} pips)"
                        };
                    }
                }

                if (alert == null && spreadPips >= _config.TradingWatchMaxSpreadPips)
                {
                    alert = new MarketWatchAlert
                    {
                        Symbol = symbol,
                        AlertType = "wide_spread",
                        Bid = quote.Bid,
                        Ask = quote.Ask,
                        SpreadPips = spreadPips,
                        Message = $"{symbol} wide spread {spreadPips:F1} pips (threshold {_config.TradingWatchMaxSpreadPips})"
                    };
                }

                _lastMidBySymbol[symbol] = mid;

                if (alert != null)
                {
                    alerts.Add(alert);
                    EnqueueAlert(alert);
                }
            }

            _lastQuoteScanUtc = DateTime.UtcNow;

            if (_config.TradingWatchTechnicalEnabled && ShouldRunTechnicalScan())
            {
                technicalFound = await RunTechnicalScanAsync(status, cancellationToken).ConfigureAwait(false);
            }

            AppendScanLog(polled, available, alerts, technicalFound);
            await WriteStatusFileAsync().ConfigureAwait(false);

            return new MarketWatchScanSummary
            {
                ScannedUtc = DateTime.UtcNow,
                SymbolsPolled = polled,
                QuotesAvailable = available,
                NewAlerts = alerts.Count,
                TechnicalSignalsFound = technicalFound,
                Alerts = alerts
            };
        }

        public IReadOnlyList<MarketWatchAlert> ConsumePendingAlerts()
        {
            lock (_lock)
            {
                if (_pendingAlerts.Count == 0)
                    return Array.Empty<MarketWatchAlert>();

                var copy = _pendingAlerts.ToList();
                _pendingAlerts.Clear();
                return copy;
            }
        }

        public IReadOnlyList<MarketWatchAlert> PeekPendingAlerts()
        {
            lock (_lock)
            {
                return _pendingAlerts.Count == 0
                    ? Array.Empty<MarketWatchAlert>()
                    : _pendingAlerts.ToList();
            }
        }

        public MarketWatchStatus GetStatus()
        {
            lock (_lock)
            {
                return new MarketWatchStatus
                {
                    ScannerRunning = _running,
                    BridgeActive = _bridgeActive,
                    WatchSymbols = _watchSymbols.ToList(),
                    PendingAlertCount = _pendingAlerts.Count,
                    LastQuoteScanUtc = _lastQuoteScanUtc,
                    LastTechnicalScanUtc = _lastTechnicalScanUtc,
                    MarketWatchProjectId = _marketWatchProjectId,
                    MarketWatchProjectName = MarketWatchProjectBootstrap.ProjectName,
                    PendingAlerts = _pendingAlerts.ToList(),
                    RecentTechnicalSignals = _recentTechnicalSignals.ToList()
                };
            }
        }

        public void Dispose() => StopAsync().GetAwaiter().GetResult();

        private bool ShouldRunTechnicalScan()
        {
            if (_lastTechnicalScanUtc == null)
                return true;

            return DateTime.UtcNow - _lastTechnicalScanUtc.Value >=
                   TimeSpan.FromSeconds(Math.Max(60, _config.TradingWatchTechnicalIntervalSeconds));
        }

        private async Task<int> RunTechnicalScanAsync(
            TradingServiceStatus status,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(status.MT4DataPath))
                return 0;

            var found = 0;
            var end = DateTime.UtcNow;
            var barCount = Math.Clamp(_config.TradingWatchTechnicalBarCount, TechnicalSignalScanner.MinBars, 500);
            var start = end.AddHours(-barCount);

            foreach (var symbol in _watchSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<HistoricalBar> bars;
                try
                {
                    bars = await Mt4HistoricalDataReader.LoadBarsAsync(
                        status.MT4DataPath!,
                        symbol,
                        TimeFrame.H1,
                        start,
                        end).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (bars.Count < TechnicalSignalScanner.MinBars)
                {
                    try
                    {
                        await _trading.ExportHistoricalDataAsync(symbol, TimeFrame.H1, start, end, cancellationToken)
                            .ConfigureAwait(false);
                        bars = await Mt4HistoricalDataReader.LoadBarsAsync(
                            status.MT4DataPath!,
                            symbol,
                            TimeFrame.H1,
                            start,
                            end).ConfigureAwait(false);
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (bars.Count < TechnicalSignalScanner.MinBars)
                    continue;

                bars = bars.OrderBy(b => b.Time).TakeLast(barCount).ToList();
                var signal = TechnicalSignalScanner.Evaluate(symbol, bars);
                if (signal == null)
                    continue;

                var signalKey = $"{symbol}:{signal.SignalType}:{signal.Direction}";
                if (_lastTechnicalSignalKey.TryGetValue(symbol, out var prevKey) && prevKey == signalKey)
                    continue;

                _lastTechnicalSignalKey[symbol] = signalKey;
                found++;

                lock (_lock)
                {
                    _recentTechnicalSignals.Add(signal);
                    while (_recentTechnicalSignals.Count > 30)
                        _recentTechnicalSignals.RemoveAt(0);
                }

                var quote = await _trading.GetMarketDataAsync(symbol).ConfigureAwait(false);
                var alert = new MarketWatchAlert
                {
                    Symbol = symbol,
                    AlertType = $"technical_{signal.SignalType}",
                    Direction = signal.Direction,
                    SuggestedStrategy = signal.SuggestedStrategy,
                    IndicatorValue = signal.IndicatorValue,
                    Bid = quote?.Bid ?? 0,
                    Ask = quote?.Ask ?? 0,
                    Message = $"[H1] {signal.Message}"
                };

                EnqueueAlert(alert);
                AppendTechnicalLog(signal);
            }

            _lastTechnicalScanUtc = DateTime.UtcNow;
            return found;
        }

        private async Task RunTimerScanAsync()
        {
            try
            {
                await ScanOnceAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Market watch scan: {ex.Message}");
            }
        }

        private async Task SyncWatchlistToBridgeAsync(CancellationToken cancellationToken)
        {
            var status = await _trading.GetStatusAsync().ConfigureAwait(false);
            if (!status.IsConnected || string.IsNullOrWhiteSpace(status.MT4DataPath))
                return;

            try
            {
                var commandPath = Path.Combine(status.MT4DataPath, "MQL4", "Files", "HouseVictoria");
                Directory.CreateDirectory(commandPath);
                var json = JsonSerializer.Serialize(_watchSymbols);
                await File.WriteAllTextAsync(Path.Combine(commandPath, "Watchlist.json"), json, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Watchlist sync: {ex.Message}");
            }
        }

        private void EnqueueAlert(MarketWatchAlert alert)
        {
            lock (_lock)
            {
                if (_pendingAlerts.Count >= 50)
                    _pendingAlerts.RemoveAt(0);
                _pendingAlerts.Add(alert);
            }

            AlertRaised?.Invoke(this, alert);
        }

        private void AppendScanLog(int polled, int available, List<MarketWatchAlert> alerts, int technicalFound)
        {
            try
            {
                Directory.CreateDirectory(_config.AutonomyDataPath);
                var line = JsonSerializer.Serialize(new
                {
                    utc = DateTime.UtcNow,
                    polled,
                    available,
                    alert_count = alerts.Count,
                    technical_found = technicalFound,
                    alerts = alerts.Select(a => new
                    {
                        a.Symbol,
                        a.AlertType,
                        a.Direction,
                        a.SuggestedStrategy,
                        a.MovePips,
                        a.Message
                    })
                });
                File.AppendAllText(Path.Combine(_config.AutonomyDataPath, "market-scan.jsonl"), line + Environment.NewLine);
            }
            catch
            {
                // non-fatal
            }
        }

        private void AppendTechnicalLog(TechnicalSignalResult signal)
        {
            try
            {
                Directory.CreateDirectory(_config.AutonomyDataPath);
                var line = JsonSerializer.Serialize(new
                {
                    utc = DateTime.UtcNow,
                    signal.Symbol,
                    signal.SignalType,
                    signal.Direction,
                    signal.SuggestedStrategy,
                    signal.IndicatorValue,
                    signal.Message,
                    bar_time = signal.BarTime
                });
                File.AppendAllText(Path.Combine(_config.AutonomyDataPath, "market-technical.jsonl"), line + Environment.NewLine);
            }
            catch
            {
                // non-fatal
            }
        }

        private async Task WriteStatusFileAsync()
        {
            try
            {
                Directory.CreateDirectory(_config.AutonomyDataPath);
                var status = GetStatus();
                var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(_config.AutonomyDataPath, "market-watch-status.json"), json)
                    .ConfigureAwait(false);
            }
            catch
            {
                // non-fatal
            }
        }

        private static string[] ParseWatchSymbols(string? raw)
        {
            const string defaultList =
                "EURUSD,GBPUSD,USDJPY,AUDUSD,USDCAD,USDCHF,NZDUSD,EURGBP,EURJPY,GBPJPY,XAUUSD,XAGUSD,US30,US500,NAS100";

            var text = string.IsNullOrWhiteSpace(raw) ? defaultList : raw;
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static double GetPipSize(string symbol) =>
            symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 0.01 : 0.0001;
    }
}
