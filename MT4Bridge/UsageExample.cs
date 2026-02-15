using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.Examples
{
    /// <summary>
    /// Example usage of the MetaTrader 4 Trading Service
    /// This demonstrates how the AI can interact with MT4
    /// </summary>
    public class TradingServiceExample
    {
        /// <summary>
        /// Example: Get historical data and analyze it
        /// </summary>
        public static async Task ExampleGetHistoricalData()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null)
            {
                Console.WriteLine("Trading service not available");
                return;
            }

            // Get status
            var status = await tradingService.GetStatusAsync();
            if (!status.IsConnected)
            {
                Console.WriteLine("Not connected to MT4. Please configure MT4DataPath in App.config");
                return;
            }

            // Get available symbols
            var symbols = await tradingService.GetSymbolsAsync();
            Console.WriteLine($"Available symbols: {string.Join(", ", symbols)}");

            // Get historical data for EURUSD
            var bars = await tradingService.GetHistoricalDataAsync(
                "EURUSD",
                TimeFrame.H1,
                DateTime.Now.AddMonths(-1),
                DateTime.Now
            );

            Console.WriteLine($"Retrieved {bars.Count} bars of EURUSD H1 data");

            if (bars.Count > 0)
            {
                var firstBar = bars.First();
                var lastBar = bars.Last();
                Console.WriteLine($"Date range: {firstBar.Time:yyyy-MM-dd} to {lastBar.Time:yyyy-MM-dd}");
                Console.WriteLine($"Price range: {bars.Min(b => b.Low):F5} to {bars.Max(b => b.High):F5}");
            }
        }

        /// <summary>
        /// Example: Create and test a trading strategy
        /// </summary>
        public static async Task ExampleCreateAndTestStrategy()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null) return;

            // Create a simple moving average crossover strategy
            var strategy = new TradingStrategy
            {
                Name = "MA_Crossover_10_30",
                Description = "Moving Average Crossover with periods 10 and 30",
                CreatedAt = DateTime.Now,
                Parameters = new Dictionary<string, object>
                {
                    { "FastMA", 10 },
                    { "SlowMA", 30 },
                    { "LotSize", 0.01 }
                }
            };

            // Create the strategy EA file
            bool created = await tradingService.CreateStrategyAsync(strategy);
            if (created)
            {
                Console.WriteLine($"Strategy '{strategy.Name}' created successfully");
                Console.WriteLine("You can now compile and test it in MT4's Strategy Tester");
            }
        }

        /// <summary>
        /// Example: Run a backtest
        /// </summary>
        public static async Task ExampleRunBacktest()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null) return;

            var request = new BacktestRequest
            {
                StrategyName = "MA_Crossover_10_30",
                Symbol = "EURUSD",
                TimeFrame = TimeFrame.H1,
                StartDate = DateTime.Now.AddMonths(-6),
                EndDate = DateTime.Now,
                InitialDeposit = 10000,
                LotSize = 0.01,
                StrategyParameters = new Dictionary<string, object>
                {
                    { "FastMA", 10 },
                    { "SlowMA", 30 }
                }
            };

            var result = await tradingService.RunBacktestAsync(request);

            if (result.Success)
            {
                Console.WriteLine("=== Backtest Results ===");
                Console.WriteLine($"Initial Deposit: ${result.InitialDeposit:F2}");
                Console.WriteLine($"Final Balance: ${result.FinalBalance:F2}");
                Console.WriteLine($"Net Profit: ${result.NetProfit:F2} ({result.ProfitPercent:F2}%)");
                Console.WriteLine($"Total Trades: {result.TotalTrades}");
                Console.WriteLine($"Winning Trades: {result.WinningTrades}");
                Console.WriteLine($"Losing Trades: {result.LosingTrades}");
                Console.WriteLine($"Win Rate: {result.WinRate:F2}%");
                Console.WriteLine($"Max Drawdown: ${result.MaxDrawdown:F2} ({result.MaxDrawdownPercent:F2}%)");
                Console.WriteLine($"Profit Factor: {result.ProfitFactor:F2}");
                Console.WriteLine($"Sharpe Ratio: {result.SharpeRatio:F2}");
            }
            else
            {
                Console.WriteLine($"Backtest failed: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// Example: Get current market data
        /// </summary>
        public static async Task ExampleGetMarketData()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null) return;

            var marketData = await tradingService.GetMarketDataAsync("EURUSD");
            if (marketData != null)
            {
                Console.WriteLine($"=== EURUSD Market Data ===");
                Console.WriteLine($"Bid: {marketData.Bid:F5}");
                Console.WriteLine($"Ask: {marketData.Ask:F5}");
                Console.WriteLine($"Spread: {marketData.Spread:F5}");
                Console.WriteLine($"Last Update: {marketData.LastUpdate:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                Console.WriteLine("Market data not available. Ensure the EA is running.");
            }
        }

        /// <summary>
        /// Example: Get account information
        /// </summary>
        public static async Task ExampleGetAccountInfo()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null) return;

            var accountInfo = await tradingService.GetAccountInfoAsync();
            if (accountInfo != null)
            {
                Console.WriteLine($"=== Account Information ===");
                Console.WriteLine($"Account Number: {accountInfo.AccountNumber}");
                Console.WriteLine($"Account Name: {accountInfo.AccountName}");
                Console.WriteLine($"Balance: ${accountInfo.Balance:F2}");
                Console.WriteLine($"Equity: ${accountInfo.Equity:F2}");
                Console.WriteLine($"Margin: ${accountInfo.Margin:F2}");
                Console.WriteLine($"Free Margin: ${accountInfo.FreeMargin:F2}");
                Console.WriteLine($"Margin Level: {accountInfo.MarginLevel:F2}%");
                Console.WriteLine($"Currency: {accountInfo.Currency}");
                Console.WriteLine($"Leverage: 1:{accountInfo.Leverage}");
            }
        }

        /// <summary>
        /// Example: Get open positions
        /// </summary>
        public static async Task ExampleGetOpenPositions()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null) return;

            var positions = await tradingService.GetOpenPositionsAsync();
            Console.WriteLine($"=== Open Positions ({positions.Count}) ===");

            foreach (var position in positions)
            {
                Console.WriteLine($"Ticket: {position.Ticket}");
                Console.WriteLine($"Symbol: {position.Symbol}");
                Console.WriteLine($"Type: {(position.Type == TradeType.Buy ? "BUY" : "SELL")}");
                Console.WriteLine($"Volume: {position.Volume}");
                Console.WriteLine($"Open Price: {position.OpenPrice:F5}");
                Console.WriteLine($"Current Price: {position.CurrentPrice:F5}");
                Console.WriteLine($"Profit: ${position.Profit:F2}");
                Console.WriteLine("---");
            }
        }

        /// <summary>
        /// Example: Execute a trade (requires EA with trade execution enabled)
        /// </summary>
        public static async Task ExampleExecuteTrade()
        {
            var tradingService = App.ServiceProvider?.GetService<ITradingService>();
            if (tradingService == null) return;

            // Get current market data first
            var marketData = await tradingService.GetMarketDataAsync("EURUSD");
            if (marketData == null)
            {
                Console.WriteLine("Cannot get market data. EA may not be running.");
                return;
            }

            var tradeRequest = new TradeRequest
            {
                Symbol = "EURUSD",
                Type = TradeType.Buy,
                Volume = 0.01, // Minimum lot size
                StopLoss = marketData.Bid - 0.0020, // 20 pips stop loss
                TakeProfit = marketData.Bid + 0.0040, // 40 pips take profit
                Comment = "House Victoria AI Trade"
            };

            bool executed = await tradingService.ExecuteTradeAsync(tradeRequest);
            if (executed)
            {
                Console.WriteLine("Trade command sent to MT4. Check MT4 for execution status.");
            }
            else
            {
                Console.WriteLine("Failed to send trade command.");
            }
        }
    }
}
