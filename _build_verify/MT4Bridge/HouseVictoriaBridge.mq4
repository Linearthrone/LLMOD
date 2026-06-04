//+------------------------------------------------------------------+
//|                                          HouseVictoriaBridge.mq4 |
//|                        House Victoria MT4 Communication Bridge   |
//|                                                                  |
//+------------------------------------------------------------------+
#property copyright "House Victoria"
#property link      ""
#property version   "1.00"
#property strict

//--- Input parameters
input int UpdateIntervalSeconds = 5;  // Market data update interval
input bool EnableTradeExecution = true;  // Allow trade execution from House Victoria
input int MagicNumber = 123456;  // Magic number for House Victoria trades

//--- Global variables
string CommandFolder = "HouseVictoria";
string ResponseFolder = "HouseVictoria/Responses";
datetime LastMarketDataUpdate = 0;
datetime LastCommandCheck = 0;
int CommandCheckInterval = 1; // Check for commands every second

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
    Print("House Victoria Bridge initialized");
    Print("Command folder: ", CommandFolder);
    Print("Magic number: ", MagicNumber);
    
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
    
    return INIT_SUCCEEDED;
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    Print("House Victoria Bridge deinitialized");
}

//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
{
    // Update market data periodically
    if (TimeCurrent() - LastMarketDataUpdate >= UpdateIntervalSeconds)
    {
        UpdateMarketData();
        LastMarketDataUpdate = TimeCurrent();
    }
    
    // Check for commands periodically
    if (TimeCurrent() - LastCommandCheck >= CommandCheckInterval)
    {
        ProcessCommands();
        LastCommandCheck = TimeCurrent();
    }
    
    // Update account info periodically
    UpdateAccountInfo();
    
    // Update open positions
    UpdateOpenPositions();
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
    static datetime lastExport = 0;
    if (!force && TimeCurrent() - lastExport < 60)
        return;
    lastExport = TimeCurrent();

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
//| Process commands from House Victoria                            |
//+------------------------------------------------------------------+
void ProcessCommands()
{
    string folderPath = CommandFolder + "/";
    string searchPattern = "Trade_*.json";
    string fileName = "";
    
    int fileHandle = FileFindFirst(folderPath + searchPattern, fileName, 0);
    if (fileHandle == INVALID_HANDLE)
        return;
    
    do
    {
        string fullPath = folderPath + fileName;
        ProcessTradeCommand(fullPath);
        
        // Delete processed command file
        FileDelete(fullPath);
        
    } while (FileFindNext(fileHandle, fileName));
    
    FileFindClose(fileHandle);
    
    // Close-position commands (persona / MCP mt4_close_position)
    fileName = "";
    fileHandle = FileFindFirst(folderPath + "Close_*.json", fileName, 0);
    if (fileHandle != INVALID_HANDLE)
    {
        do
        {
            string fullPath = folderPath + fileName;
            ProcessCloseCommand(fullPath);
            FileDelete(fullPath);
        }
        while (FileFindNext(fileHandle, fileName));
        FileFindClose(fileHandle);
    }

    // Historical export commands (House Victoria / MCP mt4_export_history)
    fileName = "";
    fileHandle = FileFindFirst(folderPath + "History_*.json", fileName, 0);
    if (fileHandle != INVALID_HANDLE)
    {
        do
        {
            string fullPath = folderPath + fileName;
            ProcessHistoryCommand(fullPath);
            FileDelete(fullPath);
        }
        while (FileFindNext(fileHandle, fileName));
        FileFindClose(fileHandle);
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
        if (barTime > endDate)
            continue;
        if (barTime < startDate)
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
    json += "\"base_symbol\":\"" + baseSymbol + "\",";
    json += "\"bars_exported\":" + IntegerToString(barsExported) + ",";
    json += "\"csv_file\":\"" + csvFile + "\",";
    json += "\"message\":\"" + message + "\"";
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
        WriteTradeResponse(filePath, false, 0, ticket, "",
            "Close blocked: MT4 trade context busy. Retry in a few seconds.");
        return;
    }
    
    if (!OrderSelect(ticket, SELECT_BY_TICKET, MODE_TRADES))
    {
        WriteTradeResponse(filePath, false, 0, ticket, "",
            "Position not found for ticket " + IntegerToString(ticket));
        return;
    }
    
    if (OrderMagicNumber() != MagicNumber)
    {
        WriteTradeResponse(filePath, false, 0, ticket, OrderSymbol(),
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
        WriteTradeResponse(filePath, false, 0, ticket, brokerSymbol, DescribeTradeError(error));
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
    
    string brokerSymbol = ResolveBrokerSymbol(symbol);
    if (brokerSymbol == "")
        brokerSymbol = LookupSymbolMap(symbol);
    if (brokerSymbol == "")
    {
        WriteTradeResponse(filePath, false, 0, symbol, "",
            "Symbol not found: " + symbol + " (no broker match in Market Watch)");
        return;
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
    json += "\"base_symbol\":\"" + baseSymbol + "\",";
    json += "\"broker_symbol\":\"" + brokerSymbol + "\",";
    json += "\"message\":\"" + message + "\"";
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
    static datetime lastUpdate = 0;
    if (TimeCurrent() - lastUpdate < 10) // Update every 10 seconds
        return;
    
    lastUpdate = TimeCurrent();
    
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
        json += "\"AccountName\":\"" + AccountName() + "\",";
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
    static datetime lastUpdate = 0;
    if (!force && TimeCurrent() - lastUpdate < 5) // Update every 5 seconds
        return;
    
    lastUpdate = TimeCurrent();
    
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
                    
                    double currentPrice = (OrderType() == OP_BUY) ? SymbolInfoDouble(OrderSymbol(), SYMBOL_BID) : SymbolInfoDouble(OrderSymbol(), SYMBOL_ASK);
                    double profit = (OrderType() == OP_BUY) ? (currentPrice - OrderOpenPrice()) * OrderLots() * 100000 : (OrderOpenPrice() - currentPrice) * OrderLots() * 100000;
                    
                    string json = "{";
                    json += "\"Ticket\":" + IntegerToString(OrderTicket()) + ",";
                    json += "\"Symbol\":\"" + OrderSymbol() + "\",";
                    json += "\"Type\":" + IntegerToString(OrderType()) + ",";
                    json += "\"Volume\":" + DoubleToString(OrderLots(), 2) + ",";
                    json += "\"OpenPrice\":" + DoubleToString(OrderOpenPrice(), Digits) + ",";
                    json += "\"OpenTime\":\"" + TimeToString(OrderOpenTime(), TIME_DATE | TIME_SECONDS) + "\",";
                    json += "\"CurrentPrice\":" + DoubleToString(currentPrice, Digits) + ",";
                    json += "\"StopLoss\":" + (OrderStopLoss() > 0 ? DoubleToString(OrderStopLoss(), Digits) : "null") + ",";
                    json += "\"TakeProfit\":" + (OrderTakeProfit() > 0 ? DoubleToString(OrderTakeProfit(), Digits) : "null") + ",";
                    json += "\"Profit\":" + DoubleToString(profit, 2) + ",";
                    json += "\"Comment\":\"" + OrderComment() + "\"";
                    json += "}";
                    
                    FileWriteString(fileHandle, json);
                }
            }
        }
        
        FileWriteString(fileHandle, "]");
        FileClose(fileHandle);
    }
}
