using System.Globalization;

using System.Text.Json;

using System.Text.RegularExpressions;

using HouseVictoria.Core.Models;



namespace HouseVictoria.Services.Trading

{

    /// <summary>

    /// Shared helpers for MT4 file-bridge trade commands and responses.

    /// </summary>

    public static class Mt4TradeBridgeHelper

    {

        private static readonly Regex TicketRegex = new(

            @"Ticket:\s*(\d+)",

            RegexOptions.IgnoreCase | RegexOptions.Compiled);



        public const int DefaultVerifyTimeoutSeconds = 10;



        public static TradeExecutionResult ParseExecutionResponse(string commandId, string responseText)

        {

            var message = responseText.Trim();



            if (message.StartsWith('{'))

            {

                try

                {

                    using var doc = JsonDocument.Parse(message);

                    var root = doc.RootElement;

                    var reportedSuccess = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();

                    int? ticket = null;

                    if (root.TryGetProperty("ticket", out var ticketProp) && ticketProp.TryGetInt32(out var parsedTicket))

                        ticket = parsedTicket;

                    else if (root.TryGetProperty("Ticket", out var ticketProp2) && ticketProp2.TryGetInt32(out var parsedTicket2))

                        ticket = parsedTicket2;



                    string? brokerSymbol = null;

                    if (root.TryGetProperty("broker_symbol", out var brokerProp))

                        brokerSymbol = brokerProp.GetString();

                    else if (root.TryGetProperty("BrokerSymbol", out var brokerProp2))

                        brokerSymbol = brokerProp2.GetString();



                    var responseMessage = root.TryGetProperty("message", out var msgProp)

                        ? msgProp.GetString() ?? message

                        : message;



                    return new TradeExecutionResult

                    {

                        Success = reportedSuccess,

                        Message = responseMessage,

                        Ticket = ticket,

                        CommandId = commandId,

                        BrokerSymbol = brokerSymbol

                    };

                }

                catch (JsonException)

                {

                    // Fall through to legacy text parsing

                }

            }



            var success = message.Contains("executed successfully", StringComparison.OrdinalIgnoreCase);



            int? textTicket = null;

            var ticketMatch = TicketRegex.Match(message);

            if (ticketMatch.Success && int.TryParse(ticketMatch.Groups[1].Value, out var parsedTextTicket))

                textTicket = parsedTextTicket;



            string? textBrokerSymbol = null;

            if (message.Contains("Symbol:", StringComparison.OrdinalIgnoreCase))

            {

                var symbolPart = message.Split("Symbol:", 2, StringSplitOptions.None)[1].Trim();

                if (!string.IsNullOrWhiteSpace(symbolPart))

                    textBrokerSymbol = symbolPart.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

            }



            return new TradeExecutionResult

            {

                Success = success,

                Message = message,

                Ticket = textTicket,

                CommandId = commandId,

                BrokerSymbol = textBrokerSymbol

            };

        }



        public static TradeExecutionResult ApplyTicketVerification(

            TradeExecutionResult result,

            IReadOnlyList<Position> positions,

            int verifyTimeoutSeconds = DefaultVerifyTimeoutSeconds)

        {

            if (!result.Success || result.Ticket is not int ticket)

            {

                result.Verified = false;

                return result;

            }



            if (PositionHasTicket(positions, ticket))

            {

                result.Verified = true;

                var match = positions.First(p => p.Ticket == ticket);

                if (string.IsNullOrWhiteSpace(result.BrokerSymbol))

                    result.BrokerSymbol = match.Symbol;

                return result;

            }



            result.Success = false;

            result.Verified = false;

            result.Message =

                $"Ghost execution rejected: EA reported ticket {ticket} but it never appeared " +

                $"in OpenPositions.json within {verifyTimeoutSeconds}s. Original response: {result.Message}";

            return result;

        }



        public static async Task<TradeExecutionResult> VerifyTicketAsync(

            TradeExecutionResult result,

            Func<Task<List<Position>>> loadPositionsAsync,

            int verifyTimeoutSeconds = DefaultVerifyTimeoutSeconds,

            CancellationToken cancellationToken = default)

        {

            if (!result.Success || result.Ticket is not int ticket)

            {

                result.Verified = false;

                return result;

            }



            var deadline = DateTime.UtcNow.AddSeconds(verifyTimeoutSeconds);

            while (DateTime.UtcNow < deadline)

            {

                cancellationToken.ThrowIfCancellationRequested();

                var positions = await loadPositionsAsync().ConfigureAwait(false);

                if (PositionHasTicket(positions, ticket))

                {

                    result.Verified = true;

                    var match = positions.First(p => p.Ticket == ticket);

                    if (string.IsNullOrWhiteSpace(result.BrokerSymbol))

                        result.BrokerSymbol = match.Symbol;

                    return result;

                }



                await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            }



            result.Success = false;

            result.Verified = false;

            result.Message =

                $"Ghost execution rejected: EA reported ticket {ticket} but it never appeared " +

                $"in OpenPositions.json within {verifyTimeoutSeconds}s. Original response: {result.Message}";

            return result;

        }



        public static bool PositionHasTicket(IReadOnlyList<Position> positions, int ticket) =>

            positions.Any(p => p.Ticket == ticket);



        public static Dictionary<string, string> LoadSymbolMap(string commandRoot)

        {

            var mapFile = Path.Combine(commandRoot, "SymbolMap.json");

            if (!File.Exists(mapFile))

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);



            try

            {

                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(mapFile));

                if (raw == null)

                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);



                return raw.ToDictionary(

                    kvp => kvp.Key.ToUpperInvariant(),

                    kvp => kvp.Value,

                    StringComparer.OrdinalIgnoreCase);

            }

            catch

            {

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            }

        }



        public static List<string> LoadAvailableSymbols(string commandRoot)

        {

            var listFile = Path.Combine(commandRoot, "SymbolsAvailable.json");

            if (!File.Exists(listFile))

                return new List<string>();



            try

            {

                return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(listFile)) ?? new List<string>();

            }

            catch

            {

                return new List<string>();

            }

        }



        public static string? ResolveMarketDataFile(string commandRoot, string symbol)

        {

            var baseSymbol = symbol.ToUpperInvariant();

            var direct = Path.Combine(commandRoot, $"MarketData_{baseSymbol}.txt");

            if (File.Exists(direct))

                return direct;



            var symbolMap = LoadSymbolMap(commandRoot);

            if (symbolMap.TryGetValue(baseSymbol, out var brokerSymbol))

            {

                var mapped = Path.Combine(commandRoot, $"MarketData_{brokerSymbol.ToUpperInvariant()}.txt");

                if (File.Exists(mapped))

                    return mapped;

            }



            return null;

        }



        public static TradeRequest? TryParseTradeRequest(string? text)

        {

            if (string.IsNullOrWhiteSpace(text))

                return null;



            var json = ExtractTradeJson(text);

            if (string.IsNullOrWhiteSpace(json))

                return null;



            try

            {

                return JsonSerializer.Deserialize<TradeRequest>(json, new JsonSerializerOptions

                {

                    PropertyNameCaseInsensitive = true

                });

            }

            catch

            {

                return null;

            }

        }



        public static string? ExtractTradeJson(string text)

        {

            var fenced = Regex.Match(

                text,

                @"```(?:trade|json)\s*([\s\S]*?)```",

                RegexOptions.IgnoreCase);



            if (fenced.Success)

                return fenced.Groups[1].Value.Trim();



            var inline = Regex.Match(text, @"\{[^{}]*""Symbol""[^{}]*\}", RegexOptions.IgnoreCase);

            return inline.Success ? inline.Value.Trim() : null;

        }



        public static string FormatBridgeStatus(TradingServiceStatus status, AccountInfo? account)

        {

            var accountLine = account != null

                ? $"Account #{account.AccountNumber}, balance {account.Balance:F2} {account.Currency}"

                : "Account info unavailable";



            return

                $"MT4 connected={status.IsConnected}, bridge active={status.IsBridgeActive}, " +

                $"path={status.MT4DataPath ?? "n/a"}, {accountLine}";

        }



        public static string? FindLatestResponseSince(string responsesDir, string commandId, DateTime notBeforeUtc)

        {

            if (!Directory.Exists(responsesDir))

                return null;



            string? bestPath = null;

            DateTime bestTime = DateTime.MinValue;



            foreach (var file in Directory.EnumerateFiles(responsesDir, "Response_*.txt"))

            {

                var name = Path.GetFileNameWithoutExtension(file);

                if (name.Equals($"Response_{commandId}", StringComparison.OrdinalIgnoreCase))

                    return File.ReadAllText(file);



                var writeUtc = File.GetLastWriteTimeUtc(file);

                if (writeUtc >= notBeforeUtc && writeUtc > bestTime)

                {

                    bestTime = writeUtc;

                    bestPath = file;

                }

            }



            return bestPath != null ? File.ReadAllText(bestPath) : null;

        }



        public static MarketData? ParseMarketDataFile(string filePath, string symbol)

        {

            var content = File.ReadAllText(filePath);

            var parts = content.Split(',');

            if (parts.Length < 2 ||

                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var bid) ||

                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ask))

            {

                return null;

            }



            return new MarketData

            {

                Symbol = symbol,

                Bid = bid,

                Ask = ask,

                Spread = ask - bid,

                LastUpdate = File.GetLastWriteTime(filePath)

            };

        }

        public static string? ExtractBacktestJson(string text)
        {
            var fenced = Regex.Match(
                text,
                @"```(?:backtest|json)\s*([\s\S]*?)```",
                RegexOptions.IgnoreCase);

            if (fenced.Success)
                return fenced.Groups[1].Value.Trim();

            var inline = Regex.Match(text, @"\{[^{}]*""(?:symbol|Symbol)""[^{}]*\}", RegexOptions.IgnoreCase);
            return inline.Success ? inline.Value.Trim() : null;
        }

        public static BacktestRequest? TryParseBacktestRequest(string? text)
        {
            var json = ExtractBacktestJson(text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var request = new BacktestRequest
                {
                    StrategyName = GetJsonString(root, "strategy_name", "strategyName", "name") ?? "AutonomyBacktest",
                    Symbol = GetJsonString(root, "symbol", "Symbol") ?? "EURUSD",
                    TimeFrame = ParseTimeFrame(GetJsonString(root, "time_frame", "timeFrame", "timeframe") ?? "H1"),
                    StartDate = ParseJsonDate(root, "start_date", "startDate") ?? DateTime.UtcNow.AddMonths(-6),
                    EndDate = ParseJsonDate(root, "end_date", "endDate") ?? DateTime.UtcNow,
                    InitialDeposit = GetJsonDouble(root, 10000, "initial_deposit", "initialDeposit"),
                    LotSize = GetJsonDouble(root, 0.01, "lot_size", "lotSize", "volume")
                };

                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        var key = prop.Name;
                        if (key is "strategy_name" or "strategyName" or "name" or "symbol" or "Symbol" or
                            "time_frame" or "timeFrame" or "timeframe" or "start_date" or "startDate" or
                            "end_date" or "endDate" or "initial_deposit" or "initialDeposit" or
                            "lot_size" or "lotSize" or "volume")
                            continue;

                        request.StrategyParameters[key] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.Number when prop.Value.TryGetInt64(out var n) => n,
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                            _ => prop.Value.ToString()
                        };
                    }
                }

                if (!request.StrategyParameters.ContainsKey("strategy_type"))
                {
                    var type = GetJsonString(root, "strategy_type", "strategyType", "type");
                    if (!string.IsNullOrWhiteSpace(type))
                        request.StrategyParameters["strategy_type"] = type;
                }

                return request;
            }
            catch
            {
                return null;
            }
        }

        public static string FormatBacktestSummary(BacktestResult result)
        {
            if (!result.Success)
                return $"Backtest failed: {result.ErrorMessage}";

            return
                $"Backtest OK ({result.StrategyTypeUsed ?? "unknown"}): " +
                $"bars={result.BarsProcessed}, trades={result.TotalTrades}, winRate={result.WinRate:F1}%, " +
                $"netProfit={result.NetProfit:F2} ({result.ProfitPercent:F2}%), maxDD={result.MaxDrawdownPercent:F2}%, " +
                $"profitFactor={result.ProfitFactor:F2}, sharpe={result.SharpeRatio:F2}";
        }

        private static string? GetJsonString(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }

            return null;
        }

        private static double GetJsonDouble(JsonElement root, double defaultValue, params string[] names)
        {
            foreach (var name in names)
            {
                if (!root.TryGetProperty(name, out var prop))
                    continue;

                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var n))
                    return n;
                if (prop.ValueKind == JsonValueKind.String &&
                    double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        private static DateTime? ParseJsonDate(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (!root.TryGetProperty(name, out var prop))
                    continue;

                if (prop.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(prop.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                    return dt;
            }

            return null;
        }

        public static TimeFrame ParseTimeFrame(string value) =>
            value.ToUpperInvariant() switch
            {
                "M1" or "1" => TimeFrame.M1,
                "M5" or "5" => TimeFrame.M5,
                "M15" or "15" => TimeFrame.M15,
                "M30" or "30" => TimeFrame.M30,
                "H1" or "60" => TimeFrame.H1,
                "H4" or "240" => TimeFrame.H4,
                "D1" or "1440" => TimeFrame.D1,
                "W1" or "10080" => TimeFrame.W1,
                "MN1" or "43200" => TimeFrame.MN1,
                _ => TimeFrame.H1
            };

        public static HistoricalExportResult ParseHistoryExportResponse(string commandId, string symbol, string responseText)
        {
            var message = responseText.Trim();
            var result = new HistoricalExportResult
            {
                CommandId = commandId,
                Symbol = symbol.ToUpperInvariant()
            };

            if (!message.StartsWith('{'))
            {
                result.Success = message.Contains("Exported", StringComparison.OrdinalIgnoreCase);
                result.Message = message;
                return result;
            }

            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                result.Success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
                result.BarsExported = GetJsonInt(root, 0, "bars_exported", "barsExported");
                result.CsvFileName = GetJsonString(root, "csv_file", "csvFile");
                result.Message = GetJsonString(root, "message") ?? message;
            }
            catch
            {
                result.Success = false;
                result.Message = message;
            }

            return result;
        }

        private static int GetJsonInt(JsonElement root, int defaultValue, params string[] names)
        {
            foreach (var name in names)
            {
                if (!root.TryGetProperty(name, out var prop))
                    continue;

                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
                    return n;
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                    return parsed;
            }

            return defaultValue;
        }
    }

}


