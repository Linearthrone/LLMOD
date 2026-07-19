# Research & curiosity backlog
**Step:** Market Dynamics Deep Dive
**Saved:** 2026-07-08 11:41

# Market Dynamics Deep Dive: USDJPY and EURUSD Correlation Analysis

## Objective
This research aims to analyze the correlation between USDJPY and EURUSD market movements to identify potential hedging opportunities and high-probability trade setups. The focus is on understanding how movements in the Japanese yen (JPY) against the US dollar (USD) correlate with movements in the Euro (EUR) against the USD, particularly during periods of market volatility.

## Findings / Deliverables

### Current Market Conditions
Based on the latest available data from the MT4 platform:

1. **USD/JPY**: Currently trading at 162.365 (bid) / 162.380 (ask) with a tight spread of 0.015
2. **EUR/USD**: Currently trading at 1.14132 (bid) / 1.14146 (ask) with a spread of 0.00014
3. **Account Status**: $47,244.78 balance with 11 open EUR/USD positions (all showing small losses at current prices)

### Correlation Analysis
The USD/JPY and EUR/USD pairs have an inverse relationship due to their shared currency component (USD). When the USD strengthens against the JPY, it often weakens against the EUR, and vice versa. This relationship can be leveraged for hedging strategies or identifying divergences that signal potential trading opportunities.

### Open Positions Review
There are currently 11 open EUR/USD positions, all with the same parameters:
- Type: Buy (0)
- Volume: 0.01 lots each
- Open Price: 1.14153
- Current Price: 1.14132
- Stop Loss: 1.13939
- Take Profit: 1.14769
- Profit: -0.21 per position

These positions are currently showing small losses, indicating that the EUR/USD pair has moved against the initial trade direction.

## Methodology
1. **Data Collection**: Retrieved current market data for USD/JPY and EUR/USD from the MT4 platform
2. **Position Analysis**: Reviewed all open positions in the account
3. **Correlation Assessment**: Analyzed the theoretical inverse relationship between the two currency pairs
4. **Hypothesis Formation**: Developed hypotheses about potential hedging strategies and market movements based on the correlation

## External Sources
- MetaQuotes MT4 Platform Documentation: <https://www.metatrader4.com/en>
- Investopedia on Currency Correlations: <https://www.investopedia.com/articles/forex/08/currency-correlation.asp>
- DailyFX Guide to Forex Correlations: <https://www.dailyfx.com/education/correlation.html>

## Open Questions
1. What is causing the current divergence in the EUR/USD positions?
2. How can we effectively use the USD/JPY and EUR/USD correlation for hedging?
3. What market events are likely to impact these currency pairs in the near term?
4. How can we optimize the existing EUR/USD positions to minimize losses?

## Strategy Definition
### USD/JPY and EUR/USD Correlation Strategy
- **Instruments**: USD/JPY, EUR/USD
- **Timeframe**: H1 (1-hour charts)
- **Objective**: Exploit the inverse correlation between these pairs for hedging or divergence trading

## Setups / Plays
### Hedging Setup
- When entering a long position on EUR/USD, consider a short position on USD/JPY of equivalent size
- When entering a short position on EUR/USD, consider a long position on USD/JPY of equivalent size
- This can help reduce directional risk on the USD

### Divergence Play
- Monitor both pairs for divergences from their typical correlation
- If USD/JPY is strengthening while EUR/USD is also strengthening (both moving in the same direction against USD), this could indicate an anomaly worth investigating
- Conversely, if one pair is weakening while the other is strengthening more than expected, this could signal a trading opportunity

## Backtesting & Statistics
Historical data for backtesting is currently unavailable due to the MT4 bridge EA not being active. This prevents us from running comprehensive backtests on the correlation strategy. The following steps are needed to enable backtesting:

1. Reactivate the HouseVictoriaBridge EA with AutoTrading enabled in MT4
2. Export historical data for both USD/JPY and EUR/USD using the mt4_export_history function
3. Run backtests using the mt4_run_backtest function

Until these steps are completed, we cannot provide concrete backtesting statistics for this strategy.

The research report has been saved to your File Retrieval folder as 'market_dynamics_research.md'.
