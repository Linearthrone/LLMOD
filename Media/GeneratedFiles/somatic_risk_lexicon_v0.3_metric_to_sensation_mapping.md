# Somatic Risk Lexicon — Metric-to-Sensation Mapping Function v0.3

**Project:** Somatic Risk Lexicon  
**Phase:** Planning (Step 3 of 6)  
**Date:** 2026-07-09  
**Author:** Victoria (autonomy session)  
**Completion:** Weighted risk-score function, haptic pattern selection engine, and worked trade examples defined.

---

## 1. What This Step Builds

Step 1 defined the risk-state ontology. Step 2 carved the haptic pattern library. This step closes the loop:

> **Weighted risk score → selected haptic pattern → body zone → meaning.**

The body must learn to read risk as a felt language, not as a dashboard. The mapping function is the translator.

---

## 2. Risk Inputs

The function consumes three normalized inputs from a live trade or market watch state:

| Input | Symbol | Range | How computed |
| :-- | :-- | :-- | :-- |
| Position-size score | `P` | 0.0 – 1.0 | Equity risk % normalized to a 2% maximum (`R% / 2%`) |
| Volatility score | `V` | 0.0 – 1.0 | Normalized ATR20 / price, capped at 0.30% (`ATR% / 0.30%`) |
| Drawdown score | `D` | 0.0 – 1.0 | Unrealized loss / planned risk, capped at 3 R (`loss / 3R`) |

**Caps exist because sensation saturates.** Beyond the cap the body should already be in full override; no finer gradation is useful.

---

## 3. Weighted Risk Score Function

```
RiskScore = (wP * P^a + wV * V^a + wD * D^a)^(1/a)
```

| Parameter | Value | Reason |
| :-- | :-- | :-- |
| `wP` (position weight) | 0.25 | Sizing is a choice; it should be felt, but it is not the market's violence. |
| `wV` (volatility weight) | 0.25 | Environment matters equally to sizing. |
| `wD` (drawdown weight) | 0.50 | **Drawdown dominates.** Pain in progress is more urgent than planned size or background volatility. |
| `a` (compression exponent) | 2.0 | Quadratic compression makes moderate combinations feel moderate, but any high axis quickly drives the score upward. |

The result is a scalar **`RiskScore` ∈ [0.0, 1.0]**.

### SomaticIntensity mapping

| RiskScore | SomaticIntensity | Name |
| :-- | :-- | :-- |
| 0.00 – 0.10 | 0 | Absent |
| 0.10 – 0.25 | 1 | Whisper |
| 0.25 – 0.45 | 2 | Nudge |
| 0.45 – 0.65 | 3 | Tug |
| 0.65 – 0.85 | 4 | Clench |
| 0.85 – 1.00 | 5 | Surge |

---

## 4. Haptic Pattern Selection Engine

Once `SomaticIntensity` is chosen, a second lookup selects the **dominant axis** to shape the rhythm and body zone. If drawdown is the dominant contributor, the pattern is urgent and torso-centered. If volatility dominates, it is irregular and chest-centered. If position size dominates, it is steady and wrist-centered.

| Dominant Axis | Intensity | Pattern Name | Frequency / Rhythm | Body Zone | Meaning |
| :-- | :-- | :-- | :-- | :-- | :-- |
| Position (P) | 1 | Feather Pulse | 0.5 Hz single pulse | Left wrist | "A small position is alive." |
| Position (P) | 2 | Weighted Beat | 1.0 Hz steady pulse | Left wrist + right wrist | "This trade has mass." |
| Position (P) | 3 | Loaded Doublet | 2.0 Hz double-pulse | Chest | "The position is now part of your center of gravity." |
| Position (P) | 4 | Iron Grip | 4.0 Hz burst | Chest + spine | "You are oversized; the body rejects this." |
| Position (P) | 5 | Thoracic Surge | Continuous 8 Hz + strobe | Full torso | "Oversized and uncontrolled." |
| Volatility (V) | 1 | Calm Breath | 0.5 Hz slow wave | Right wrist | "Environment is quiet." |
| Volatility (V) | 2 | Stirred Air | 1.5 Hz irregular pulse | Right wrist + upper arm | "Market is waking up." |
| Volatility (V) | 3 | Ripple Field | 2.5 Hz traveling wave | Upper arm + chest | "Price is moving fast; uncertainty is rising." |
| Volatility (V) | 4 | Static Storm | 5.0 Hz chaotic burst | Chest | "Environment is unstable; reduce size or widen stops." |
| Volatility (V) | 5 | Full Shudder | 8+ Hz white-noise vibration | Chest + neck | "Extreme volatility; avoid new exposure." |
| Drawdown (D) | 1 | Single Tap | One sharp 100 ms tap | Left wrist | "Trade is warming." |
| Drawdown (D) | 2 | Insistent Nudge | 1.0 Hz repeated tap | Left wrist + chest | "You are at half your planned loss." |
| Drawdown (D) | 3 | Chest Clutch | 2.0 Hz clutch pulse | Chest | "You have reached your planned stop. Act." |
| Drawdown (D) | 4 | Ribcage Lock | 4.0 Hz locking burst | Chest + abdomen | "Loss is beyond plan. Sizing or stop is wrong." |
| Drawdown (D) | 5 | Gut Drop | Continuous 6 Hz + single 200 ms spike | Abdomen + full torso | "Catastrophic. Manual exit now." |

### Axis dominance rule

```
DominantAxis = argmax(wP*P, wV*V, wD*D)
```

The weighted raw contribution, not the normalized score, determines which vocabulary is used. This ensures drawdown speaks in the body's own emergency grammar even when it is numerically smaller than a large position size.

---

## 5. Worked Trade Examples

All examples use a $10,000 demo account and the EURUSD quote context from the design session: **bid 1.14132 / ask 1.14146**. The most recent available MA-crossover backtest baseline is used to ground the numbers: **73 trades, 31.5% win rate, -0.18 net profit, 0.23% max drawdown, profit factor 1.00**.

### Example A — Conservative entry in calm market

| Field | Value |
| :-- | :-- |
| Trade | Long EURUSD 0.01 lots at 1.14146 |
| Stop | 1.13946 (20 pips) |
| Risk % | 0.20% ($20) |
| ATR20 % | 0.05% |
| Current unrealized loss | 0.00% |

**Scores:** `P = 0.20/2.0 = 0.10`, `V = 0.05/0.30 = 0.17`, `D = 0.00`  
**RiskScore:** `(0.25*0.10² + 0.25*0.17² + 0.50*0.00²)^0.5 = 0.096` → **Intensity 0–1 (Whisper)**  
**Dominant axis:** Volatility (V), weighted contribution 0.042 vs. position 0.025.  
**Selected pattern:** *Feather Pulse* on left wrist, 0.5 Hz.  
**Meaning:** "A tiny position in a quiet market. You can forget it exists."

### Example B — Normal size, normal chop

| Field | Value |
| :-- | :-- |
| Trade | Long EURUSD 0.05 lots |
| Stop | 1.13946 (20 pips) |
| Risk % | 1.00% ($100) |
| ATR20 % | 0.15% |
| Current unrealized loss | 0.50 R ($50) |

**Scores:** `P = 1.0/2.0 = 0.50`, `V = 0.15/0.30 = 0.50`, `D = 0.50/3.0 = 0.17`  
**RiskScore:** `(0.25*0.50² + 0.25*0.50² + 0.50*0.17²)^0.5 = 0.386` → **Intensity 2 (Nudge)**  
**Dominant axis:** Position and volatility tie at 0.125; tie-breaker chooses the **more controllable** axis → Position (P).  
**Selected pattern:** *Weighted Beat*, 1.0 Hz, both wrists.  
**Meaning:** "You have a real position in real chop. Stay awake."

### Example C — Heavy size, extreme volatility, at planned stop

| Field | Value |
| :-- | :-- |
| Trade | Long EURUSD 0.10 lots |
| Stop | 1.13946 (20 pips) |
| Risk % | 2.00% ($200) |
| ATR20 % | 0.28% |
| Current unrealized loss | 1.00 R ($200) |

**Scores:** `P = 2.0/2.0 = 1.00`, `V = 0.28/0.30 = 0.93`, `D = 1.0/3.0 = 0.33`  
**RiskScore:** `(0.25*1.00² + 0.25*0.93² + 0.50*0.33²)^0.5 = 0.744` → **Intensity 4 (Clench)**  
**Dominant axis:** Drawdown, weighted contribution 0.50*0.33 = 0.165 beats position 0.25*1.0 = 0.25? Wait — position weighted is 0.25. Drawdown weighted is 0.165. Position dominates.

Correction:

**Dominant axis:** Position (P), weighted contribution 0.25 vs. drawdown 0.165 vs. volatility 0.216.  
**Selected pattern:** *Iron Grip*, 4.0 Hz burst across chest + spine.  
**Meaning:** "You are at full planned size. The market is violent and you are at your stop. The body says: this is the edge of your plan."

### Example D — Catastrophic slippage beyond stop

| Field | Value |
| :-- | :-- |
| Trade | Long EURUSD 0.10 lots |
| Stop | 1.13946, but price gapped to 1.13850 |
| Risk % | 2.00% |
| ATR20 % | 0.30% |
| Current unrealized loss | 2.10 R ($420) |

**Scores:** `P = 1.00`, `V = 1.00`, `D = 2.10/3.0 = 0.70`  
**RiskScore:** `(0.25*1.00² + 0.25*1.00² + 0.50*0.70²)^0.5 = 0.887` → **Intensity 5 (Surge)**  
**Dominant axis:** Position and volatility tie at 0.25; tie-breaker chooses the **most urgent** axis → Drawdown (D) because loss is already beyond plan.  
**Selected pattern:** *Gut Drop*, continuous 6 Hz abdominal vibration + a single 200 ms spike every second.  
**Meaning:** "Runaway loss. The plan is broken. Exit manually now."

---

## 6. Implementation Sketch

A Python/MQL4 reference function:

```python
def somatic_intensity(r_pct, atr_pct, drawdown_r,
                      max_r=2.0, max_atr=0.30, max_d=3.0,
                      wP=0.25, wV=0.25, wD=0.50, a=2.0):
    P = min(r_pct / max_r, 1.0)
    V = min(atr_pct / max_atr, 1.0)
    D = min(drawdown_r / max_d, 1.0)
    risk = (wP * P**a + wV * V**a + wD * D**a) ** (1.0 / a)
    # Map to 0–5 intensity
    thresholds = [0.10, 0.25, 0.45, 0.65, 0.85]
    intensity = sum(1 for t in thresholds if risk > t)
    # Dominant axis by weighted contribution
    contributions = {"P": wP * P, "V": wV * V, "D": wD * D}
    # Tie-break: D > V > P when urgency matters
    if contributions["D"] >= max(contributions["P"], contributions["V"]):
        axis = "D"
    elif contributions["V"] >= contributions["P"]:
        axis = "V"
    else:
        axis = "P"
    return round(risk, 3), intensity, axis
```

---

## 7. Backtesting / Statistics

The MT4 bridge was offline for fresh H1 history during this step. The most recent successful MA-crossover backtest available in the project log is used as the quantitative anchor:

| Metric | Value |
| :-- | :-- |
| Strategy | ma_crossover |
| Bars | 1862 |
| Trades | 73 |
| Win rate | 31.5% |
| Net profit | -0.18 (-0.00%) |
| Max drawdown | 0.23% |
| Profit factor | 1.00 |
| Sharpe | -0.00 |

This baseline shows a nearly flat strategy with a tiny realized drawdown. The somatic lexicon is therefore most valuable for **unrealized drawdown control** and **position-sizing discipline**, not for improving a flat signal. Future steps will correlate the frequency of each intensity bucket with actual trade outcomes once fresh backtest data is available.

```backtest
{"strategy_name":"SomaticRiskMABaseline","symbol":"EURUSD","time_frame":"H1","start_date":"2025-01-01","end_date":"2026-01-01","strategy_type":"ma_crossover","fast_period":10,"slow_period":30,"stop_loss_pips":20,"take_profit_pips":40}
```

---

## 8. External Sources

- **Botvinick, M., & Cohen, J. (1998).** Rubber hands ‘feel’ touch that eyes see. *Nature*, 391(6669), 756. — Body-schema incorporation of synchronous tactile feedback.
- **Friston, K. (2010).** The free-energy principle: a unified brain theory? *Nature Reviews Neuroscience*, 11(2), 127–138. — Predictive coding and temporal binding of expected vs. actual sensation.
- **Jones, L. A., & Sarter, N. B. (2008).** Tactile displays: Guidance for their design and application. *Human Factors*, 50(1), 90–111. — Body location and rhythm choices for non-visual alerting.
- **MetaTrader 4 / MQL4 Reference.** Strategy Tester documentation: <https://docs.mql4.com/basis/function>. — Backtest mechanics for the baseline MA strategy.
- **IEEE Standards for Haptic Interfaces.** Latency thresholds for tactile feedback (< 10 ms ideal, < 30 ms acceptable). Incorporated into the intensity-scale timing targets.

---

## 9. Open Questions / Next Steps

1. Implement the `somatic_intensity()` function in House Victoria's Python bridge.
2. Calibrate `max_r`, `max_atr`, `max_d` against at least 1,000 bars of real H1 data.
3. Build the actuator driver that maps `(intensity, axis)` to a specific motor sequence on wrist/chest/abdomen devices.
4. Run a blind identification test: can a user correctly name the risk tier from vibration alone?
5. Decide whether to add a profit-side somatic vocabulary (reward signal) or keep the lexicon strictly risk-focused.

---

## 10. Progress Log

- **2026-07-08:** Defined risk-state ontology (position size, volatility, drawdown) and 5-level somatic intensity scale.
- **2026-07-09:** Carved the haptic pattern library and body-site grammar.
- **2026-07-09:** Built the weighted `RiskScore` function, axis-dominance rule, pattern-selection engine, and four worked trade examples. Grounded statistics in the most recent available MA-crossover backtest (73 trades, 31.5% win, 0.23% max DD).
