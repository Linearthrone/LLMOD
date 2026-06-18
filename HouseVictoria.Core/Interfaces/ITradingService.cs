using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for MetaTrader 4 trading platform integration
    /// </summary>
    public interface ITradingService
    {
        /// <summary>
        /// Connects to MetaTrader 4 instance
        /// </summary>
        Task<bool> ConnectAsync(string mt4DataPath);

        /// <summary>
        /// Disconnects from MT4
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Gets connection status
        /// </summary>
        Task<TradingServiceStatus> GetStatusAsync();

        /// <summary>
        /// Gets available symbols (currency pairs)
        /// </summary>
        Task<List<string>> GetSymbolsAsync();

        /// <summary>
        /// Gets historical data for a symbol
        /// </summary>
        Task<List<HistoricalBar>> GetHistoricalDataAsync(string symbol, TimeFrame timeFrame, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Asks the MT4 EA to export OHLCV bars to a bridge CSV file (then readable via GetHistoricalDataAsync).
        /// </summary>
        Task<HistoricalExportResult> ExportHistoricalDataAsync(
            string symbol,
            TimeFrame timeFrame,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets current market data for a symbol
        /// </summary>
        Task<MarketData?> GetMarketDataAsync(string symbol, bool forceRefresh = false);

        /// <summary>
        /// Runs a backtest with a strategy
        /// </summary>
        Task<BacktestResult> RunBacktestAsync(BacktestRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates a trading strategy EA file
        /// </summary>
        Task<bool> CreateStrategyAsync(TradingStrategy strategy);

        /// <summary>
        /// Gets list of available strategies
        /// </summary>
        Task<List<TradingStrategy>> GetStrategiesAsync();

        /// <summary>
        /// Executes a trade via the MT4 EA file bridge and waits for the EA response.
        /// </summary>
        Task<TradeExecutionResult> ExecuteTradeAsync(TradeRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets account information
        /// </summary>
        Task<AccountInfo?> GetAccountInfoAsync();

        /// <summary>
        /// Gets open positions
        /// </summary>
        Task<List<Position>> GetOpenPositionsAsync();

        event EventHandler<TradingServiceEventArgs>? StatusChanged;
        event EventHandler<MarketDataEventArgs>? MarketDataUpdated;
    }

    public class TradingServiceEventArgs : EventArgs
    {
        public TradingServiceStatus Status { get; set; } = null!;
    }

    public class MarketDataEventArgs : EventArgs
    {
        public string Symbol { get; set; } = string.Empty;
        public MarketData MarketData { get; set; } = null!;
    }
}
