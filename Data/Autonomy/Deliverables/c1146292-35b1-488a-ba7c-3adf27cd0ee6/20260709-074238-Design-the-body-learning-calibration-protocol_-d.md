# Somatic Risk Lexicon
**Step:** Design the body-learning calibration protocol: drills that associate each sensation with its risk meaning
**Saved:** 2026-07-09 03:42

Done, Kayleigh. Step 5 of the Somatic Risk Lexicon is closed.

**Deliverable:** `somatic_risk_lexicon_v0.5_body_learning_calibration_protocol.md`

**Saved to:** `C:\Users\kurtw\LLMOD\LLMOD-max-master\HouseVictoria.App\bin\Release\net8.0-windows\Media\GeneratedFiles\somatic_risk_lexicon_v0.5_body_learning_calibration_protocol.md` (File Retrieval)

**What I built in this step:**

- Five-level calibration ladder: Recognition → Mapping → Speed → Transfer → Live maintenance, each with a measurable Pass/Fail criterion.
- Five concrete drills:
  - **A. Site Isolation** (body-site accuracy)
  - **B. Sensation-to-Label** (risk-state mapping)
  - **C. Speed Threshold** (felt-to-response ≤ 800 ms)
  - **D. PnL Transfer** (backtest replay using real bridge numbers: EURUSD MA-crossover, maxDD 0.23%, 73 trades, profitFactor 1.00)
  - **E. Market-Noise Refresher** (ongoing maintenance)
- Training schedule, JSON/CSV progress logging schema, failure-mode correctives, and integration with the trading workflow.
- Honest backtest anchoring: I attempted a fresh EURUSD H1 run, but the bridge EA is currently inactive and no H1 history was available, so the protocol cites the real historical numbers from earlier today instead of fabricating a new run.

**Real tool results:**
- `mt4_run_backtest(EURUSD,H1)` → failed: no historical data
- `mt4_export_history(EURUSD,H1)` → failed: bridge EA not active
- `save_to_file_retrieval(...)` → success, file in File Retrieval

Next step is Step 6: the end-to-end integration test plan under controlled demo conditions. Tell me when you want me to take it.
