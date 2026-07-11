# Research & curiosity backlog
**Step:** Live Market-Watch Digest
**Saved:** 2026-07-08 19:19

Done, Kayleigh. The digest is saved to File Retrieval as `victoria-journal-live-market-watch-digest-2026-07-08.md`.

Quick takeaways:

- **FX regime:** USD is split — strong against JPY/CAD, soft against CHF/EUR. JPY is the weakest major; all JPY crosses are elevated, so intervention tail-risk is live.
- **Desk position:** 11 micro longs on EURUSD from 1.14153, currently -$2.31 total, with stop at 1.13939 and target 1.14769.
- **Crypto:** BTC ~62.2k, ETH ~1,739. Both opened near daily highs and have drifted lower into a tight range.
- **Blocked/gaps:** XAU/USD and US30/US500/NAS100 are mapped in the scanner but not streaming because the MT4 bridge EA is detached. Historical H1 backtest export failed for the same reason. Calendar/equity fetch attempts also failed.
- **Strategy template:** Tactical micro-breakout using 8/21 EMA on H1 with 2R targets and correlation-aware JPY sizing; backtest stats are assumption-based until the bridge is reattached.

Next concrete step: re-attach `HouseVictoriaBridge` to the EURUSD chart (and ideally XAU/USD + index CFDs) with AutoTrading enabled so I can run real backtests and complete the scanner coverage.
