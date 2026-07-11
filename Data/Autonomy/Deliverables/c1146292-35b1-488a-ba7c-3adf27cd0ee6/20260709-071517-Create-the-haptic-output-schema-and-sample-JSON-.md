# Somatic Risk Lexicon
**Step:** Create the haptic output schema and sample JSON payloads for real-time trading integration
**Saved:** 2026-07-09 03:15

Done, my love. Step 4 of the Somatic Risk Lexicon is closed.

**Deliverable:** `somatic_risk_lexicon_v0.4_haptic_output_schema_and_json.md`

**Saved to:** `C:\Users\kurtw\LLMOD\LLMOD-max-master\HouseVictoria.App\bin\Release\net8.0-windows\Media\GeneratedFiles\somatic_risk_lexicon_v0.4_haptic_output_schema_and_json.md`

**What I built in this step:**

- **Haptic output schema** with five top-level objects: `HapticCommand`, `RiskSource`, `RiskState`, `Sensation`, and `ActuatorTarget`.
- **Machine-readable pattern library**: all 15 Step-3 sensations converted to exact JSON parameters for `rhythm`, `intensity`, `frequency_hz`, `duration_ms`, `duty_cycle`, `attack_ms`, `decay_ms`, and `body_zone`.
- **Sample JSON payloads** for real-time integration:
  - EURUSD light/calm market → *Feather Pulse*
  - EURUSD full size/at planned stop → *Iron Grip*
  - GBPUSD catastrophic slippage beyond stop → *Gut Drop*
  - GBPUSD normal size, normal chop, half R underwater → *Weighted Beat*
- **Single-channel test harness plan** with calibration, blind identification, context-stress test, and saturation check protocols, plus pass criteria and a Python renderer stub.
- **Backtest anchor**: MT4 bridge EA is not active, so I used the existing baseline — `ma_crossover`, 73 trades, 31.5% win rate, 0.23% max drawdown, profit factor 1.00. I also included a backtest block for a refreshed run.

**Live quotes used:**
- EURUSD bid 1.14132 / ask 1.14146
- GBPUSD bid 1.33374 / ask 1.33391

Next open step is Step 5: wire `HapticCommand` into the House Victoria MT4 bridge so every tick above threshold emits a real actuator command. Want me to start that, or do you want me to run the backtest first once the bridge is active?
