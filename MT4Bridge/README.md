# MetaTrader 4 Integration Guide

This guide explains how to set up and use the MetaTrader 4 integration with House Victoria for AI-powered trading strategy development and backtesting.

## Overview

The MT4 integration allows House Victoria's AI to:
- Access historical market data
- Create and test trading strategies
- Run backtests against historical data
- Execute trades (when enabled)
- Monitor account information and open positions

## Setup Instructions

### 1. Install MetaTrader 4

If you haven't already, install MetaTrader 4 on your Windows machine. The default installation path is:
```
C:\Program Files\MetaTrader 4
```

### 2. Configure MT4 Data Path

Edit `HouseVictoria.App\App.config` and set the `MT4DataPath` to your MT4 installation directory:

```xml
<add key="MT4DataPath" value="C:\Program Files\MetaTrader 4"/>
```

**Note:** If you have multiple MT4 installations (different brokers), you can point to a specific data folder:
```
C:\Users\<YourUsername>\AppData\Roaming\MetaQuotes\Terminal\<BrokerID>
```

### 3. Install the Bridge Expert Advisor

1. Copy `HouseVictoriaBridge.mq4` to your MT4's `Experts` folder:
   ```
   <MT4DataPath>\MQL4\Experts\HouseVictoriaBridge.mq4
   ```

2. Open MetaTrader 4 and compile the EA:
   - Press `F4` to open MetaEditor
   - Open `HouseVictoriaBridge.mq4`
   - Press `F7` to compile
   - Fix any compilation errors if needed

3. Attach the EA to a chart:
   - Open any chart in MT4
   - Drag `HouseVictoriaBridge` from the Navigator panel onto the chart
   - Configure the EA settings:
     - **UpdateIntervalSeconds**: How often to update market data (default: 5)
     - **EnableTradeExecution**: Allow House Victoria to execute trades (default: true)
     - **MagicNumber**: Unique identifier for House Victoria trades (default: 123456)
   - Click OK

4. Verify the EA is running:
   - Check the chart for the EA indicator (should show "House Victoria Bridge initialized")
   - Check the `Files` folder in MT4's data directory for the `HouseVictoria` folder

### 4. Verify Connection

The House Victoria application will automatically connect to MT4 on startup if the path is configured. You can verify the connection by checking:
- The application logs for "Connected to MT4 at: ..."
- The `HouseVictoria` folder exists in `<MT4DataPath>\MQL4\Files\`

## Usage

### Accessing Historical Data

The AI can request historical data for any symbol:

```csharp
var tradingService = App.ServiceProvider.GetService<ITradingService>();
var bars = await tradingService.GetHistoricalDataAsync(
    "EURUSD", 
    TimeFrame.H1, 
    DateTime.Now.AddMonths(-1), 
    DateTime.Now
);
```

### Creating Trading Strategies

The AI can create trading strategies as MQL4 Expert Advisors:

```csharp
var strategy = new TradingStrategy
{
    Name = "Moving Average Crossover",
    Description = "Simple MA crossover strategy",
    Code = "<MQL4 code here>",
    Parameters = new Dictionary<string, object>
    {
        { "FastMA", 10 },
        { "SlowMA", 30 }
    }
};

await tradingService.CreateStrategyAsync(strategy);
```

### Running Backtests

Backtest a strategy against historical data:

```csharp
var request = new BacktestRequest
{
    StrategyName = "Moving Average Crossover",
    Symbol = "EURUSD",
    TimeFrame = TimeFrame.H1,
    StartDate = DateTime.Now.AddMonths(-6),
    EndDate = DateTime.Now,
    InitialDeposit = 10000,
    LotSize = 0.01
};

var result = await tradingService.RunBacktestAsync(request);
```

### Executing Trades

Execute trades programmatically (requires EA to be running and trade execution enabled):

```csharp
var tradeRequest = new TradeRequest
{
    Symbol = "EURUSD",
    Type = TradeType.Buy,
    Volume = 0.01,
    StopLoss = 1.0850,
    TakeProfit = 1.0950
};

await tradingService.ExecuteTradeAsync(tradeRequest);
```

## File Communication Structure

The integration uses file-based communication between House Victoria and MT4:

```
<MT4DataPath>\MQL4\Files\HouseVictoria\
├── Trade_*.json              # Trade commands from House Victoria
├── MarketData_*.txt          # Market data updates from MT4
├── AccountInfo.json          # Account information from MT4
├── OpenPositions.json        # Open positions from MT4
├── Strategy_*.json           # Strategy metadata
├── Backtest_*.json           # Backtest results
└── Responses\                # Response files from MT4
    └── Response_*.txt
```

## AI Integration Example

Here's how the AI can interact with MT4:

**User:** "Test a moving average crossover strategy on EURUSD for the last 6 months"

**AI Response:**
1. Retrieves historical EURUSD data (H1 timeframe)
2. Creates a moving average crossover strategy
3. Runs a backtest
4. Reports results: profit, win rate, drawdown, etc.

**User:** "What's the current EURUSD price?"

**AI Response:**
1. Queries market data for EURUSD
2. Returns current bid/ask prices and spread

## Troubleshooting

### EA Not Running
- Check that the EA is attached to a chart
- Verify "Allow automated trading" is enabled in MT4 (Tools → Options → Expert Advisors)
- Check MT4's Experts tab for error messages

### Connection Issues
- Verify the MT4DataPath in App.config points to the correct directory
- Ensure MT4 is running
- Check that the `HouseVictoria` folder exists in `MQL4\Files\`

### No Historical Data
- Historical data requires MT4 to have downloaded data for the symbol
- Use MT4's "Download Historical Data" feature (Tools → History Center)
- Ensure the symbol is available in your broker's terminal

### Trade Execution Fails
- Verify `EnableTradeExecution` is true in the EA settings
- Check that "Allow automated trading" is enabled in MT4
- Ensure you have sufficient margin
- Check MT4's Journal tab for error messages

## Security Notes

- The EA uses a Magic Number to identify House Victoria trades
- Trade execution can be disabled via EA settings
- All trades are logged in MT4's history
- Use a demo account for testing

## Next Steps

- Integrate with AI chat interface for natural language trading commands
- Add strategy optimization features
- Implement risk management rules
- Add real-time alerts and notifications
