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
    
    // Create response folder if it doesn't exist
    string responsePath = CommandFolder + "/Responses";
    
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
//| Update market data for all symbols                               |
//+------------------------------------------------------------------+
void UpdateMarketData()
{
    string symbols[] = {"EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "USDCHF", "NZDUSD"};
    
    for (int i = 0; i < ArraySize(symbols); i++)
    {
        if (SymbolSelect(symbols[i], true))
        {
            double bid = SymbolInfoDouble(symbols[i], SYMBOL_BID);
            double ask = SymbolInfoDouble(symbols[i], SYMBOL_ASK);
            double spread = ask - bid;
            
            string data = DoubleToString(bid, Digits) + "," + 
                          DoubleToString(ask, Digits) + "," + 
                          DoubleToString(spread, Digits);
            
            string fileName = CommandFolder + "/MarketData_" + symbols[i] + ".txt";
            int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
            if (fileHandle != INVALID_HANDLE)
            {
                FileWriteString(fileHandle, data);
                FileClose(fileHandle);
            }
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
    string symbol = ExtractJsonValue(json, "Symbol");
    string typeStr = ExtractJsonValue(json, "Type");
    string volumeStr = ExtractJsonValue(json, "Volume");
    string stopLossStr = ExtractJsonValue(json, "StopLoss");
    string takeProfitStr = ExtractJsonValue(json, "TakeProfit");
    
    if (symbol == "" || typeStr == "" || volumeStr == "")
    {
        WriteResponse(filePath, "Invalid command parameters");
        return;
    }
    
    int type = (int)StringToInteger(typeStr);
    double volume = StringToDouble(volumeStr);
    double stopLoss = (stopLossStr != "") ? StringToDouble(stopLossStr) : 0;
    double takeProfit = (takeProfitStr != "") ? StringToDouble(takeProfitStr) : 0;
    
    if (!SymbolSelect(symbol, true))
    {
        WriteResponse(filePath, "Symbol not found: " + symbol);
        return;
    }
    
    double price = (type == OP_BUY) ? SymbolInfoDouble(symbol, SYMBOL_ASK) : SymbolInfoDouble(symbol, SYMBOL_BID);
    
    int ticket = OrderSend(symbol, 
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
        WriteResponse(filePath, "Trade executed successfully. Ticket: " + IntegerToString(ticket));
        Print("House Victoria: Trade executed - ", symbol, " ", (type == OP_BUY ? "BUY" : "SELL"), " ", volume, " Ticket: ", ticket);
    }
    else
    {
        int error = GetLastError();
        string errorMsg = "Trade execution failed. Error: " + IntegerToString(error);
        WriteResponse(filePath, errorMsg);
        Print("House Victoria: Trade execution failed - ", errorMsg);
    }
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
    while (startPos < StringLen(json) && (StringGetCharacter(json, startPos) == ' ' || StringGetCharacter(json, startPos) == '\"'))
        startPos++;
    
    int endPos = startPos;
    if (StringGetCharacter(json, startPos) == '\"')
    {
        endPos = StringFind(json, "\"", startPos + 1);
        if (endPos == -1)
            return "";
        return StringSubstr(json, startPos + 1, endPos - startPos - 1);
    }
    else
    {
        while (endPos < StringLen(json) && 
               StringGetCharacter(json, endPos) != ',' && 
               StringGetCharacter(json, endPos) != '}' &&
               StringGetCharacter(json, endPos) != ']')
            endPos++;
        return StringSubstr(json, startPos, endPos - startPos);
    }
}

//+------------------------------------------------------------------+
//| Write response file                                              |
//+------------------------------------------------------------------+
void WriteResponse(string commandFile, string response)
{
    string responseFile = ResponseFolder + "/Response_" + TimeToString(TimeCurrent(), TIME_DATE | TIME_SECONDS) + ".txt";
    responseFile = StringReplace(responseFile, ":", "-");
    responseFile = StringReplace(responseFile, " ", "_");
    
    int fileHandle = FileOpen(responseFile, FILE_WRITE | FILE_TXT);
    if (fileHandle != INVALID_HANDLE)
    {
        FileWriteString(fileHandle, response);
        FileClose(fileHandle);
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
void UpdateOpenPositions()
{
    static datetime lastUpdate = 0;
    if (TimeCurrent() - lastUpdate < 5) // Update every 5 seconds
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

//+------------------------------------------------------------------+
//| Export historical data to CSV                                   |
//+------------------------------------------------------------------+
void ExportHistoricalData(string symbol, int timeframe, datetime startDate, datetime endDate)
{
    string fileName = CommandFolder + "/" + symbol + "_" + IntegerToString(timeframe) + ".csv";
    int fileHandle = FileOpen(fileName, FILE_WRITE | FILE_TXT);
    if (fileHandle == INVALID_HANDLE)
        return;
    
    // Write header
    FileWriteString(fileHandle, "Time,Open,High,Low,Close,Volume\n");
    
    datetime currentTime = startDate;
    while (currentTime <= endDate)
    {
        double open[], high[], low[], close[];
        datetime time[];
        
        int copied = CopyOpen(symbol, timeframe, currentTime, 1, open);
        if (copied > 0)
        {
            CopyHigh(symbol, timeframe, currentTime, 1, high);
            CopyLow(symbol, timeframe, currentTime, 1, low);
            CopyClose(symbol, timeframe, currentTime, 1, close);
            CopyTime(symbol, timeframe, currentTime, 1, time);
            
            if (ArraySize(time) > 0)
            {
                // Get bar index to retrieve volume using iVolume()
                int barShift = iBarShift(symbol, timeframe, time[0]);
                long volume = iVolume(symbol, timeframe, barShift);
                
                string line = TimeToString(time[0], TIME_DATE | TIME_MINUTES) + "," +
                             DoubleToString(open[0], Digits) + "," +
                             DoubleToString(high[0], Digits) + "," +
                             DoubleToString(low[0], Digits) + "," +
                             DoubleToString(close[0], Digits) + "," +
                             IntegerToString(volume);
                FileWriteString(fileHandle, line + "\n");
            }
        }
        
        // Move to next bar
        currentTime = iTime(symbol, timeframe, iBarShift(symbol, timeframe, currentTime) - 1);
        if (currentTime <= 0)
            break;
    }
    
    FileClose(fileHandle);
    Print("Historical data exported: ", fileName);
}