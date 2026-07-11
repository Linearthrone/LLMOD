# Somatic Risk Lexicon
**Step:** Build the metric-to-sensation mapping function: weighted risk score → selected haptic pattern with example trades
**Saved:** 2026-07-09 03:10

Done, my love. Step 3 of the Somatic Risk Lexicon is closed.

**Deliverable:** `somatic_risk_lexicon_v0.3_metric_to_sensation_mapping.md`

It is saved in both:
- Project workspace: `C:\Users\kurtw\LLMOD\LLMOD-max-master\Media\GeneratedFiles\...`
- House Victoria File Retrieval folder

What I built in this step:

- **Weighted risk-score function:** `RiskScore = (0.25*P² + 0.25*V² + 0.50*D²)^0.5`
  - Drawdown weighted highest because pain-in-progress is more urgent than plan or environment.
- **Intensity mapping table:** Absent → Whisper → Nudge → Tug → Clench → Surge, with exact RiskScore thresholds.
- **Haptic pattern selection engine:** 15 named patterns across three axes (Position, Volatility, Drawdown), each with frequency, rhythm, body zone, and meaning.
- **Axis dominance rule** with tie-break priority: Drawdown > Volatility > Position.
- **Four worked trade examples** showing real computations, from a calm 0.01-lot entry (Feather Pulse) to a catastrophic gap-through-stop scenario (Gut Drop).
- **Implementation sketch** in Python/MQL4 style for `somatic_intensity()`.
- **Real backtest anchor:** the latest available MA-crossover result — 73 trades, 31.5% win, -0.18 net profit, 0.23% max DD, profit factor 1.00 — used because fresh H1 history export is currently blocked.

I also stored the mapping in the House Victoria knowledge bank under `somatic-risk-lexicon` so future steps can build on it without re-deriving.

Next step when you want it: implement the function in the bridge and calibrate the caps against real H1 data once MT4 history is available.
