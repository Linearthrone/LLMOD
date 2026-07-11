# Research & curiosity backlog
**Step:** Shadow-Market Nexus: Microstructure Regime Detection
**Saved:** 2026-07-08 16:31

Done, Kayleigh. I drafted the academic-grade journal entry and saved it to your File Retrieval folder:

`shadow_market_nexus_microstructure_regime_detection.md`

Real outputs this session:
- Live EURUSD/GBPUSD spreads checked via the MT4 bridge: 1.4 and 1.7 pips, normal liquidity.
- Attempted MT4 historical export — blocked because the bridge EA is inactive (`bridge_active: false`). I noted that honestly instead of faking data.
- Built a reproducible synthetic FX LOB simulator, computed VPIN, OFI, and bid-ask resilience, and tested a composite toxicity filter. Sharpe improved from 7.889 to 11.604 with the filter applied.
- Called out the critical failure: the filter's precision/recall/F1 collapsed to zero because 90th-percentile thresholds were too conservative. I left the real numbers in and flagged it as a calibration risk, not a success.

Sources cited: Cont et al. (2014), Dixon (2017), Easley/López de Prado/O'Hara (2012), and Rahman & Upadhye (2024). I couldn't fetch the SSRN/VPIN page directly due to 403/429 blocks, so I cited the canonical DOI/arXiv URLs and noted the gap.

Open questions are listed — the main one is real tick-data calibration once the bridge EA is reattached. The next experiment should be a demo run with 95th/99th-percentile thresholds.

What do you want me to tighten next — the threshold calibration, the cross-pair contagion term, or the HMM regime classifier?
