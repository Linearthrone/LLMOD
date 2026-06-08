//+------------------------------------------------------------------+
//|                                          HouseVictoriaBridge.mq4 |
//|                        House Victoria MT4 Communication Bridge   |
//|                                                                  |
//+------------------------------------------------------------------+
#property copyright "House Victoria"
#property link      ""
#property version   "1.10"
#property strict

//--- Input parameters
input int UpdateIntervalSeconds = 5;       // Market data update interval (seconds)
input int TimerIntervalSeconds  = 1;       // Command/heartbeat poll interval (seconds)
input bool EnableTradeExecution = true;    // Allow trade execution from House Victoria
input int MagicNumber           = 123456;  // Magic number for House Victoria trades

//--- Live-trading safety guardrails
input int    ExpectedAccount      = 0;     // If !=0, EA only trades when login == this account
input double MaxLotSize           = 1.0;   // Reject any order with volume above this
input bool   RequireStopLoss      = true;  // Reject market orders without a stop-loss
input int    MaxOpenPositions     = 10;    // Reject new orders beyond this many HV positions
input int    MaxOrdersPerInterval = 5;     // Rate limit: max new orders per window
input int    OrderIntervalSeconds = 60;    // Rate-limit window (seconds)

//--- Global variables
string CommandFolder = "HouseVictoria";
string ResponseFolder = "HouseVictoria/Responses";
uint   LastMarketDataMs = 0;
bool   AccountGuardOk = true;              // false => wrong login, all trading blocked

//--- Order rate-limit ring buffer (millisecond timestamps of recent sends)
#define HV_ORDER_LOG_SIZE 256
uint   OrderLogMs[HV_ORDER_LOG_SIZE];
int    OrderLogCount = 0;

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
    Print("House Victoria Bridge v1.10 initialized");
    Print("Command folder: ", CommandFolder);
    Print("Magic number: ", MagicNumber, " Account: ", AccountNumber());

    ArrayInitialize(OrderLogMs, 0);
    OrderLogCount = 0;

    // Account guard: refuse to trade if attached to an unexpected login.
    AccountGuardOk = (ExpectedAccount == 0 || ExpectedAccount == (int)AccountNumber());
    if (!AccountGuardOk)
    {
        Print("House Victoria: ACCOUNT GUARD ACTIVE - ExpectedAccount=", ExpectedAccount,
              " but terminal login=", AccountNumber(), ". Trade execution is BLOCKED.");
    }

    ExportSymbolMap(true);

    // Ensure response folder exists
    string probe = ResponseFolder + "/.bridge_ready";
    int probeHandle = FileOpen(probe, FILE_WRITE | FILE_TXT);
    if (probeHandle != INVALID_HANDLE)
    {
        FileWriteString(probeHandle, "ok");
        FileClose(probeHandle);
    }
    else
    {
        Print("House Victoria: failed to initialize response folder: ", ResponseFolder, " error=", GetLastError());
    }

    // Drive all bridge work from a timer so commands are processed even when
    // the chart symbol is not ticking (quiet/closed market, illiquid symbol).
    int timerSec = (TimerIntervalSeconds > 0) ? TimerIntervalSeconds : 1;
    EventSetTimer(timerSec);

    WriteHeartbeat();
    return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    EventKillTimer();
    Print("House Victoria Bridge deinitialized (reason=", reason, ")");
}

//+------------------------------------------------------------------+
//| Timer: tick-independent bridge processing                        |
//+------------------------------------------------------------------+
void OnTimer()
{
    // Commands every timer tick.
    ProcessCommands();

    // Market data on its own interval.
    if (GetTickCount() - LastMarketDataMs >= (uint)(UpdateIntervalSeconds * 1000))
    {
        UpdateMarketData();
        LastMarketDataMs = GetTickCount();
    }

    // Account / positions / heartbeat (each internally throttled).
    UpdateAccountInfo();
    UpdateOpenPositions();
    WriteHeartbeat();
}

//+------------------------------------------------------------------+
//| Expert tick function (kept light; timer does the work)           |
//+------------------------------------------------------------------+
void OnTick()
{
    // Refresh quotes promptly while the market is ticking.
    if (GetTickCount() - LastMarketDataMs >= (uint)(UpdateIntervalSeconds * 1000))
    {
        UpdateMarketData();
        LastMarketDataMs = GetTickCount();
    }
}

//+------------------------------------------------------------------+
//| Write heartbeat status file for consumer watchdogs               |
//+------------------------------------------------------------------+
void WriteHeartbeat()
{
    string fileName = CommandFolder + "/Heartbeat.json";
    int handle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
    if (handle == INVALID_HANDLE)
        return;

    string json = "{";
    json += "\"timestamp\":\"" + TimeToString(TimeCurrent(), TIME_DATE | TIME_SECONDS) + "\",";
    json += "\"local_time\":\"" + TimeToString(TimeLocal(), TIME_DATE | TIME_SECONDS) + "\",";
    json += "\"account\":" + IntegerToString(AccountNumber()) + ",";
    json += "\"account_guard_ok\":" + (AccountGuardOk ? "true" : "false") + ",";
    json += "\"expected_account\":" + IntegerToString(ExpectedAccount) + ",";
    json += "\"trade_execution_enabled\":" + (EnableTradeExecution ? "true" : "false") + ",";
    json += "\"expert_enabled\":" + (IsExpertEnabled() ? "true" : "false") + ",";
    json += "\"trade_allowed\":" + (IsTradeAllowed() ? "true" : "false") + ",";
    json += "\"open_positions\":" + IntegerToString(CountHouseVictoriaPositions()) + ",";
    json += "\"version\":\"1.10\"";
    json += "}";

    FileWriteString(handle, json);
    FileClose(handle);
}

//+------------------------------------------------------------------+
//| Count currently open House Victoria positions                    |
//+------------------------------------------------------------------+
int CountHouseVictoriaPositions()
{
    int count = 0;
    for (int i = 0; i < OrdersTotal(); i++)
    {
        if (OrderSelect(i, SELECT_BY_POS, MODE_TRADES) && OrderMagicNumber() == MagicNumber)
            count++;
    }
    return count;
}

//+------------------------------------------------------------------+
//| Record a successful order send for rate limiting                 |
//+------------------------------------------------------------------+
void RecordOrderSend()
{
    OrderLogMs[OrderLogCount % HV_ORDER_LOG_SIZE] = GetTickCount();
    OrderLogCount++;
}

//+------------------------------------------------------------------+
//| Count orders sent within the rate-limit window                   |
//+------------------------------------------------------------------+
int RecentOrderCount()
{
    uint now = GetTickCount();
    uint windowMs = (uint)(OrderIntervalSeconds * 1000);
    int recent = 0;
    int scan = (OrderLogCount < HV_ORDER_LOG_SIZE) ? OrderLogCount : HV_ORDER_LOG_SIZE;
    for (int i = 0; i < scan; i++)
    {
        if (OrderLogMs[i] != 0 && (now - OrderLogMs[i]) <= windowMs)
            recent++;
    }
    return recent;
}

//+------------------------------------------------------------------+
//| Validate a command's target account against this terminal        |
//| Returns "" if OK, otherwise a rejection reason.                  |
//+------------------------------------------------------------------+
string CheckAccountGuard(string json)
{
    if (!AccountGuardOk)
        return "Account guard: EA attached to login " + IntegerToString(AccountNumber()) +
               " but ExpectedAccount is " + IntegerToString(ExpectedAccount) + ". Trading blocked.";

    string acctStr = SanitizeJsonString(ExtractJsonValue(json, "AccountNumber"));
    if (acctStr == "")
        acctStr = SanitizeJsonString(ExtractJsonValue(json, "account"));
    if (acctStr != "")
    {
        long target = StringToInteger(acctStr);
        if (target != 0 && target != AccountNumber())
            return "Account mismatch: command targeted account " + acctStr +
                   " but this terminal is login " + IntegerToString(AccountNumber()) +
                   ". Command refused (possible wrong-terminal routing).";
    }
    return "";
}

//+------------------------------------------------------------------+
//| Resolve a user/base symbol to this broker's Market Watch name     |
//| (e.g. EURUSD -> EURUSD.pro). Tries exact, suffixes, prefix scan. |
//+------------------------------------------------------------------+
string ResolveBrokerSymbol(string requested)
{
    string sym = requested;
    StringTrimLeft(sym);
    StringTrimRight(sym);
    if (StringLen(sym) < 3)
        return "";

    if (SymbolSelect(sym, true))
        return sym;

    string suffixes[] = {".pro", ".PRO", ".m", ".M", ".raw", ".i", ".c", ".e", "_i", "-pro", ".micro", ".std", ".ecn", ".fx"};
    int s;
    for (s = 0; s < ArraySize(suffixes); s++)
    {
        string candidate = sym + suffixes[s];
        if (SymbolSelect(candidate, true))
            return candidate;
    }

    int total = SymbolsTotal(true);
    int i;
    for (i = 0; i < total; i++)
    {
        string name = SymbolName(i, true);
        if (StringCompare(StringSubstr(name, 0, StringLen(sym)), sym, false) != 0)
            continue;
        if (StringLen(name) > StringLen(sym) + 10)
            continue;
        if (SymbolSelect(name, true))
            return name;
    }

    return "";
}

//+------------------------------------------------------------------+
//| Base symbols for quotes, symbol map, and history export           |
//+------------------------------------------------------------------+
void GetWatchBaseSymbols(string &symbols[])
{
    string list[] = {"EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "USDCHF", "NZDUSD",
                     "EURGBP", "EURJPY", "GBPJPY", "XAUUSD", "XAGUSD", "US30", "US500", "NAS100"};
    int n = ArraySize(list);
    ArrayResize(symbols, n);
    for (int k = 0; k < n; k++)
        symbols[k] = list[k];
}

//+------------------------------------------------------------------+
//| Write base->broker symbol map for House Victoria tools            |
//+------------------------------------------------------------------+
void ExportSymbolMap(bool force)
{
    static uint lastExport = 0;
    if (!force && lastExport != 0 && GetTickCount() - lastExport < 60000)
        return;
    lastExport = GetTickCount();

    string symbols[];
    GetWatchBaseSymbols(symbols);
    string json = "{";
    bool first = true;
    int i;
    for (i = 0; i < ArraySize(symbols); i++)
    {
        string broker = ResolveBrokerSymbol(symbols[i]);
        if (broker == "")
            continue;
        if (!first)
            json += ",";
        json += "\"" + symbols[i] + "\":\"" + broker + "\"";
        first = false;
    }
    json += "}";

    string mapFile = CommandFolder + "/SymbolMap.json";
    int mapHandle = FileOpen(mapFile, FILE_WRITE | FILE_TXT);
    if (mapHandle != INVALID_HANDLE)
    {
        FileWriteString(mapHandle, json);
        FileClose(mapHandle);
    }

    // Full broker symbol list (Market Watch) for discovery
    string listJson = "[";
    bool listFirst = true;
    int total = SymbolsTotal(true);
    for (i = 0; i < total; i++)
    {
        string name = SymbolName(i, true);
        if (StringLen(name) < 3)
            continue;
        if (!listFirst)
            listJson += ",";
        listJson += "\"" + name + "\"";
        listFirst = false;
    }
    listJson += "]";

    string listFile = CommandFolder + "/SymbolsAvailable.json";
    int listHandle = FileOpen(listFile, FILE_WRITE | FILE_TXT);
    if (listHandle != INVALID_HANDLE)
    {
        FileWriteString(listHandle, listJson);
        FileClose(listHandle);
    }
}

//+------------------------------------------------------------------+
//| Update market data for watch-list symbols                        |
//+------------------------------------------------------------------+
//+------------------------------------------------------------------+
//| Load watchlist from Watchlist.json (written by House Victoria)    |
//+------------------------------------------------------------------+
void LoadWatchlistSymbols(string &symbols[])
{
    string watchFile = CommandFolder + "/Watchlist.json";
    int handle = FileOpen(watchFile, FILE_READ | FILE_TXT);
    if (handle == INVALID_HANDLE)
    {
        GetWatchBaseSymbols(symbols);
        return;
    }

    string json = "";
    while (!FileIsEnding(handle))
        json += FileReadString(handle);
    FileClose(handle);

    int count = 0;
    int pos = 0;
    while (pos < StringLen(json))
    {
        int q1 = StringFind(json, "\"", pos);
        if (q1 < 0)
            break;
        int q2 = StringFind(json, "\"", q1 + 1);
        if (q2 < 0)
            break;
        string sym = StringSubstr(json, q1 + 1, q2 - q1 - 1);
        StringTrimLeft(sym);
        StringTrimRight(sym);
        if (StringLen(sym) >= 3)
        {
            ArrayResize(symbols, count + 1);
            symbols[count] = sym;
            count++;
        }
        pos = q2 + 1;
    }

    if (count == 0)
        GetWatchBaseSymbols(symbols);
}

//+------------------------------------------------------------------+
//| Update market data for watch-list symbols                        |
//+------------------------------------------------------------------+
void UpdateMarketData()
{
    ExportSymbolMap(false);

    string symbols[];
    LoadWatchlistSymbols(symbols);
    
    for (int i = 0; i < ArraySize(symbols); i++)
    {
        string brokerSymbol = ResolveBrokerSymbol(symbols[i]);
        if (brokerSymbol == "")
            continue;

        double bid = SymbolInfoDouble(brokerSymbol, SYMBOL_BID);
        double ask = SymbolInfoDouble(brokerSymbol, SYMBOL_ASK);
        double spread = ask - bid;
        
        string data = DoubleToString(bid, (int)MarketInfo(brokerSymbol, MODE_DIGITS)) + "," + 
                      DoubleToString(ask, (int)MarketInfo(brokerSymbol, MODE_DIGITS)) + "," + 
                      DoubleToString(spread, (int)MarketInfo(brokerSymbol, MODE_DIGITS));
        
        // File name uses base symbol so tools can request "EURUSD"
        string fileName = CommandFolder + "/MarketData_" + symbols[i] + ".txt";
        int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
        if (fileHandle != INVALID_HANDLE)
        {
            FileWriteString(fileHandle, data);
            FileClose(fileHandle);
        }
    }
}

//+------------------------------------------------------------------+
//| Collect file names matching a pattern (does NOT mutate folder)    |
//| Returns count. Collecting first avoids skipping files when we     |
//| delete during enumeration.                                        |
//+------------------------------------------------------------------+
int CollectCommandFiles(string pattern, string &names[])
{
    ArrayResize(names, 0);
    string fileName = "";
    int handle = FileFindFirst(pattern, fileName, 0);
    if (handle == INVALID_HANDLE)
        return 0;

    int count = 0;
    do
    {
        ArrayResize(names, count + 1);
        names[count] = fileName;
        count++;
    }
    while (FileFindNext(handle, fileName));

    FileFindClose(handle);
    return count;
}

//+------------------------------------------------------------------+
//| Process commands from House Victoria                            |
//+------------------------------------------------------------------+
void ProcessCommands()
{
    string folderPath = CommandFolder + "/";
    string names[];
    int i;

    // Trade commands
    int n = CollectCommandFiles(folderPath + "Trade_*.json", names);
    for (i = 0; i < n; i++)
    {
        string tradePath = folderPath + names[i];
        ProcessTradeCommand(tradePath);
        FileDelete(tradePath);
    }

    // Close-position commands (persona / MCP mt4_close_position)
    n = CollectCommandFiles(folderPath + "Close_*.json", names);
    for (i = 0; i < n; i++)
    {
        string closePath = folderPath + names[i];
        ProcessCloseCommand(closePath);
        FileDelete(closePath);
    }

    // Historical export commands (House Victoria / MCP mt4_export_history)
    n = CollectCommandFiles(folderPath + "History_*.json", names);
    for (i = 0; i < n; i++)
    {
        string historyPath = folderPath + names[i];
        ProcessHistoryCommand(historyPath);
        FileDelete(historyPath);
    }
}

//+------------------------------------------------------------------+
//| Map timeframe minutes (60=H1) to MT4 period constant             |
//+------------------------------------------------------------------+
int PeriodFromMinutes(int minutes)
{
    if (minutes <= 1) return PERIOD_M1;
    if (minutes <= 5) return PERIOD_M5;
    if (minutes <= 15) return PERIOD_M15;
    if (minutes <= 30) return PERIOD_M30;
    if (minutes <= 60) return PERIOD_H1;
    if (minutes <= 240) return PERIOD_H4;
    if (minutes <= 1440) return PERIOD_D1;
    if (minutes <= 10080) return PERIOD_W1;
    return PERIOD_MN1;
}

//+------------------------------------------------------------------+
//| Parse timeframe from JSON (H1, M15, or minutes as number)        |
//+------------------------------------------------------------------+
int ParseTimeFrameMinutes(string json)
{
    string tfStr = SanitizeJsonString(ExtractJsonValue(json, "TimeFrame"));
    if (tfStr == "")
        tfStr = SanitizeJsonString(ExtractJsonValue(json, "timeframe"));
    if (tfStr == "")
        tfStr = SanitizeJsonString(ExtractJsonValue(json, "time_frame"));

    StringToUpper(tfStr);
    if (tfStr == "M1") return 1;
    if (tfStr == "M5") return 5;
    if (tfStr == "M15") return 15;
    if (tfStr == "M30") return 30;
    if (tfStr == "H1") return 60;
    if (tfStr == "H4") return 240;
    if (tfStr == "D1") return 1440;
    if (tfStr == "W1") return 10080;
    if (tfStr == "MN1") return 43200;

    int minutes = (int)StringToInteger(tfStr);
    if (minutes > 0)
        return minutes;

    return 60;
}

//+------------------------------------------------------------------+
//| Export history for a symbol range to bridge CSV                    |
//+------------------------------------------------------------------+
int ExportHistoricalData(string baseSymbol, string brokerSymbol, int tfMinutes, datetime startDate, datetime endDate)
{
    int period = PeriodFromMinutes(tfMinutes);
    if (!SymbolSelect(brokerSymbol, true))
        return 0;

    int digits = (int)MarketInfo(brokerSymbol, MODE_DIGITS);
    string fileName = CommandFolder + "/" + baseSymbol + "_" + IntegerToString(tfMinutes) + ".csv";
    int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
    if (fileHandle == INVALID_HANDLE)
        return 0;

    FileWriteString(fileHandle, "Time,Open,High,Low,Close,Volume\n");

    int totalBars = iBars(brokerSymbol, period);
    int exported = 0;

    for (int shift = totalBars - 1; shift >= 0; shift--)
    {
        datetime barTime = iTime(brokerSymbol, period, shift);
        if (barTime <= 0)
            continue;
        // Iterating oldest -> newest, so bars before the window come first
        // (skip them) and bars after the window terminate the loop.
        if (barTime < startDate)
            continue;
        if (barTime > endDate)
            break;

        double open = iOpen(brokerSymbol, period, shift);
        double high = iHigh(brokerSymbol, period, shift);
        double low = iLow(brokerSymbol, period, shift);
        double close = iClose(brokerSymbol, period, shift);
        long volume = iVolume(brokerSymbol, period, shift);

        string line = TimeToString(barTime, TIME_DATE | TIME_MINUTES) + "," +
                      DoubleToString(open, digits) + "," +
                      DoubleToString(high, digits) + "," +
                      DoubleToString(low, digits) + "," +
                      DoubleToString(close, digits) + "," +
                      IntegerToString(volume);
        FileWriteString(fileHandle, line + "\n");
        exported++;
    }

    FileClose(fileHandle);
    Print("House Victoria: exported ", exported, " bars -> ", fileName);
    return exported;
}

//+------------------------------------------------------------------+
//| Process History_*.json export command                            |
//+------------------------------------------------------------------+
void ProcessHistoryCommand(string filePath)
{
    int fileHandle = FileOpen(filePath, FILE_READ | FILE_TXT);
    if (fileHandle == INVALID_HANDLE)
        return;

    string json = "";
    while (!FileIsEnding(fileHandle))
        json += FileReadString(fileHandle);
    FileClose(fileHandle);

    string baseSymbol = SanitizeJsonString(ExtractJsonValue(json, "Symbol"));
    if (baseSymbol == "")
        baseSymbol = SanitizeJsonString(ExtractJsonValue(json, "symbol"));
    StringToUpper(baseSymbol);

    if (StringLen(baseSymbol) < 3)
    {
        WriteHistoryResponse(filePath, false, baseSymbol, 0, "", "Invalid history command: Symbol is required");
        return;
    }

    int tfMinutes = ParseTimeFrameMinutes(json);
    string startStr = SanitizeJsonString(ExtractJsonValue(json, "StartDate"));
    if (startStr == "")
        startStr = SanitizeJsonString(ExtractJsonValue(json, "start_date"));
    string endStr = SanitizeJsonString(ExtractJsonValue(json, "EndDate"));
    if (endStr == "")
        endStr = SanitizeJsonString(ExtractJsonValue(json, "end_date"));

    datetime startDate = (startStr != "") ? StringToTime(startStr) : (TimeCurrent() - 365 * 24 * 60 * 60);
    datetime endDate = (endStr != "") ? StringToTime(endStr) : TimeCurrent();
    if (startDate <= 0)
        startDate = TimeCurrent() - 365 * 24 * 60 * 60;
    if (endDate <= 0)
        endDate = TimeCurrent();

    string brokerSymbol = LookupSymbolMap(baseSymbol);
    if (brokerSymbol == "")
        brokerSymbol = ResolveBrokerSymbol(baseSymbol);
    if (brokerSymbol == "")
    {
        WriteHistoryResponse(filePath, false, baseSymbol, 0, "", "Cannot resolve symbol " + baseSymbol + " in Market Watch");
        return;
    }

    int barsExported = ExportHistoricalData(baseSymbol, brokerSymbol, tfMinutes, startDate, endDate);
    string csvFile = baseSymbol + "_" + IntegerToString(tfMinutes) + ".csv";

    if (barsExported > 0)
    {
        WriteHistoryResponse(filePath, true, baseSymbol, barsExported, csvFile,
            "Exported " + IntegerToString(barsExported) + " bars to " + csvFile);
    }
    else
    {
        WriteHistoryResponse(filePath, false, baseSymbol, 0, csvFile,
            "No bars exported. Download history in MT4 History Center for " + baseSymbol + " " + IntegerToString(tfMinutes));
    }
}

//+------------------------------------------------------------------+
//| Write history export response JSON                                 |
//+------------------------------------------------------------------+
void WriteHistoryResponse(string commandFile, bool success, string baseSymbol, int barsExported, string csvFile, string message)
{
    string json = "{";
    json += "\"success\":" + (success ? "true" : "false") + ",";
    json += "\"base_symbol\":\"" + EscapeJson(baseSymbol) + "\",";
    json += "\"bars_exported\":" + IntegerToString(barsExported) + ",";
    json += "\"csv_file\":\"" + EscapeJson(csvFile) + "\",";
    json += "\"message\":\"" + EscapeJson(message) + "\"";
    json += "}";
    WriteResponse(commandFile, json);
}

//+------------------------------------------------------------------+
//| Close an open position by ticket (House Victoria magic only)      |
//+------------------------------------------------------------------+
void ProcessCloseCommand(string filePath)
{
    if (!EnableTradeExecution)
    {
        WriteResponse(filePath, "Trade execution disabled");
        return;
    }
    
    int fileHandle = FileOpen(filePath, FILE_READ | FILE_TXT);
    if (fileHandle == INVALID_HANDLE)
        return;
    
    string json = "";
    while (!FileIsEnding(fileHandle))
        json += FileReadString(fileHandle);
    FileClose(fileHandle);

    string acctReason = CheckAccountGuard(json);
    if (acctReason != "")
    {
        WriteTradeResponse(filePath, false, 0, "", "", acctReason);
        Print("House Victoria: ", acctReason);
        return;
    }
    
    string ticketStr = SanitizeJsonString(ExtractJsonValue(json, "Ticket"));
    if (ticketStr == "")
    {
        WriteTradeResponse(filePath, false, 0, "", "", "Invalid close command: Ticket is required");
        return;
    }
    
    int ticket = (int)StringToInteger(ticketStr);
    if (ticket <= 0)
    {
        WriteTradeResponse(filePath, false, 0, "", "", "Invalid close command: Ticket must be positive");
        return;
    }
    
    if (!IsExpertEnabled())
    {
        WriteTradeResponse(filePath, false, 0, "", "",
            "Close blocked: Expert Advisors disabled on this chart (check AutoTrading button and EA Common tab).");
        return;
    }

    if (!IsTradeAllowed())
    {
        WriteTradeResponse(filePath, false, 0, "", "",
            "Close blocked: IsTradeAllowed() is false. Enable AutoTrading and Allow live trading on this chart.");
        return;
    }

    if (IsTradeContextBusy())
    {
        WriteTradeResponse(filePath, false, ticket, "", "",
            "Close blocked: MT4 trade context busy. Retry in a few seconds.");
        return;
    }
    
    if (!OrderSelect(ticket, SELECT_BY_TICKET, MODE_TRADES))
    {
        WriteTradeResponse(filePath, false, ticket, "", "",
            "Position not found for ticket " + IntegerToString(ticket));
        return;
    }
    
    if (OrderMagicNumber() != MagicNumber)
    {
        WriteTradeResponse(filePath, false, ticket, OrderSymbol(), OrderSymbol(),
            "Position ticket " + IntegerToString(ticket) + " is not a House Victoria trade (wrong magic number).");
        return;
    }
    
    string brokerSymbol = OrderSymbol();
    double lots = OrderLots();
    int orderType = OrderType();
    double price = (orderType == OP_BUY)
        ? MarketInfo(brokerSymbol, MODE_BID)
        : MarketInfo(brokerSymbol, MODE_ASK);
    
    bool closed = OrderClose(ticket, lots, price, 3, clrYellow);
    if (closed)
    {
        UpdateOpenPositions(true);
        WriteTradeResponse(filePath, true, ticket, brokerSymbol, brokerSymbol,
            "Position closed successfully. Ticket: " + IntegerToString(ticket));
        Print("House Victoria: Closed position ticket ", ticket, " ", brokerSymbol);
    }
    else
    {
        int error = GetLastError();
        WriteTradeResponse(filePath, false, ticket, brokerSymbol, brokerSymbol, DescribeTradeError(error));
        Print("House Victoria: Close failed ticket ", ticket, " - ", DescribeTradeError(error));
    }
}

//+------------------------------------------------------------------+
//| Process a trade command file                                     |
//+------------------------------------------------------------------+
void ProcessTradeCommand(string filePath)
{
    if (!EnableTradeExecution)
    {
        WriteResponse(filePath, "Trade execution disabled");
        return;
    }
    
    int fileHandle = FileOpen(filePath, FILE_READ | FILE_TXT);
    if (fileHandle == INVALID_HANDLE)
        return;
    
    string json = "";
    while (!FileIsEnding(fileHandle))
    {
        json += FileReadString(fileHandle);
    }
    FileClose(fileHandle);

    // Account guard: refuse if attached to the wrong login or the command was
    // routed to the wrong terminal.
    string acctReason = CheckAccountGuard(json);
    if (acctReason != "")
    {
        WriteTradeResponse(filePath, false, 0, "", "", acctReason);
        Print("House Victoria: ", acctReason);
        return;
    }
    
    // Simple JSON parsing (basic implementation)
    // In production, use a proper JSON library or parse more carefully
    string symbol = SanitizeJsonString(ExtractJsonValue(json, "Symbol"));
    string typeStr = SanitizeJsonString(ExtractJsonValue(json, "Type"));
    string volumeStr = SanitizeJsonString(ExtractJsonValue(json, "Volume"));
    string stopLossStr = SanitizeJsonString(ExtractJsonValue(json, "StopLoss"));
    string takeProfitStr = SanitizeJsonString(ExtractJsonValue(json, "TakeProfit"));
    
    if (symbol == "" || typeStr == "" || volumeStr == "")
    {
        WriteResponse(filePath, "Invalid command parameters");
        return;
    }
    
    int type = (int)StringToInteger(typeStr);
    double volume = StringToDouble(volumeStr);
    double stopLoss = (stopLossStr != "") ? StringToDouble(stopLossStr) : 0;
    double takeProfit = (takeProfitStr != "") ? StringToDouble(takeProfitStr) : 0;

    // Only market orders are supported by this bridge.
    if (type != OP_BUY && type != OP_SELL)
    {
        WriteTradeResponse(filePath, false, 0, symbol, "",
            "Rejected: unsupported order type " + IntegerToString(type) + " (only 0=BUY, 1=SELL).");
        return;
    }

    // Guardrail: mandatory stop-loss for live safety.
    if (RequireStopLoss && stopLoss <= 0)
    {
        WriteTradeResponse(filePath, false, 0, symbol, "",
            "Rejected: stop-loss is required (RequireStopLoss=true) but none was provided.");
        return;
    }

    // Guardrail: per-interval order rate limit.
    if (RecentOrderCount() >= MaxOrdersPerInterval)
    {
        WriteTradeResponse(filePath, false, 0, symbol, "",
            "Rejected: order rate limit reached (" + IntegerToString(MaxOrdersPerInterval) +
            " per " + IntegerToString(OrderIntervalSeconds) + "s). Throttling to protect the account.");
        Print("House Victoria: order rate limit hit, command refused.");
        return;
    }

    // Guardrail: cap on concurrently open House Victoria positions.
    if (CountHouseVictoriaPositions() >= MaxOpenPositions)
    {
        WriteTradeResponse(filePath, false, 0, symbol, "",
            "Rejected: max open positions reached (" + IntegerToString(MaxOpenPositions) + ").");
        return;
    }
    
    string brokerSymbol = ResolveBrokerSymbol(symbol);
    if (brokerSymbol == "")
        brokerSymbol = LookupSymbolMap(symbol);
    if (brokerSymbol == "")
    {
        WriteTradeResponse(filePath, false, 0, symbol, "",
            "Symbol not found: " + symbol + " (no broker match in Market Watch)");
        return;
    }

    // Guardrail: lot-size sanity against EA cap and broker limits.
    double minLot  = MarketInfo(brokerSymbol, MODE_MINLOT);
    double maxLot  = MarketInfo(brokerSymbol, MODE_MAXLOT);
    double lotStep = MarketInfo(brokerSymbol, MODE_LOTSTEP);
    if (volume <= 0)
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Rejected: volume must be positive.");
        return;
    }
    if (volume > MaxLotSize)
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Rejected: volume " + DoubleToString(volume, 2) + " exceeds MaxLotSize " +
            DoubleToString(MaxLotSize, 2) + ".");
        return;
    }
    if (minLot > 0 && volume < minLot)
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Rejected: volume " + DoubleToString(volume, 2) + " below broker minimum " +
            DoubleToString(minLot, 2) + " for " + brokerSymbol + ".");
        return;
    }
    if (maxLot > 0 && volume > maxLot)
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Rejected: volume " + DoubleToString(volume, 2) + " above broker maximum " +
            DoubleToString(maxLot, 2) + " for " + brokerSymbol + ".");
        return;
    }
    // Normalize to the broker's lot step to avoid invalid-volume rejections.
    if (lotStep > 0)
    {
        double steps = MathRound(volume / lotStep);
        double normalized = steps * lotStep;
        if (MathAbs(normalized - volume) > 0.0000001 && normalized >= minLot && normalized <= MaxLotSize)
            volume = normalized;
    }
    
    if (!IsExpertEnabled())
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Trade blocked: Expert Advisors disabled on this chart (check AutoTrading button and EA Common tab).");
        return;
    }

    if (!IsTradeAllowed())
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Trade blocked: IsTradeAllowed() is false. Enable AutoTrading and Allow live trading on this chart.");
        return;
    }

    if (IsTradeContextBusy())
    {
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol,
            "Trade blocked: MT4 trade context busy. Retry in a few seconds.");
        return;
    }
    
    double price = (type == OP_BUY) ? MarketInfo(brokerSymbol, MODE_ASK) : MarketInfo(brokerSymbol, MODE_BID);
    
    int ticket = OrderSend(brokerSymbol, 
                          type, 
                          volume, 
                          price, 
                          3, 
                          stopLoss, 
                          takeProfit, 
                          "HouseVictoria", 
                          MagicNumber, 
                          0, 
                          (type == OP_BUY) ? clrGreen : clrRed);
    
    if (ticket > 0)
    {
        RecordOrderSend();
        string note = brokerSymbol;
        if (brokerSymbol != symbol)
            note = symbol + "->" + brokerSymbol;
        UpdateOpenPositions(true);
        WriteTradeResponse(filePath, true, ticket, symbol, brokerSymbol,
            "Trade executed successfully. Ticket: " + IntegerToString(ticket) + " Symbol: " + note);
        Print("House Victoria: Trade executed - ", note, " ", (type == OP_BUY ? "BUY" : "SELL"), " ", volume, " Ticket: ", ticket);
    }
    else
    {
        int error = GetLastError();
        string errorMsg = DescribeTradeError(error);
        WriteTradeResponse(filePath, false, 0, symbol, brokerSymbol, errorMsg);
        Print("House Victoria: Trade execution failed - ", errorMsg);
    }
}

//+------------------------------------------------------------------+
//| Human-readable MT4 trade error                                    |
//+------------------------------------------------------------------+
string DescribeTradeError(int errorCode)
{
    if (errorCode == 4112)
        return "Error 4112: Broker/server disabled Expert Advisor trading on this account. "
             + "AutoTrading can be green locally and still fail — contact FOREX.com to enable EA/automated trading on account "
             + IntegerToString(AccountNumber()) + ".";
    if (errorCode == 4109)
        return "Error 4109: Trading not allowed (terminal or account restriction).";
    if (errorCode == 133)
        return "Error 133: Trading disabled in terminal — enable AutoTrading.";
    if (errorCode == 134)
        return "Error 134: Not enough money for this volume.";
    if (errorCode == 136)
        return "Error 136: Off quotes / no price — market may be closed.";
    if (errorCode == 146)
        return "Error 146: Trade context busy — retry shortly.";
    return "Trade execution failed. Error: " + IntegerToString(errorCode);
}

//+------------------------------------------------------------------+
//| Look up broker symbol from SymbolMap.json (written by this EA)    |
//+------------------------------------------------------------------+
string LookupSymbolMap(string baseSymbol)
{
    string mapFile = CommandFolder + "/SymbolMap.json";
    int handle = FileOpen(mapFile, FILE_READ | FILE_TXT);
    if (handle == INVALID_HANDLE)
        return "";

    string json = "";
    while (!FileIsEnding(handle))
        json += FileReadString(handle);
    FileClose(handle);

    string mapped = SanitizeJsonString(ExtractJsonValue(json, baseSymbol));
    if (mapped == "")
        return "";

    if (SymbolSelect(mapped, true))
        return mapped;

    return "";
}

//+------------------------------------------------------------------+
//| Escape a string for safe embedding inside JSON double quotes       |
//+------------------------------------------------------------------+
string EscapeJson(string value)
{
    string outVal = "";
    int len = StringLen(value);
    for (int i = 0; i < len; i++)
    {
        ushort ch = StringGetCharacter(value, i);
        if (ch == '\\')
            outVal += "\\\\";
        else if (ch == '\"')
            outVal += "\\\"";
        else if (ch == '\n')
            outVal += "\\n";
        else if (ch == '\r')
            outVal += "\\r";
        else if (ch == '\t')
            outVal += "\\t";
        else if (ch < 0x20)
            outVal += " ";
        else
            outVal += ShortToString(ch);
    }
    return outVal;
}

//+------------------------------------------------------------------+
//| Strip whitespace and stray JSON quotes from parsed values         |
//+------------------------------------------------------------------+
string SanitizeJsonString(string value)
{
    string outVal = value;
    StringTrimLeft(outVal);
    StringTrimRight(outVal);

    while (StringLen(outVal) > 0 && StringGetCharacter(outVal, 0) == '\"')
        outVal = StringSubstr(outVal, 1);

    while (StringLen(outVal) > 0 && StringGetCharacter(outVal, StringLen(outVal) - 1) == '\"')
        outVal = StringSubstr(outVal, 0, StringLen(outVal) - 1);

    return outVal;
}

//+------------------------------------------------------------------+
//| Extract value from simple JSON string                           |
//+------------------------------------------------------------------+
string ExtractJsonValue(string json, string key)
{
    string searchKey = "\"" + key + "\"";
    int keyPos = StringFind(json, searchKey);
    if (keyPos == -1)
        return "";
    
    int colonPos = StringFind(json, ":", keyPos);
    if (colonPos == -1)
        return "";
    
    int startPos = colonPos + 1;
    while (startPos < StringLen(json) && StringGetCharacter(json, startPos) == ' ')
        startPos++;

    if (startPos >= StringLen(json))
        return "";
    
    if (StringGetCharacter(json, startPos) == '\"')
    {
        int endPos = StringFind(json, "\"", startPos + 1);
        if (endPos == -1)
            return "";
        return StringSubstr(json, startPos + 1, endPos - startPos - 1);
    }
    else
    {
        int endPos = startPos;
        while (endPos < StringLen(json) && 
               StringGetCharacter(json, endPos) != ',' && 
               StringGetCharacter(json, endPos) != '}' &&
               StringGetCharacter(json, endPos) != ']')
            endPos++;
        return StringSubstr(json, startPos, endPos - startPos);
    }
}

//+------------------------------------------------------------------+
//| Write structured trade response (JSON + legacy text compatibility) |
//+------------------------------------------------------------------+
void WriteTradeResponse(string commandFile, bool success, int ticket, string baseSymbol, string brokerSymbol, string message)
{
    string json = "{";
    json += "\"success\":" + (success ? "true" : "false") + ",";
    json += "\"ticket\":" + IntegerToString(ticket) + ",";
    json += "\"base_symbol\":\"" + EscapeJson(baseSymbol) + "\",";
    json += "\"broker_symbol\":\"" + EscapeJson(brokerSymbol) + "\",";
    json += "\"message\":\"" + EscapeJson(message) + "\"";
    json += "}";
    WriteResponse(commandFile, json);
}

//+------------------------------------------------------------------+
//| Write response file                                              |
//+------------------------------------------------------------------+
void WriteResponse(string commandFile, string response)
{
    string fileName = commandFile;
    int slashPos = StringFind(commandFile, "/");
    if (slashPos >= 0)
        fileName = StringSubstr(commandFile, slashPos + 1);

    int dotPos = StringFind(fileName, ".json");
    if (dotPos >= 0)
        fileName = StringSubstr(fileName, 0, dotPos);

    string responseFile = ResponseFolder + "/Response_" + fileName + ".txt";
    
    int fileHandle = FileOpen(responseFile, FILE_WRITE | FILE_TXT);
    if (fileHandle != INVALID_HANDLE)
    {
        FileWriteString(fileHandle, response);
        FileClose(fileHandle);
    }
    else
    {
        Print("House Victoria: failed to write response ", responseFile, " error=", GetLastError());
    }
}

//+------------------------------------------------------------------+
//| Update account information                                       |
//+------------------------------------------------------------------+
void UpdateAccountInfo()
{
    static uint lastUpdate = 0;
    if (lastUpdate != 0 && GetTickCount() - lastUpdate < 10000) // Update every 10 seconds
        return;
    
    lastUpdate = GetTickCount();
    
    string fileName = CommandFolder + "/AccountInfo.json";
    int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
    if (fileHandle != INVALID_HANDLE)
    {
        double balance = AccountBalance();
        double equity = AccountEquity();
        double margin = AccountMargin();
        double freeMargin = AccountFreeMargin();
        double marginLevel = 0.0;
        
        // Calculate margin level manually (Equity / Margin * 100)
        if (margin > 0)
            marginLevel = (equity / margin) * 100.0;
        
        string json = "{";
        json += "\"AccountNumber\":" + IntegerToString(AccountNumber()) + ",";
        json += "\"AccountName\":\"" + EscapeJson(AccountName()) + "\",";
        json += "\"Balance\":" + DoubleToString(balance, 2) + ",";
        json += "\"Equity\":" + DoubleToString(equity, 2) + ",";
        json += "\"Margin\":" + DoubleToString(margin, 2) + ",";
        json += "\"FreeMargin\":" + DoubleToString(freeMargin, 2) + ",";
        json += "\"MarginLevel\":" + DoubleToString(marginLevel, 2) + ",";
        json += "\"Currency\":\"" + AccountCurrency() + "\",";
        json += "\"Leverage\":" + IntegerToString(AccountLeverage());
        json += "}";
        
        FileWriteString(fileHandle, json);
        FileClose(fileHandle);
    }
}
//+------------------------------------------------------------------+
//| Update open positions                                            |
//+------------------------------------------------------------------+
void UpdateOpenPositions(bool force = false)
{
    static uint lastUpdate = 0;
    if (!force && lastUpdate != 0 && GetTickCount() - lastUpdate < 5000) // Update every 5 seconds
        return;
    
    lastUpdate = GetTickCount();
    
    string fileName = CommandFolder + "/OpenPositions.json";
    int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
    if (fileHandle != INVALID_HANDLE)
    {
        FileWriteString(fileHandle, "[");
        
        bool first = true;
        for (int i = 0; i < OrdersTotal(); i++)
        {
            if (OrderSelect(i, SELECT_BY_POS, MODE_TRADES))
            {
                if (OrderMagicNumber() == MagicNumber)
                {
                    if (!first)
                        FileWriteString(fileHandle, ",");
                    first = false;
                    
                    int symbolDigits = (int)MarketInfo(OrderSymbol(), MODE_DIGITS);
                    double currentPrice = (OrderType() == OP_BUY) ? SymbolInfoDouble(OrderSymbol(), SYMBOL_BID) : SymbolInfoDouble(OrderSymbol(), SYMBOL_ASK);
                    // Broker-accurate P/L incl. swap and commission, correct for
                    // all instrument/contract sizes and account currencies.
                    double profit = OrderProfit() + OrderSwap() + OrderCommission();
                    
                    string json = "{";
                    json += "\"Ticket\":" + IntegerToString(OrderTicket()) + ",";
                    json += "\"Symbol\":\"" + EscapeJson(OrderSymbol()) + "\",";
                    json += "\"Type\":" + IntegerToString(OrderType()) + ",";
                    json += "\"Volume\":" + DoubleToString(OrderLots(), 2) + ",";
                    json += "\"OpenPrice\":" + DoubleToString(OrderOpenPrice(), symbolDigits) + ",";
                    json += "\"OpenTime\":\"" + TimeToString(OrderOpenTime(), TIME_DATE | TIME_SECONDS) + "\",";
                    json += "\"CurrentPrice\":" + DoubleToString(currentPrice, symbolDigits) + ",";
                    json += "\"StopLoss\":" + (OrderStopLoss() > 0 ? DoubleToString(OrderStopLoss(), symbolDigits) : "null") + ",";
                    json += "\"TakeProfit\":" + (OrderTakeProfit() > 0 ? DoubleToString(OrderTakeProfit(), symbolDigits) : "null") + ",";
                    json += "\"Profit\":" + DoubleToString(profit, 2) + ",";
                    json += "\"Comment\":\"" + EscapeJson(OrderComment()) + "\"";
                    json += "}";
                    
                    FileWriteString(fileHandle, json);
                }
            }
        }
        
        FileWriteString(fileHandle, "]");
        FileClose(fileHandle);
    }
}
