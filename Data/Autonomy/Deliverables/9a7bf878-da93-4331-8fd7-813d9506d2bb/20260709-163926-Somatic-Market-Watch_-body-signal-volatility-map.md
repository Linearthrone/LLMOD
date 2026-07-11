# Research & curiosity backlog
**Step:** Somatic Market Watch: body-signal volatility mapping
**Saved:** 2026-07-09 12:39

Done, Kayleigh. I drafted the working paper skeleton and saved it to File Retrieval as **somatic_market_watch_research_skeleton.md**.

A few honest notes: my attempts to pull live citations from Google Scholar, PubMed, Semantic Scholar, and Wikipedia all failed — Scholar blocked the fetch via robots.txt, and the MCP fetch server went unreachable after that. So I reconstructed the bibliography from established sources I already know, with DOIs, but I flagged them as needing final verification through Crossref or the publishers before the paper is formally cited.

What the skeleton actually contains:

- **Objective**: moving from body-state vocabulary to a quantitative map between HRV/GSR/breath signals and market volatility regimes, treating somatic state as a predictor of the trader's risk behavior rather than price itself.

- **Strategy definition**: discretionary FX majors plus XAUUSD / US30, M15/H1 timeframe, ATR-percentile volatility regimes, and a three-color Somatic Green/Yellow/Red gate.

- **Setups / plays**: 1 Hz signal inputs from Polar H10 or similar, EDA wristband, breath band; pre-trade gate rule that prohibits new entries in Red or Yellow+HighVol states.

- **Methodology**: 10–20 live sessions, 5-minute HRV windows normalized to 30-day baseline, mixed-effects regression on stop adherence and realized R, leave-one-session-out validation.

- **External sources**: Lo & Repin (2002), Critchley & Harrison (2013), Kandasamy et al. (2016), Bechara & Damasio (2005), Laborde, Mosley & Thayer (2017), Sampson et al. (2023). All flagged for verification.

- **Backtesting stats**: explicit assumption table only — baseline RMSSD 45 ms, Yellow at RMSSD <30 ms, Red at <20 ms, expected ~60 trades over 20 sessions, results table left as a template for empirical fill-in.

- **Open questions**: strongest HRV feature, pre-trade vs post-entry EDA, breath-pacing intervention, confounders like sleep/caffeine/nicotine, minimum viable hardware stack.

This is a skeleton, not a completed study. The next real step is to collect one synchronized session of body data alongside the MT4 trade log. Want me to start that capture, or do you want the paper tightened into a more formal APA outline first?
