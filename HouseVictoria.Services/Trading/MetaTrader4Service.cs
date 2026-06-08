using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Trading.Backtest;
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

                var resolvedPath = Mt4PathResolver.Resolve(mt4DataPath);
                if (!Mt4PathResolver.IsWritableTerminalDataPath(resolvedPath))
                {
                    throw new UnauthorizedAccessException(
                        $"MT4 terminal data path is not writable: {resolvedPath}");
                }

                _mt4DataPath = resolvedPath;
                Mt4BridgeInstaller.EnsureExpertAdvisor(resolvedPath);

                // Create communication folders
                var commandPath = Path.Combine(resolvedPath, "MQL4", "Files", _commandFolder);
                Directory.CreateDirectory(commandPath);

                var responsePath = Path.Combine(resolvedPath, "MQL4", "Files", _commandFolder, "Responses");
                Directory.CreateDirectory(responsePath);

                // Verify MT4 structure
                var expertsPath = Path.Combine(resolvedPath, "MQL4", _strategyFolder);
                if (!Directory.Exists(expertsPath))
                {
                    Directory.CreateDirectory(expertsPath);
                }

                RefreshBridgeActivityStatus();

                _status.IsConnected = true;
                _status.MT4DataPath = resolvedPath;
                _status.ConnectedAt = DateTime.Now;
                _status.LastError = null;

                StatusChanged?.Invoke(this, new TradingServiceEventArgs { Status = _status });

                // Start market data updates
                _marketDataTimer?.Change(0, 5000);

                if (!string.Equals(resolvedPath, mt4DataPath.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"MT4 path resolved: {mt4DataPath} -> {resolvedPath}");
                }

                System.Diagnostics.Debug.WriteLine($"Connected to MT4 at: {resolvedPath}");
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

            var commandPath = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
            var fromBridge = Mt4TradeBridgeHelper.LoadAvailableSymbols(commandPath);
            if (fromBridge.Count > 0)
                return await Task.FromResult(fromBridge);

            var symbolMap = Mt4TradeBridgeHelper.LoadSymbolMap(commandPath);
            if (symbolMap.Count > 0)
                return await Task.FromResult(symbolMap.Keys.ToList());

            var symbols = new List<string>();

            try
            {
                // Fallback: read broker-specific names from history folders
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

            return await Mt4HistoricalDataReader.LoadBarsAsync(
                _mt4DataPath,
                symbol,
                timeFrame,
                startDate,
                endDate,
                _commandFolder).ConfigureAwait(false);
        }

        public async Task<HistoricalExportResult> ExportHistoricalDataAsync(
            string symbol,
            TimeFrame timeFrame,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return new HistoricalExportResult
                {
                    Success = false,
                    Message = "Not connected to MT4",
                    Symbol = symbol
                };
            }

            if (!_status.IsBridgeActive)
            {
                return new HistoricalExportResult
                {
                    Success = false,
                    Message = "MT4 bridge EA is not active. Attach HouseVictoriaBridge with AutoTrading enabled.",
                    Symbol = symbol
                };
            }

            try
            {
                var commandPath = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
                Directory.CreateDirectory(Path.Combine(commandPath, "Responses"));

                var commandId = $"History_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
                var commandFile = Path.Combine(commandPath, $"{commandId}.json");
                var responseFile = Path.Combine(commandPath, "Responses", $"Response_{commandId}.txt");
                var tfMinutes = int.Parse(Mt4HistoricalDataReader.GetTimeFrameCode(timeFrame), CultureInfo.InvariantCulture);

                var payload = new Dictionary<string, object>
                {
                    ["Symbol"] = symbol.ToUpperInvariant(),
                    ["TimeFrame"] = tfMinutes,
                    ["StartDate"] = startDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ["EndDate"] = endDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                };

                await AtomicWriteAllTextAsync(
                    commandFile,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
                    cancellationToken).ConfigureAwait(false);

                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (File.Exists(responseFile))
                    {
                        var responseText = await File.ReadAllTextAsync(responseFile, cancellationToken).ConfigureAwait(false);
                        var parsed = Mt4TradeBridgeHelper.ParseHistoryExportResponse(commandId, symbol, responseText);
                        return parsed;
                    }

                    if (!File.Exists(commandFile))
                    {
                        var fallback = Mt4TradeBridgeHelper.FindLatestResponseSince(
                            Path.Combine(commandPath, "Responses"),
                            commandId,
                            DateTime.UtcNow.AddSeconds(-65));
                        if (fallback != null)
                            return Mt4TradeBridgeHelper.ParseHistoryExportResponse(commandId, symbol, fallback);
                    }

                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }

                return new HistoricalExportResult
                {
                    Success = false,
                    CommandId = commandId,
                    Symbol = symbol,
                    Message = "Timed out waiting for MT4 history export response."
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new HistoricalExportResult
                {
                    Success = false,
                    Symbol = symbol,
                    Message = $"History export failed: {ex.Message}"
                };
            }
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
                var commandPath = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
                var marketDataFile = Mt4TradeBridgeHelper.ResolveMarketDataFile(commandPath, symbol);
                if (marketDataFile != null)
                {
                    var marketData = Mt4TradeBridgeHelper.ParseMarketDataFile(marketDataFile, symbol);
                    if (marketData != null)
                    {
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

        public async Task<BacktestResult> RunBacktestAsync(BacktestRequest request, CancellationToken cancellationToken = default)
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
                        ErrorMessage =
                            $"No historical data found for {request.Symbol}. " +
                            "Use ExportHistoricalDataAsync or download history in MT4 History Center."
                    };
                }

                var config = BacktestStrategyConfig.FromParameters(request.StrategyParameters);
                var result = BacktestEngine.Run(bars, request);
                result.StrategyTypeUsed = config.StrategyType;
                result.BarsProcessed = bars.Count;

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

        public async Task<TradeExecutionResult> ExecuteTradeAsync(
            TradeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_status.IsConnected || _mt4DataPath == null)
            {
                return new TradeExecutionResult
                {
                    Success = false,
                    Message = "Not connected to MT4"
                };
            }

            if (!_status.IsBridgeActive)
            {
                return new TradeExecutionResult
                {
                    Success = false,
                    Message = "MT4 bridge EA is not active (no recent file updates). Attach HouseVictoriaBridge to a chart with AutoTrading enabled."
                };
            }

            if (string.IsNullOrWhiteSpace(request.Symbol) || request.Volume <= 0)
            {
                return new TradeExecutionResult
                {
                    Success = false,
                    Message = "Invalid trade request: Symbol and positive Volume are required."
                };
            }

            try
            {
                var commandPath = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
                var symbolMap = Mt4TradeBridgeHelper.LoadSymbolMap(commandPath);
                var baseSymbol = request.Symbol.ToUpperInvariant();
                if (!symbolMap.ContainsKey(baseSymbol) &&
                    !File.Exists(Path.Combine(commandPath, "SymbolsAvailable.json")))
                {
                    return new TradeExecutionResult
                    {
                        Success = false,
                        Message =
                            $"Cannot resolve {baseSymbol}: symbol map unavailable. " +
                            "List symbols from MT4 first or add the pair to Market Watch."
                    };
                }

                var commandId = $"Trade_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
                var commandFile = Path.Combine(commandPath, $"{commandId}.json");
                var responseFile = Path.Combine(commandPath, "Responses", $"Response_{commandId}.txt");

                Directory.CreateDirectory(Path.Combine(commandPath, "Responses"));

                // Stamp the target account so the EA can reject wrong-terminal routing.
                var account = await GetAccountInfoAsync().ConfigureAwait(false);
                var payload = new Dictionary<string, object?>
                {
                    ["Symbol"] = request.Symbol.ToUpperInvariant(),
                    ["Type"] = (int)request.Type,
                    ["Volume"] = request.Volume,
                };
                if (request.StopLoss.HasValue)
                    payload["StopLoss"] = request.StopLoss.Value;
                if (request.TakeProfit.HasValue)
                    payload["TakeProfit"] = request.TakeProfit.Value;
                if (account != null && account.AccountNumber != 0)
                    payload["AccountNumber"] = account.AccountNumber;

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await AtomicWriteAllTextAsync(commandFile, json, cancellationToken).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine($"Trade command written: {commandFile}");

                var deadline = DateTime.UtcNow.AddSeconds(30);
                var responsesDir = Path.Combine(commandPath, "Responses");
                TradeExecutionResult? parsed = null;
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (File.Exists(responseFile))
                    {
                        var responseText = await File.ReadAllTextAsync(responseFile, cancellationToken).ConfigureAwait(false);
                        parsed = Mt4TradeBridgeHelper.ParseExecutionResponse(commandId, responseText);
                        break;
                    }

                    var fallback = Mt4TradeBridgeHelper.FindLatestResponseSince(responsesDir, commandId, DateTime.UtcNow.AddSeconds(-35));
                    if (fallback != null)
                    {
                        parsed = Mt4TradeBridgeHelper.ParseExecutionResponse(commandId, fallback);
                        break;
                    }

                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }

                if (parsed != null)
                {
                    if (symbolMap.TryGetValue(baseSymbol, out var brokerHint) &&
                        string.IsNullOrWhiteSpace(parsed.BrokerSymbol))
                    {
                        parsed.BrokerSymbol = brokerHint;
                    }

                    return await Mt4TradeBridgeHelper.VerifyTicketAsync(
                        parsed,
                        () => GetOpenPositionsAsync(),
                        Mt4TradeBridgeHelper.DefaultVerifyTimeoutSeconds,
                        cancellationToken).ConfigureAwait(false);
                }

                if (!File.Exists(commandFile))
                {
                    return new TradeExecutionResult
                    {
                        Success = false,
                        Verified = false,
                        CommandId = commandId,
                        Message = "Trade command was consumed by MT4 but no response file appeared within 30 seconds."
                    };
                }

                return new TradeExecutionResult
                {
                    Success = false,
                    Verified = false,
                    CommandId = commandId,
                    Message = "Timed out waiting for MT4 EA response. Ensure HouseVictoriaBridge is attached to a ticking chart and AutoTrading is enabled."
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing trade: {ex.Message}");
                return new TradeExecutionResult
                {
                    Success = false,
                    Message = $"Error executing trade: {ex.Message}"
                };
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

            try
            {
                var commandPath = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
                if (!Directory.Exists(commandPath))
                    return;

                foreach (var file in Directory.EnumerateFiles(commandPath, "MarketData_*.txt"))
                {
                    var symbol = Path.GetFileName(file).Replace("MarketData_", "", StringComparison.Ordinal)
                        .Replace(".txt", "", StringComparison.Ordinal);
                    if (string.IsNullOrWhiteSpace(symbol))
                        continue;

                    try
                    {
                        var content = File.ReadAllText(file);
                        var parts = content.Split(',');
                        if (parts.Length < 3 ||
                            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var bid) ||
                            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ask))
                        {
                            continue;
                        }

                        var marketData = new MarketData
                        {
                            Symbol = symbol,
                            Bid = bid,
                            Ask = ask,
                            Spread = ask - bid,
                            LastUpdate = File.GetLastWriteTime(file)
                        };

                        lock (_lockObject)
                        {
                            _marketDataCache[symbol] = marketData;
                        }

                        MarketDataUpdated?.Invoke(this, new MarketDataEventArgs
                        {
                            Symbol = symbol,
                            MarketData = marketData
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading {file}: {ex.Message}");
                    }
                }

                RefreshBridgeActivityStatus();
                StatusChanged?.Invoke(this, new TradingServiceEventArgs { Status = _status });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating market data: {ex.Message}");
            }
        }

        /// <summary>
        /// Writes a command file atomically (temp + rename) so the EA never reads a
        /// half-written file. The temp name does not match the EA's command globs.
        /// </summary>
        private static async Task AtomicWriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            var dir = Path.GetDirectoryName(path) ?? ".";
            var tmp = Path.Combine(dir, "." + Path.GetFileName(path) + ".writing");
            await File.WriteAllTextAsync(tmp, content, cancellationToken).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
        }

        private void RefreshBridgeActivityStatus()
        {
            if (_mt4DataPath == null)
            {
                _status.IsBridgeActive = false;
                _status.BridgeLastActivityUtc = null;
                return;
            }

            var commandPath = Path.Combine(_mt4DataPath, "MQL4", "Files", _commandFolder);
            if (!Directory.Exists(commandPath))
            {
                _status.IsBridgeActive = false;
                _status.BridgeLastActivityUtc = null;
                return;
            }

            DateTime? latest = null;
            foreach (var file in Directory.EnumerateFiles(commandPath, "*", SearchOption.AllDirectories))
            {
                var writeUtc = File.GetLastWriteTimeUtc(file);
                if (latest == null || writeUtc > latest)
                    latest = writeUtc;
            }

            _status.BridgeLastActivityUtc = latest;
            _status.IsBridgeActive = latest.HasValue &&
                                     DateTime.UtcNow - latest.Value <= TimeSpan.FromSeconds(30);
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
