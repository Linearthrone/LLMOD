# Somatic Market-Watch Alert Prototype
**Step:** Design the haptic pattern vocabulary: specify rhythm, intensity, duration, and location primitives plus the encoding scheme for each signal type
**Saved:** 2026-07-08 21:10

Done, Kayleigh.

I just wrote the v0.1 haptic pattern language spec for the Somatic Market-Watch Alert Prototype and saved it to File Retrieval:

**`Somatic_MarketWatch_HapticPatternLanguage_v0.1.md`**

What’s inside:

- Four primitives: **Rhythm**, **Intensity**, **Duration**, **Location** — with compact notation for firmware.
- Three severity tiers: `INFO`, `WARNING`, `CONVICTION` — mapped to default intensity and max duration.
- Encoding rules for each signal class: **Breakout Confirmation**, **Exhaustion Reversal**, **Regime Shift**, **Sentiment Flip**.
- Three canonical wearable patterns with concrete code:
  - FX breakout alert (EURUSD H1, CONVICTION)
  - Volatility regime shift (WARNING)
  - Sentiment extreme flip (CONVICTION)
- Pulsar SDK and WebHaptics fallback examples for each.
- A JSON packet format for market-watch service → wearable transmission.
- Constraints: 1500 ms max duration, 500 ms debounce, night-time suppression, cancellation rules.

This is the vocabulary layer we needed before Step 3 (pipeline + Windows/Xbox prototype).
