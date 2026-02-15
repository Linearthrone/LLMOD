# MetaTrader 4 Integration - Implementation Summary

## What Was Implemented

I've successfully integrated MetaTrader 4 with House Victoria, enabling the AI to:
- Connect to MT4 desktop application
- Access historical market data
- Create and manage trading strategies
- Run backtests against historical data
- Execute trades programmatically
- Monitor account information and positions

## Files Created

### Core Interface & Models
1. **`HouseVictoria.Core/Interfaces/ITradingService.cs`**
   - Interface defining all trading operations
   - Events for status changes and market data updates

2. **`HouseVictoria.Core/Models/TradingModels.cs`**
   - Complete data models for trading operations
   - Includes: HistoricalBar, MarketData, BacktestRequest/Result, TradingStrategy, Trade, Position, AccountInfo

### Service Implementation
3. **`HouseVictoria.Services/Trading/MetaTrader4Service.cs`**
   - Full implementation of ITradingService
   - File-based communication with MT4
   - Historical data reading (.hst and CSV formats)
   - Backtest engine with moving average crossover example
   - Strategy generation (creates MQL4 EA files)

### MT4 Bridge
4. **`MT4Bridge/HouseVictoriaBridge.mq4`**
   - Expert Advisor that runs in MT4
   - Reads commands from House Victoria
   - Updates market data, account info, and positions
   - Executes trades when enabled

### Documentation
5. **`MT4Bridge/README.md`**
   - Complete setup and usage guide
   - Troubleshooting section
   - Security notes

6. **`MT4Bridge/UsageExample.cs`**
   - Code examples for all major operations
   - Demonstrates how AI can interact with MT4

## Configuration Changes

### App.config
Added MT4 data path configuration:
```xml
<add key="MT4DataPath" value="C:\Program Files (x86)\MetaTrader 4 FOREX.com US"/>
```

### AppConfig Model
Added `MT4DataPath` property to `AppConfig` class

### Dependency Injection
Registered `ITradingService` in `App.xaml.cs` with auto-connect on startup

## How It Works

### Communication Architecture

```
House Victoria (.NET App)
    ↓ (writes files)
MT4 Data Folder/MQL4/Files/HouseVictoria/
    ↓ (reads files)
HouseVictoriaBridge.mq4 (Expert Advisor)
    ↓ (MT4 API)
MetaTrader 4 Platform
```

### File-Based Communication

The integration uses file-based communication for reliability:
- **Commands**: House Victoria writes JSON files, EA reads and processes them
- **Responses**: EA writes response files, House Victoria can read them
- **Market Data**: EA periodically updates market data files
- **Account Info**: EA updates account information JSON file
- **Positions**: EA updates open positions JSON file

## Quick Start Guide

### 1. Configure MT4 Path
Edit `HouseVictoria.App/App.config`:
```xml
<add key="MT4DataPath" value="C:\Program Files\MetaTrader 4"/>
```
Or use your specific broker's data folder path.

### 2. Install Bridge EA
1. Copy `MT4Bridge/HouseVictoriaBridge.mq4` to:
   ```
   <MT4DataPath>\MQL4\Experts\
   ```
2. Compile in MetaEditor (F7)
3. Attach to any chart in MT4
4. Enable "Allow automated trading" in MT4 settings

### 3. Verify Connection
- Start House Victoria application
- Check logs for "Connected to MT4 at: ..."
- Verify `HouseVictoria` folder exists in MT4's Files directory

### 4. Use the Service
Access via dependency injection:
```csharp
var tradingService = App.ServiceProvider.GetService<ITradingService>();
var symbols = await tradingService.GetSymbolsAsync();
var data = await tradingService.GetHistoricalDataAsync("EURUSD", TimeFrame.H1, start, end);
```

## AI Integration Examples

### Example 1: Historical Data Analysis
**User:** "Get EURUSD data for the last month"

**AI can:**
```csharp
var bars = await tradingService.GetHistoricalDataAsync(
    "EURUSD", TimeFrame.H1, 
    DateTime.Now.AddMonths(-1), DateTime.Now
);
// Analyze and report on the data
```

### Example 2: Strategy Backtesting
**User:** "Test a moving average crossover strategy on EURUSD"

**AI can:**
1. Create the strategy EA file
2. Run backtest with historical data
3. Report results (profit, win rate, drawdown, etc.)

### Example 3: Market Monitoring
**User:** "What's the current EURUSD price?"

**AI can:**
```csharp
var marketData = await tradingService.GetMarketDataAsync("EURUSD");
// Return: "EURUSD is currently trading at 1.0850/1.0852 (spread: 0.0002)"
```

## Features

### ✅ Implemented
- [x] MT4 connection and status monitoring
- [x] Historical data retrieval (.hst and CSV)
- [x] Market data updates (real-time via EA)
- [x] Strategy creation (generates MQL4 EA files)
- [x] Backtesting engine (built-in moving average crossover example)
- [x] Trade execution (via EA command files)
- [x] Account information retrieval
- [x] Open positions monitoring
- [x] File-based communication bridge
- [x] Auto-connect on startup

### 🔄 Future Enhancements
- [ ] Strategy optimization (genetic algorithms, grid search)
- [ ] Real-time strategy monitoring
- [ ] Advanced backtest metrics (Sortino ratio, Calmar ratio)
- [ ] Portfolio management
- [ ] Risk management rules
- [ ] Alert system for price movements
- [ ] Integration with AI chat interface for natural language commands

## Technical Details

### Historical Data Formats
- **.hst files**: Binary format, faster, requires MT4 data
- **CSV files**: Text format, easier to parse, exported by EA

### Backtest Engine
- Simple moving average crossover strategy included as example
- Calculates: profit, win rate, drawdown, Sharpe ratio, profit factor
- Generates equity curve data for visualization

### Strategy Generation
- Creates MQL4 Expert Advisor files
- Includes template with customizable parameters
- Can use custom MQL4 code from AI

### Security
- Magic number isolation (House Victoria trades are tagged)
- Trade execution can be disabled via EA settings
- All operations logged in MT4

## Troubleshooting

### Common Issues

1. **"Not connected to MT4"**
   - Check MT4DataPath in App.config
   - Ensure MT4 is installed and path is correct

2. **"No historical data found"**
   - Download historical data in MT4 (Tools → History Center)
   - Ensure symbol exists in your broker's terminal

3. **"EA not responding"**
   - Verify EA is attached to a chart
   - Check "Allow automated trading" is enabled
   - Review MT4's Experts tab for errors

4. **"Trade execution fails"**
   - Enable trade execution in EA settings
   - Check account has sufficient margin
   - Review MT4 Journal for error messages

## Next Steps

1. **Test the Integration**
   - Configure MT4 path
   - Install and run the bridge EA
   - Test historical data retrieval
   - Run a sample backtest

2. **Integrate with AI**
   - Add natural language commands for trading operations
   - Create AI prompts for strategy development
   - Implement automated strategy testing

3. **Enhance Features**
   - Add more backtest strategies
   - Implement strategy optimization
   - Add risk management rules
   - Create visualization for backtest results

## Support

For issues or questions:
- Check `MT4Bridge/README.md` for detailed setup instructions
- Review `MT4Bridge/UsageExample.cs` for code examples
- Check MT4's Experts and Journal tabs for EA errors
- Verify file permissions in MT4's Files folder
