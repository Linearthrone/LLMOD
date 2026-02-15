using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Service for interacting with MetaTrader 4 trading platform
    /// Uses file-based communication for reliability
    /// </summary>
    public class MetaTrader4Service : ITradingService
    {
        private TradingServiceStatus _status = new();
        private string? _mt4DataPath;
        private readonly string _commandFolder = "HouseVictoria";
        private readonly string _strategyFolder = "Experts";
        private readonly Timer? _marketDataTimer;
        private readonly Dictionary<string, MarketData> _marketDataCache = new();
        private readonly object _lockObject = new();

        public event EventHandler<TradingServiceEventArgs>? StatusChanged;
        public event EventHandler<MarketDataEventArgs>? MarketDataUpdated;

        public MetaTrader4Service()
        {
            // Timer for periodic market data updates (every 5 seconds)
            _marketDataTimer = new Timer(UpdateMarketData, null, Timeout.Infinite, 5000);
        }

        public async Task<bool> ConnectAsync(string mt4DataPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mt4DataPath))
                {
                    throw new ArgumentException("MT4 data path cannot be empty", nameof(mt4DataPath));
                }

                if (!Directory.Exists(mt4DataPath))
                {
                    throw new DirectoryNotFoundException($"MT4 data directory not found: {mt4DataPath}");
                }

                _mt4DataPath = mt4DataPath;

                // Create communication folders
                var commandPath = Path.Combine(mt4DataPath, "MQL4", "Files", _commandFolder);
                Directory.CreateDirectory(commandPath);

                var responsePath = Path.Combine(mt4DataPath, "MQL4", "Files", _commandFolder, "Responses");
                Directory.CreateDirectory(responsePath);

                // Verify MT4 structure
                var expertsPath = Path.Combine(mt4DataPath, "MQL4", _strategyFolder);
                if (!Directory.Exists(expertsPath))
                {
                    Directory.CreateDirectory(expertsPath);
                }

                _status.IsConnected = true;
                _status.MT4DataPath = mt4DataPath;
                _status.ConnectedAt = DateTime.Now;
                _status.LastError = null;

                StatusChanged?.Invoke(this, new TradingServiceEventArgs { Status = _status });

                // Start market data updates
                _marketDataTimer?.Change(0, 5000);

                System.Diagnostics.Debug.WriteLine($"Connected to MT4 at: {mt4DataPath}");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _status.IsConnected = false;
                _status.LastError = ex.Message;
                StatusChanged?.Invoke(this, new TradingServiceEventArgs { Status = _status });
                System.Diagnostics.Debug.WriteLine($"Failed to connect to MT4: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            _marketDataTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            
            lock (_lockObject)
            {
                _marketDataCache.Clear();
            }

            _status.IsConnected = false;
            _status.MT4DataPath = null;
            _status.ConnectedAt = null;
            StatusChanged?.Invoke(this, new TradingServiceEventArgs { Status = _status });

            await Task.CompletedTask;
        }

        public Task<TradingServiceStatus> GetStatusAsync()
        {
            return Task.FromResult(_status);
        }

        public async Task<List<string>> GetSymbolsAsync()
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                throw new InvalidOperationException("Not connected to MT4");
            }

            var symbols = new List<string>();

            try
            {
                // Try to read symbols from MT4's symbol list
                // MT4 stores symbols in the terminal's common folder
                var commonPath = Path.Combine(_mt4DataPath, "..", "..", "common", "symbols.sel");
                if (File.Exists(commonPath))
                {
                    // This is a binary file, so we'll use an alternative approach
                    // Read from history files instead
                }

                // Alternative: Get symbols from history folder
                var historyPath = Path.Combine(_mt4DataPath, "history");
                if (Directory.Exists(historyPath))
                {
                    var brokerFolders = Directory.GetDirectories(historyPath);
                    foreach (var brokerFolder in brokerFolders)
                    {
                        var symbolFolders = Directory.GetDirectories(brokerFolder);
                        foreach (var symbolFolder in symbolFolders)
                        {
                            var symbolName = Path.GetFileName(symbolFolder);
                            if (!symbols.Contains(symbolName))
                            {
                                symbols.Add(symbolName);
                            }
                        }
                    }
                }

                // If no symbols found, add common ones
                if (symbols.Count == 0)
                {
                    symbols.AddRange(new[] { "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "USDCHF", "NZDUSD" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting symbols: {ex.Message}");
            }

            return await Task.FromResult(symbols);
        }

        public async Task<List<HistoricalBar>> GetHistoricalDataAsync(string symbol, TimeFrame timeFrame, DateTime startDate, DateTime endDate)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                throw new InvalidOperationException("Not connected to MT4");
            }

            var bars = new List<HistoricalBar>();

            try
            {
                // MT4 stores historical data in .hst files
                // Format: <symbol><timeframe>.hst (e.g., EURUSD60.hst for H1)
                var timeframeCode = GetTimeFrameCode(timeFrame);
                var fileName = $"{symbol}{timeframeCode}.hst";

                // Try to find the history file
                var historyPath = Path.Combine(_mt4DataPath, "history");
                var brokerFolders = Directory.GetDirectories(historyPath);

                foreach (var brokerFolder in brokerFolders)
                {
                    var symbolFolder = Path.Combine(brokerFolder, symbol);
                    if (Directory.Exists(symbolFolder))
                    {
                        var hstFile = Path.Combine(symbolFolder, fileName);
                        if (File.Exists(hstFile))
                        {
                            bars = ReadHstFile(hstFile, symbol, timeFrame, startDate, endDate);
                            break;
                        }
                    }
                }

                // If .hst file not found, try CSV export
                if (bars.Count == 0)
                {
                    bars = await ReadCsvHistoricalDataAsync(symbol, timeFrame, startDate, endDate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting historical data: {ex.Message}");
            }

            return bars;
        }

        private List<HistoricalBar> ReadHstFile(string filePath, string symbol, TimeFrame timeFrame, DateTime startDate, DateTime endDate)
        {
            var bars = new List<HistoricalBar>();

            try
            {
                using var file = File.OpenRead(filePath);
                using var reader = new BinaryReader(file);

                // Skip header (148 bytes)
                file.Seek(148, SeekOrigin.Begin);

                while (file.Position < file.Length)
                {
                    var time = DateTime.FromBinary(reader.ReadInt64());
                    var open = reader.ReadDouble();
                    var low = reader.ReadDouble();
                    var high = reader.ReadDouble();
                    var close = reader.ReadDouble();
                    var volume = reader.ReadInt64();
                    reader.ReadInt32(); // Skip spread
                    reader.ReadInt32(); // Skip real volume

                    if (time >= startDate && time <= endDate)
                    {
                        bars.Add(new HistoricalBar
                        {
                            Time = time,
                            Open = open,
                            High = high,
                            Low = low,
                            Close = close,
                            Volume = volume,
                            Symbol = symbol,
                            TimeFrame = timeFrame
                        });
                    }

                    if (time > endDate)
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading .hst file: {ex.Message}");
            }

            return bars.OrderBy(b => b.Time).ToList();
        }

        private async Task<List<HistoricalBar>> ReadCsvHistoricalDataAsync(string symbol, TimeFrame timeFrame, DateTime startDate, DateTime endDate)
        {
            var bars = new List<HistoricalBar>();

            try
            {
                // Check for CSV files in the command folder (exported by EA)
                var csvPath = Path.Combine(_mt4DataPath!, "MQL4", "Files", _commandFolder, $"{symbol}_{GetTimeFrameCode(timeFrame)}.csv");
                
                if (File.Exists(csvPath))
                {
                    var lines = await File.ReadAllLinesAsync(csvPath);
                    foreach (var line in lines.Skip(1)) // Skip header
                    {
                        var parts = line.Split(',');
                        if (parts.Length >= 6)
                        {
                            if (DateTime.TryParse(parts[0], out var time) &&
                                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var open) &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var high) &&
                                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var low) &&
                                double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var close) &&
                                long.TryParse(parts[5], out var volume))
                            {
                                if (time >= startDate && time <= endDate)
                                {
                                    bars.Add(new HistoricalBar
                                    {
                                        Time = time,
                                        Open = open,
                                        High = high,
                                        Low = low,
                                        Close = close,
                                        Volume = volume,
                                        Symbol = symbol,
                                        TimeFrame = timeFrame
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading CSV historical data: {ex.Message}");
            }

            return bars.OrderBy(b => b.Time).ToList();
        }

        public async Task<MarketData?> GetMarketDataAsync(string symbol)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return null;
            }

            lock (_lockObject)
            {
                if (_marketDataCache.TryGetValue(symbol, out var cached))
                {
                    return cached;
                }
            }

            // Try to read from file (updated by EA)
            try
            {
                var marketDataFile = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder, $"MarketData_{symbol}.txt");
                if (File.Exists(marketDataFile))
                {
                    var content = await File.ReadAllTextAsync(marketDataFile);
                    var parts = content.Split(',');
                    if (parts.Length >= 3 &&
                        double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var bid) &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ask))
                    {
                        var marketData = new MarketData
                        {
                            Symbol = symbol,
                            Bid = bid,
                            Ask = ask,
                            Spread = ask - bid,
                            LastUpdate = DateTime.Now
                        };

                        lock (_lockObject)
                        {
                            _marketDataCache[symbol] = marketData;
                        }

                        MarketDataUpdated?.Invoke(this, new MarketDataEventArgs { Symbol = symbol, MarketData = marketData });
                        return marketData;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting market data: {ex.Message}");
            }

            return null;
        }

        public async Task<BacktestResult> RunBacktestAsync(BacktestRequest request)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return new BacktestResult
                {
                    Success = false,
                    ErrorMessage = "Not connected to MT4"
                };
            }

            try
            {
                // Get historical data
                var bars = await GetHistoricalDataAsync(request.Symbol, request.TimeFrame, request.StartDate, request.EndDate);
                
                if (bars.Count == 0)
                {
                    return new BacktestResult
                    {
                        Success = false,
                        ErrorMessage = $"No historical data found for {request.Symbol}"
                    };
                }

                // Create a simple backtest engine
                var result = PerformBacktest(bars, request);

                // Save backtest result
                var resultFile = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder, 
                    $"Backtest_{request.StrategyName}_{DateTime.Now:yyyyMMddHHmmss}.json");
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(resultFile, json);

                return result;
            }
            catch (Exception ex)
            {
                return new BacktestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private BacktestResult PerformBacktest(List<HistoricalBar> bars, BacktestRequest request)
        {
            var result = new BacktestResult
            {
                InitialDeposit = request.InitialDeposit,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Trades = new List<Trade>(),
                EquityCurve = new List<EquityPoint>()
            };

            double balance = request.InitialDeposit;
            double equity = balance;
            double maxEquity = balance;
            double maxDrawdown = 0;
            double totalProfit = 0;
            double totalLoss = 0;
            var trades = new List<Trade>();

            // Simple moving average crossover strategy (example)
            // This is a placeholder - actual strategy logic should be in the EA code
            var fastMA = new List<double>();
            var slowMA = new List<double>();
            const int fastPeriod = 10;
            const int slowPeriod = 30;

            foreach (var bar in bars)
            {
                // Calculate moving averages
                fastMA.Add(bar.Close);
                slowMA.Add(bar.Close);

                if (fastMA.Count > fastPeriod)
                    fastMA.RemoveAt(0);
                if (slowMA.Count > slowPeriod)
                    slowMA.RemoveAt(0);

                if (fastMA.Count == fastPeriod && slowMA.Count == slowPeriod)
                {
                    var fastAvg = fastMA.Average();
                    var slowAvg = slowMA.Average();
                    var prevFastAvg = fastMA.Take(fastPeriod - 1).Average();
                    var prevSlowAvg = slowMA.Take(slowPeriod - 1).Average();

                    // Buy signal: fast MA crosses above slow MA
                    if (prevFastAvg <= prevSlowAvg && fastAvg > slowAvg)
                    {
                        // Close any open sell positions
                        var openSells = trades.Where(t => t.Type == TradeType.Sell && t.CloseTime == null).ToList();
                        foreach (var trade in openSells)
                        {
                            trade.ClosePrice = bar.Close;
                            trade.CloseTime = bar.Time;
                            var profit = (trade.OpenPrice - trade.ClosePrice.Value) * request.LotSize * 100000; // Simplified
                            trade.Profit = profit;
                            balance += profit;
                            totalProfit += Math.Max(0, profit);
                            totalLoss += Math.Min(0, profit);
                        }

                        // Open buy position
                        var buyTrade = new Trade
                        {
                            Ticket = trades.Count + 1,
                            Symbol = request.Symbol,
                            Type = TradeType.Buy,
                            Volume = request.LotSize,
                            OpenPrice = bar.Close,
                            OpenTime = bar.Time
                        };
                        trades.Add(buyTrade);
                    }
                    // Sell signal: fast MA crosses below slow MA
                    else if (prevFastAvg >= prevSlowAvg && fastAvg < slowAvg)
                    {
                        // Close any open buy positions
                        var openBuys = trades.Where(t => t.Type == TradeType.Buy && t.CloseTime == null).ToList();
                        foreach (var trade in openBuys)
                        {
                            trade.ClosePrice = bar.Close;
                            trade.CloseTime = bar.Time;
                            var profit = (trade.ClosePrice.Value - trade.OpenPrice) * request.LotSize * 100000; // Simplified
                            trade.Profit = profit;
                            balance += profit;
                            totalProfit += Math.Max(0, profit);
                            totalLoss += Math.Min(0, profit);
                        }

                        // Open sell position
                        var sellTrade = new Trade
                        {
                            Ticket = trades.Count + 1,
                            Symbol = request.Symbol,
                            Type = TradeType.Sell,
                            Volume = request.LotSize,
                            OpenPrice = bar.Close,
                            OpenTime = bar.Time
                        };
                        trades.Add(sellTrade);
                    }
                }

                // Update equity curve
                equity = balance;
                foreach (var openTrade in trades.Where(t => t.CloseTime == null))
                {
                    if (openTrade.Type == TradeType.Buy)
                        equity += (bar.Close - openTrade.OpenPrice) * openTrade.Volume * 100000;
                    else
                        equity += (openTrade.OpenPrice - bar.Close) * openTrade.Volume * 100000;
                }

                if (equity > maxEquity)
                    maxEquity = equity;

                var drawdown = maxEquity - equity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;

                result.EquityCurve.Add(new EquityPoint
                {
                    Time = bar.Time,
                    Equity = equity,
                    Balance = balance
                });
            }

            // Close any remaining open positions
            foreach (var openTrade in trades.Where(t => t.CloseTime == null))
            {
                var lastBar = bars.Last();
                openTrade.ClosePrice = lastBar.Close;
                openTrade.CloseTime = lastBar.Time;
                double profit;
                if (openTrade.Type == TradeType.Buy)
                    profit = (openTrade.ClosePrice.Value - openTrade.OpenPrice) * openTrade.Volume * 100000;
                else
                    profit = (openTrade.OpenPrice - openTrade.ClosePrice.Value) * openTrade.Volume * 100000;
                openTrade.Profit = profit;
                balance += profit;
                totalProfit += Math.Max(0, profit);
                totalLoss += Math.Min(0, profit);
            }

            result.FinalBalance = balance;
            result.NetProfit = balance - request.InitialDeposit;
            result.ProfitPercent = (result.NetProfit / request.InitialDeposit) * 100;
            result.TotalTrades = trades.Count;
            result.WinningTrades = trades.Count(t => t.Profit > 0);
            result.LosingTrades = trades.Count(t => t.Profit < 0);
            result.WinRate = result.TotalTrades > 0 ? (double)result.WinningTrades / result.TotalTrades * 100 : 0;
            result.MaxDrawdown = maxDrawdown;
            result.MaxDrawdownPercent = maxEquity > 0 ? (maxDrawdown / maxEquity) * 100 : 0;
            result.ProfitFactor = Math.Abs(totalLoss) > 0 ? totalProfit / Math.Abs(totalLoss) : 0;
            result.Trades = trades;
            result.Success = true;

            // Calculate Sharpe Ratio (simplified)
            if (result.EquityCurve.Count > 1)
            {
                var returns = new List<double>();
                for (int i = 1; i < result.EquityCurve.Count; i++)
                {
                    var prevEquity = result.EquityCurve[i - 1].Equity;
                    if (prevEquity > 0)
                    {
                        returns.Add((result.EquityCurve[i].Equity - prevEquity) / prevEquity);
                    }
                }
                if (returns.Count > 0)
                {
                    var avgReturn = returns.Average();
                    var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Sum() / returns.Count);
                    result.SharpeRatio = stdDev > 0 ? avgReturn / stdDev * Math.Sqrt(252) : 0; // Annualized
                }
            }

            return result;
        }

        public async Task<bool> CreateStrategyAsync(TradingStrategy strategy)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return false;
            }

            try
            {
                var expertsPath = Path.Combine(_mt4DataPath, "MQL4", _strategyFolder);
                var fileName = $"{strategy.Name.Replace(" ", "_")}.mq4";
                var filePath = Path.Combine(expertsPath, fileName);

                // If code is provided, use it; otherwise generate a template
                var code = !string.IsNullOrWhiteSpace(strategy.Code) 
                    ? strategy.Code 
                    : GenerateStrategyTemplate(strategy);

                await File.WriteAllTextAsync(filePath, code, Encoding.UTF8);

                // Save strategy metadata
                var metaFile = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder, $"Strategy_{strategy.Name}.json");
                var metaJson = JsonSerializer.Serialize(strategy, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metaFile, metaJson);

                System.Diagnostics.Debug.WriteLine($"Created strategy: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating strategy: {ex.Message}");
                return false;
            }
        }

        private string GenerateStrategyTemplate(TradingStrategy strategy)
        {
            var sb = new StringBuilder();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine($"//| {strategy.Name}.mq4 |");
            sb.AppendLine($"//| Generated by House Victoria |");
            sb.AppendLine($"//| {DateTime.Now:yyyy-MM-dd HH:mm:ss} |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine();
            sb.AppendLine("#property copyright \"House Victoria\"");
            sb.AppendLine("#property link      \"\"");
            sb.AppendLine("#property version   \"1.00\"");
            sb.AppendLine("#property strict");
            sb.AppendLine();
            sb.AppendLine("//--- Input parameters");
            sb.AppendLine("input double LotSize = 0.01;");
            sb.AppendLine("input int FastMA = 10;");
            sb.AppendLine("input int SlowMA = 30;");
            sb.AppendLine("input int MagicNumber = 123456;");
            sb.AppendLine();
            sb.AppendLine("//--- Global variables");
            sb.AppendLine("int fastMAHandle;");
            sb.AppendLine("int slowMAHandle;");
            sb.AppendLine();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("//| Expert initialization function |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("int OnInit()");
            sb.AppendLine("{");
            sb.AppendLine("    fastMAHandle = iMA(_Symbol, PERIOD_CURRENT, FastMA, 0, MODE_SMA, PRICE_CLOSE);");
            sb.AppendLine("    slowMAHandle = iMA(_Symbol, PERIOD_CURRENT, SlowMA, 0, MODE_SMA, PRICE_CLOSE);");
            sb.AppendLine("    ");
            sb.AppendLine("    if (fastMAHandle == INVALID_HANDLE || slowMAHandle == INVALID_HANDLE)");
            sb.AppendLine("    {");
            sb.AppendLine("        Print(\"Error creating indicators\");");
            sb.AppendLine("        return INIT_FAILED;");
            sb.AppendLine("    }");
            sb.AppendLine("    ");
            sb.AppendLine("    return INIT_SUCCEEDED;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("//| Expert deinitialization function |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("void OnDeinit(const int reason)");
            sb.AppendLine("{");
            sb.AppendLine("    IndicatorRelease(fastMAHandle);");
            sb.AppendLine("    IndicatorRelease(slowMAHandle);");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("//| Expert tick function |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("void OnTick()");
            sb.AppendLine("{");
            sb.AppendLine("    double fastMA[], slowMA[];");
            sb.AppendLine("    ArraySetAsSeries(fastMA, true);");
            sb.AppendLine("    ArraySetAsSeries(slowMA, true);");
            sb.AppendLine("    ");
            sb.AppendLine("    if (CopyBuffer(fastMAHandle, 0, 0, 2, fastMA) <= 0) return;");
            sb.AppendLine("    if (CopyBuffer(slowMAHandle, 0, 0, 2, slowMA) <= 0) return;");
            sb.AppendLine("    ");
            sb.AppendLine("    // Check for crossover");
            sb.AppendLine("    bool buySignal = fastMA[0] > slowMA[0] && fastMA[1] <= slowMA[1];");
            sb.AppendLine("    bool sellSignal = fastMA[0] < slowMA[0] && fastMA[1] >= slowMA[1];");
            sb.AppendLine("    ");
            sb.AppendLine("    if (buySignal)");
            sb.AppendLine("    {");
            sb.AppendLine("        ClosePositions(OP_SELL);");
            sb.AppendLine("        if (CountPositions(OP_BUY) == 0)");
            sb.AppendLine("            OpenPosition(OP_BUY);");
            sb.AppendLine("    }");
            sb.AppendLine("    ");
            sb.AppendLine("    if (sellSignal)");
            sb.AppendLine("    {");
            sb.AppendLine("        ClosePositions(OP_BUY);");
            sb.AppendLine("        if (CountPositions(OP_SELL) == 0)");
            sb.AppendLine("            OpenPosition(OP_SELL);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("//| Open position |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("void OpenPosition(int type)");
            sb.AppendLine("{");
            sb.AppendLine("    double price = (type == OP_BUY) ? Ask : Bid;");
            sb.AppendLine("    ");
            sb.AppendLine("    int ticket = OrderSend(_Symbol, type, LotSize, price, 3, 0, 0, \"HouseVictoria\", MagicNumber, 0, (type == OP_BUY) ? clrGreen : clrRed);");
            sb.AppendLine("    ");
            sb.AppendLine("    if (ticket > 0)");
            sb.AppendLine("        Print(\"Position opened: \", ticket);");
            sb.AppendLine("    else");
            sb.AppendLine("        Print(\"Error opening position: \", GetLastError());");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("//| Close positions |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("void ClosePositions(int type)");
            sb.AppendLine("{");
            sb.AppendLine("    for (int i = OrdersTotal() - 1; i >= 0; i--)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (OrderSelect(i, SELECT_BY_POS, MODE_TRADES))");
            sb.AppendLine("        {");
            sb.AppendLine("            if (OrderSymbol() == _Symbol && OrderMagicNumber() == MagicNumber && OrderType() == type)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (OrderClose(OrderTicket(), OrderLots(), (type == OP_BUY) ? Bid : Ask, 3))");
            sb.AppendLine("                    Print(\"Position closed: \", OrderTicket());");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("//| Count positions |");
            sb.AppendLine("//+------------------------------------------------------------------+");
            sb.AppendLine("int CountPositions(int type)");
            sb.AppendLine("{");
            sb.AppendLine("    int count = 0;");
            sb.AppendLine("    for (int i = 0; i < OrdersTotal(); i++)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (OrderSelect(i, SELECT_BY_POS, MODE_TRADES))");
            sb.AppendLine("        {");
            sb.AppendLine("            if (OrderSymbol() == _Symbol && OrderMagicNumber() == MagicNumber && OrderType() == type)");
            sb.AppendLine("                count++;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    return count;");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public async Task<List<TradingStrategy>> GetStrategiesAsync()
        {
            var strategies = new List<TradingStrategy>();

            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return strategies;
            }

            try
            {
                var metaFolder = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
                if (Directory.Exists(metaFolder))
                {
                    var metaFiles = Directory.GetFiles(metaFolder, "Strategy_*.json");
                    foreach (var file in metaFiles)
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(file);
                            var strategy = JsonSerializer.Deserialize<TradingStrategy>(json);
                            if (strategy != null)
                                strategies.Add(strategy);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting strategies: {ex.Message}");
            }

            return strategies;
        }

        public async Task<bool> ExecuteTradeAsync(TradeRequest request)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return false;
            }

            try
            {
                var commandFile = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder, 
                    $"Trade_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.json");
                
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(commandFile, json);

                System.Diagnostics.Debug.WriteLine($"Trade command written: {commandFile}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing trade: {ex.Message}");
                return false;
            }
        }

        public async Task<AccountInfo?> GetAccountInfoAsync()
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return null;
            }

            try
            {
                var accountFile = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder, "AccountInfo.json");
                if (File.Exists(accountFile))
                {
                    var json = await File.ReadAllTextAsync(accountFile);
                    return JsonSerializer.Deserialize<AccountInfo>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting account info: {ex.Message}");
            }

            return null;
        }

        public async Task<List<Position>> GetOpenPositionsAsync()
        {
            var positions = new List<Position>();

            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return positions;
            }

            try
            {
                var positionsFile = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder, "OpenPositions.json");
                if (File.Exists(positionsFile))
                {
                    var json = await File.ReadAllTextAsync(positionsFile);
                    var deserialized = JsonSerializer.Deserialize<List<Position>>(json);
                    if (deserialized != null)
                        positions = deserialized;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting open positions: {ex.Message}");
            }

            return positions;
        }

        private void UpdateMarketData(object? state)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
                return;

            // This would be called periodically to update market data
            // The actual data would come from MT4 EA files
        }

        private string GetTimeFrameCode(TimeFrame timeFrame)
        {
            return timeFrame switch
            {
                TimeFrame.M1 => "1",
                TimeFrame.M5 => "5",
                TimeFrame.M15 => "15",
                TimeFrame.M30 => "30",
                TimeFrame.H1 => "60",
                TimeFrame.H4 => "240",
                TimeFrame.D1 => "1440",
                TimeFrame.W1 => "10080",
                TimeFrame.MN1 => "43200",
                _ => "60"
            };
        }
    }
}
