namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Trading service connection status
    /// </summary>
    public class TradingServiceStatus
    {
        public bool IsConnected { get; set; }
        public string? MT4DataPath { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>
    /// Historical price bar data
    /// </summary>
    public class HistoricalBar
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public TimeFrame TimeFrame { get; set; }
    }

    /// <summary>
    /// Market data for a symbol
    /// </summary>
    public class MarketData
    {
        public string Symbol { get; set; } = string.Empty;
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Spread { get; set; }
        public DateTime LastUpdate { get; set; }
        public double? High { get; set; }
        public double? Low { get; set; }
    }

    /// <summary>
    /// Time frame for historical data
    /// </summary>
    public enum TimeFrame
    {
        M1 = 1,      // 1 minute
        M5 = 5,      // 5 minutes
        M15 = 15,    // 15 minutes
        M30 = 30,    // 30 minutes
        H1 = 60,     // 1 hour
        H4 = 240,    // 4 hours
        D1 = 1440,   // Daily
        W1 = 10080,  // Weekly
        MN1 = 43200  // Monthly
    }

    /// <summary>
    /// Backtest request parameters
    /// </summary>
    public class BacktestRequest
    {
        public string StrategyName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public TimeFrame TimeFrame { get; set; } = TimeFrame.H1;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double InitialDeposit { get; set; } = 10000;
        public double LotSize { get; set; } = 0.01;
        public Dictionary<string, object> StrategyParameters { get; set; } = new();
    }

    /// <summary>
    /// Backtest results
    /// </summary>
    public class BacktestResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public double InitialDeposit { get; set; }
        public double FinalBalance { get; set; }
        public double NetProfit { get; set; }
        public double ProfitPercent { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public double MaxDrawdown { get; set; }
        public double MaxDrawdownPercent { get; set; }
        public double ProfitFactor { get; set; }
        public double SharpeRatio { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Trade> Trades { get; set; } = new();
        public List<EquityPoint> EquityCurve { get; set; } = new();
    }

    /// <summary>
    /// Trading strategy definition
    /// </summary>
    public class TradingStrategy
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // MQL4 code
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Trade request
    /// </summary>
    public class TradeRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public TradeType Type { get; set; }
        public double Volume { get; set; }
        public double? Price { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }
        public string? Comment { get; set; }
    }

    /// <summary>
    /// Trade type
    /// </summary>
    public enum TradeType
    {
        Buy = 0,
        Sell = 1
    }

    /// <summary>
    /// Trade record
    /// </summary>
    public class Trade
    {
        public int Ticket { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public TradeType Type { get; set; }
        public double Volume { get; set; }
        public double OpenPrice { get; set; }
        public DateTime OpenTime { get; set; }
        public double? ClosePrice { get; set; }
        public DateTime? CloseTime { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }
        public double? Profit { get; set; }
        public string? Comment { get; set; }
    }

    /// <summary>
    /// Open position
    /// </summary>
    public class Position
    {
        public int Ticket { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public TradeType Type { get; set; }
        public double Volume { get; set; }
        public double OpenPrice { get; set; }
        public DateTime OpenTime { get; set; }
        public double CurrentPrice { get; set; }
        public double? StopLoss { get; set; }
        public double? TakeProfit { get; set; }
        public double Profit { get; set; }
        public string? Comment { get; set; }
    }

    /// <summary>
    /// Account information
    /// </summary>
    public class AccountInfo
    {
        public int AccountNumber { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public double Balance { get; set; }
        public double Equity { get; set; }
        public double Margin { get; set; }
        public double FreeMargin { get; set; }
        public double MarginLevel { get; set; }
        public string Currency { get; set; } = "USD";
        public double Leverage { get; set; }
    }

    /// <summary>
    /// Equity curve point for backtest visualization
    /// </summary>
    public class EquityPoint
    {
        public DateTime Time { get; set; }
        public double Equity { get; set; }
        public double Balance { get; set; }
    }
}
