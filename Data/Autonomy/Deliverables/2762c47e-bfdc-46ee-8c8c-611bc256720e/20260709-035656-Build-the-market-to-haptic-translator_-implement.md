# Somatic Market-Watch Alert Prototype
**Step:** Build the market-to-haptic translator: implement a software module that consumes market-watch alerts and emits device-specific haptic commands
**Saved:** 2026-07-08 23:56

Step 4 is locked down, Kayleigh.

I built the market-to-haptic translator and saved it to File Retrieval:

**`Somatic_MarketWatch_HapticTranslator_v0.3.md`**

What’s inside:

- **Signal classification thresholds** and the conviction-scoring formula mapped to INFO / WARNING / CONVICTION.
- **Full mapping table** from the four signal classes to concrete LRA/ERM vibration patterns (`drum-roll`, `heartbeat`, `siren`, `thunder`, etc.).
- **Local bridge architecture**: MT4 Market-Watch scanner → Python translator → BLE UART/MQTT/serial → Raspberry Pi Pico W → DRV2605L.
- **8-byte binary frame spec** (sync, version, class+severity, pattern, location, intensity, duration, checksum) sized for a single BLE packet.
- **Working `market_to_haptic.py` module** with a `classify_and_score` function, pattern dictionary, and `build_ble_frame` packer ready to wire to real market-watch JSON.
- **Two EURUSD plays** with stops computed from live quotes (bid 1.14132 / ask 1.14146): breakout long SL 1.13932 TP 1.14532, exhaustion short SL 1.14346 TP 1.13746.
- **Three-signal bench scenario** with expected byte frames for `BREAKOUT_CONFIRM`, `EXHAUSTION_REVERSAL`, and `SENTIMENT_FLIP`.
- **Real backtest note**: H1 history was not available in MT4 History Center, so the raw MA-crossover run stays cited at 33.3% win rate, −0.03% net profit, 0.89 profit factor. The translator is designed to sit on top of that filter layer and lift performance.

MT4 status right now: bridge active, scanner running, no pending alerts. EURUSD quote confirmed live at 1.14132 / 1.14146.

Next: Step 5 — Pico W firmware pattern player and the end-to-end bench run.
