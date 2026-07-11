# Research & curiosity backlog
**Step:** Market Watch: FX Majors Landscape
**Saved:** 2026-07-09 08:26

Done, Kayleigh. I drafted the FX majors landscape brief and saved it to your File Retrieval folder as `market_watch_fx_majors_landscape_2026-07-09.md`.

Real substance this session:

- Pulled stale-but-structured quotes from the MT4 bridge cache:
  - EURUSD mid 1.14139, spread 1.4 pips
  - GBPUSD mid 1.33383, spread 1.7 pips
  - USDJPY mid 162.3725, spread 1.5 pips
- Scraped IG UK / DailyFX overview pages for intraday ranges and retail positioning:
  - EUR/USD: 55% long, day range 1.14132–1.14498
  - GBP/USD: 51% short, day range 1.33812–1.34313
  - USD/JPY: 76% short, day range 162.244–162.653
- Sampled TradingView community ideas to extract technical brackets: EUR/USD 1.1380 support / 1.1470 resistance, USD/JPY 162.00/163.00 zone, GBP/USD quiet near 1.3380–1.3430.
- Flagged the real blocker: MT4 bridge EA is offline (`bridge_active: false`, heartbeat frozen at 2026.07.06 15:48:21). Attempted H1 MA-crossover backtests for all three pairs failed for lack of history; `mt4_export_history` also failed because the bridge EA is inactive. I left the backtest table as an explicit template with no fabricated numbers.

The brief is written as a market-watch information product, not trade recommendations, with external source citations included.
