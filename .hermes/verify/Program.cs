using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Trading;

// Ad-hoc verification harness for MarketWatchScannerService changes.
// No real MT4 connection: we simulate an offline ITradingService.

var tmp = Path.Combine(Path.GetTempPath(), "hermes-verify-marketwatch-20260709");
Directory.CreateDirectory(tmp);

var config = new AppConfig
{
    AutonomyDataPath = tmp,
    TradingWatchEnabled = true,
    TradingWatchSymbols = "EURUSD,GBPUSD,USDJPY,XAUUSD,US30",
    TradingWatchPipMoveThreshold = 5,
    TradingWatchMaxSpreadPips = 20,
    TradingWatchIntervalSeconds = 15,
    TradingWatchTechnicalEnabled = false
};

var fake = new FakeTradingService();
var scanner = new MarketWatchScannerService(config, fake);

// 1. Offline scan must report a reason.
var summary1 = await scanner.ScanOnceAsync(CancellationToken.None);
Console.WriteLine($"Offline reason: {summary1.OfflineReason}");
if (string.IsNullOrWhiteSpace(summary1.OfflineReason))
    throw new Exception("Expected OfflineReason when bridge inactive");

// 2. State file must exist after scan.
var stateFile = Path.Combine(tmp, "market-watch-state.json");
var eventsFile = Path.Combine(tmp, "market-watch-events.jsonl");
if (!File.Exists(stateFile))
    throw new Exception($"Expected state file: {stateFile}");
if (!File.Exists(eventsFile))
    throw new Exception($"Expected events file: {eventsFile}");

Console.WriteLine($"State file exists: {stateFile}");
Console.WriteLine($"Events file exists: {eventsFile}");

// 3. Pip size helper: JPY, XAU, US30 = 0.01; EURUSD = 0.0001
var pipEur = MarketWatchScannerService_GetPipSize("EURUSD");
var pipJpy = MarketWatchScannerService_GetPipSize("USDJPY");
var pipGold = MarketWatchScannerService_GetPipSize("XAUUSD");
var pipIndex = MarketWatchScannerService_GetPipSize("US30");
Console.WriteLine($"Pips: EURUSD={pipEur} USDJPY={pipJpy} XAUUSD={pipGold} US30={pipIndex}");
if (Math.Abs(pipEur - 0.0001) > 1e-9) throw new Exception("EURUSD pip size wrong");
if (Math.Abs(pipJpy - 0.01) > 1e-9) throw new Exception("JPY pip size wrong");
if (Math.Abs(pipGold - 0.01) > 1e-9) throw new Exception("XAUUSD pip size wrong");
if (Math.Abs(pipIndex - 0.01) > 1e-9) throw new Exception("US30 pip size wrong");

await scanner.StopAsync();
// Clean up
Directory.Delete(tmp, true);
Console.WriteLine("AD-HOC VERIFICATION PASSED");

// Reflection trick to call private static helper
static double MarketWatchScannerService_GetPipSize(string symbol)
{
    var type = typeof(MarketWatchScannerService);
    var method = type.GetMethod("GetPipSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    return (double)method.Invoke(null, new object[] { symbol })!;
}

class AppConfig : IConfig
{
    public string AutonomyDataPath { get; set; } = "";
    public bool TradingWatchEnabled { get; set; }
    public string? TradingWatchSymbols { get; set; }
    public double TradingWatchPipMoveThreshold { get; set; }
    public double TradingWatchMaxSpreadPips { get; set; }
    public int TradingWatchIntervalSeconds { get; set; }
    public bool TradingWatchTechnicalEnabled { get; set; }
    public int TradingWatchTechnicalIntervalSeconds { get; set; } = 300;
    public int TradingWatchTechnicalBarCount { get; set; } = 200;
}

interface IConfig { }

class FakeTradingService : ITradingService
{
    public Task<bool> ConnectAsync(string mt4DataPath) => Task.FromResult(false);
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task<TradingServiceStatus> GetStatusAsync() => Task.FromResult(new TradingServiceStatus { IsConnected = true, IsBridgeActive = false });
    public Task<List<string>> GetSymbolsAsync() => Task.FromResult(new List<string>());
    public Task<List<HistoricalBar>> GetHistoricalDataAsync(string symbol, TimeFrame timeFrame, DateTime startDate, DateTime endDate) => Task.FromResult(new List<HistoricalBar>());
    public Task<HistoricalExportResult> ExportHistoricalDataAsync(string symbol, TimeFrame timeFrame, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) => Task.FromResult(new HistoricalExportResult { Success = false, Symbol = symbol });
    public Task<MarketData?> GetMarketDataAsync(string symbol, bool forceRefresh = false) => Task.FromResult<MarketData?>(null);
    public Task<BacktestResult> RunBacktestAsync(BacktestRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new BacktestResult { Success = false });
    public Task<bool> CreateStrategyAsync(TradingStrategy strategy) => Task.FromResult(false);
    public Task<List<TradingStrategy>> GetStrategiesAsync() => Task.FromResult(new List<TradingStrategy>());
    public Task<TradeExecutionResult> ExecuteTradeAsync(TradeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new TradeExecutionResult { Success = false });
    public Task<AccountInfo?> GetAccountInfoAsync() => Task.FromResult<AccountInfo?>(null);
    public Task<List<Position>> GetOpenPositionsAsync() => Task.FromResult(new List<Position>());
    public event EventHandler<TradingServiceEventArgs>? StatusChanged;
    public event EventHandler<MarketDataEventArgs>? MarketDataUpdated;
}
