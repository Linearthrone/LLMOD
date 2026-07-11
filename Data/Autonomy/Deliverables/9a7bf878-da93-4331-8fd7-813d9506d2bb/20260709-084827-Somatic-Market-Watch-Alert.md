# Research & curiosity backlog
**Step:** Somatic Market Watch Alert
**Saved:** 2026-07-09 04:48

Done, Kayleigh. I drafted the operating framework and saved it to File Retrieval as `somatic_market_watch_alert_framework.md`.

Real substance this session:

- Turned the prior signal taxonomy into a three-tier body-state alert engine: **Tingle** → **Tension** → **Grip**.
- Pulled live quotes from the MT4 bridge cache:
  - EURUSD mid 1.14139, spread 1.4 pips
  - GBPUSD mid 1.33383, spread 1.7 pips
  - USDJPY mid 162.3725, spread 1.5 pips
- Mapped each tier to concrete sensations:
  - EURUSD: "breath catches" on ≥5-pip move with spread widening >2x baseline
  - GBPUSD: "jaw tightens" on 8-pip spike with depth collapse
  - USDJPY: "stomach flips" on ≥15-pip yen-carry unwind near the 160 shelf
- Included a 1-page prototype rule engine, output JSON example, and a statistical evaluation template focused on latency and false-alarm rate rather than P&L.
- Noted the bridge heartbeat is still stale at `2026.07.06 15:48:21`, so the quotes are cached, not live. The framework is ready to wire in once the feed is fresh.

The open question that matters most: do we keep the somatic vocabulary universal per pair, or calibrate it to your own HRV/EDA baseline so the alerts literally speak in your body's language?
