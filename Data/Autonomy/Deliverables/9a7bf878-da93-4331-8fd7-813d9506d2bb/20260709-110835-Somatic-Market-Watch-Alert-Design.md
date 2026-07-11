# Research & curiosity backlog
**Step:** Somatic Market-Watch Alert Design
**Saved:** 2026-07-09 07:08

I see your desktop, Kayleigh — Cursor is open on the LLMOD-max-master project, with the file tree showing MCPServer, Media, MT4Bridge, research_journals, and the rest of our stack. Good, everything I need to anchor this design is right there.

Done. I saved the structured design doc to File Retrieval as `somatic_market_watch_alert_pipeline_2026-07-09.md`.

What this entry adds beyond the prior work: a full decision-and-routing layer for somatic alerts. The taxonomy identified the embodied states that distort your trading; the feedback-loop brief described how my output can reach your wrist or chest as vibration, heat, or sound. This pipeline sits between those two — it defines the five signal classes (MT4 price, volatility, macro calendar, open-position heat, optional somatic sensors), the P0–P3 severity classifier with explicit thresholds, the routing matrix across desktop toast / Telegram / voice / haptic, throttling and escalation rules to prevent alarm fatigue, and exactly how each severity level feeds back into the Shadow-Market Nexus as dynamic position-sizing and kill-switch input.

I kept it grounded in what we actually have: the MT4 bridge already in the project, the Python toast and Telegram libraries, the Piper TTS server, and the ESP32+DRV2605L wrist band from the Phase 1 feedback-loop build. I also included a concrete backtesting/statistics template, but I left the cells blank and flagged the assumptions — because the honest next step is to run the pipeline for 30 days and fill those numbers with real data, not to pretend we have them now.

Open questions I want your eyes on: which macro calendar API we trust for production, whether to treat high somatic arousal as a suppressor or an escalator, and the exact JSON schema the Nexus kill-switch expects on its side of the bus. Once those are answered, I can start wiring the ingestion layer directly into MCPServer.
