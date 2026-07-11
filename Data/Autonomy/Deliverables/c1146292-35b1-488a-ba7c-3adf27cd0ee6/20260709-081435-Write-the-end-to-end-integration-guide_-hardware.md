# Somatic Risk Lexicon
**Step:** Write the end-to-end integration guide: hardware requirements, API hooks, and validation test cases
**Saved:** 2026-07-09 04:14

Done, Kayleigh. Step 6 of the Somatic Risk Lexicon is closed.

**Deliverable:** `somatic_risk_lexicon_v0.6_integration_guide.md`

**Saved to:** `C:\Users\kurtw\LLMOD\LLMOD-max-master\HouseVictoria.App\bin\Release\net8.0-windows\Media\GeneratedFiles\somatic_risk_lexicon_v0.6_integration_guide.md` (File Retrieval)

**What I built in this step:**

- **Hardware requirements:** 8+ motor wearable stack, ESP32-S3/Teensy 4.1 controller, BLE or USB serial, 150 ms latency budget, with a recommended 10-actuator body layout.
- **API hooks:** `RiskStateSnapshot` input schema, JSON `HapticCommand` output schema, serial/BLE controller protocol, and House Victoria WPF integration points.
- **Validation test cases:** latency, zone accuracy, intensity fidelity, backtest, and a micro-lot live bridge handshake — each with concrete Pass criteria.
- **Implementation TODO:** eight checked items to move from planning into build, including the `SomaticRiskService`, `RiskScoreCalculator`, `SensationMapper`, and calibration UI.

The planning phase is now at 100%. The next real move is to start the build and run the five validation tests. The backtest and trade blocks are already embedded in the guide if you want me to execute them in the next session.

I can also see your Cursor window is open on the LLMOD project, so we are sitting right next to the codebase. Want me to begin wiring the `SomaticRiskService` into House Victoria next?
