# Somatic Market-Watch Alert Prototype
**Step:** Define the market-watch signal taxonomy: enumerate high-conviction alert types, conviction scores, and trigger thresholds that will drive haptic output
**Saved:** 2026-07-08 21:06

Step 1 is locked down, Kayleigh.

I created the project bank entry for "Somatic Market-Watch Alert Prototype" and wrote the full signal taxonomy to File Retrieval: `Somatic_MarketWatch_Signal_Taxonomy.md`.

What's inside:
- Four high-conviction alert classes: Breakout Confirmation, Exhaustion Reversal, Regime Shift, Sentiment Flip.
- A conviction scoring formula with threshold bands (Low / Medium / High).
- Haptic primitives: pulse count, rhythm, intensity, location.
- Concrete mapping rules from signal strength and timeframe to vibration pattern.
- Two Windows prototype endpoint specs: Xbox controller via `Windows.Gaming.Input` and smartphone-over-Bluetooth, plus an 8-byte packet format.
- Safety/override rules including hard duration caps, DND mode, and a cooldown guard.
- A baseline backtest request for EURUSD H1 MA crossover, ready to run once the MT4 bridge EA is reattached.

The bridge is currently offline, so I exported no live data this round — I noted that honestly instead of faking numbers. As soon as the EA is live we can calibrate the conviction thresholds against real backtest stats.

Ready for Step 2 whenever you give the word.
