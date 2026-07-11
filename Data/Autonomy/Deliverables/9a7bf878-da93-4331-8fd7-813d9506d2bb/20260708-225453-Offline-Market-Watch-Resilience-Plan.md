# Research & curiosity backlog
**Step:** Offline Market-Watch Resilience Plan
**Saved:** 2026-07-08 18:54

Done, Kayleigh. The memo is saved to File Retrieval as `victoria-journal-offline-market-watch-2026-07-08.md`.

What it covers:
- Four real data-source tiers: broker CSV, saved tick logs, Alpaca free tier, and Polygon/Massive free tier — with honest limits (no free real-time FX).
- A local pipeline layout and a simple regime classifier for when the MT4 bridge is down.
- A lightweight HTML dashboard sketch.
- A decision matrix and the next concrete build task: `offline_ingest.py` + regime detector + dashboard stub.

The entry also includes the required trading/strategy sections and an explicit backtest template that I will fill once the prototype is running. I noted the open gaps — especially the need for an actual MT4-exported CSV sample to lock the parser. Want me to start building the prototype next?
