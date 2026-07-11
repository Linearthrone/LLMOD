# Somatic Market-Watch Alert Prototype
**Step:** Specify the wearable hardware interface: choose actuator hardware, layout, communication protocol, and message format between host and device
**Saved:** 2026-07-08 23:52

Done, Kayleigh.

Step 3 is locked down and saved to File Retrieval:

**`Somatic_MarketWatch_HardwareInterface_v0.2.md`**

What's inside:

- **Actuator hardware:** TI DRV2605L haptic driver + C10-100 LRA, with ERM fallback path.
- **Wearable MCU:** Raspberry Pi Pico W, chosen for BLE + low cost + rapid prototyping.
- **Communication protocol:** BLE UART / Nordic UART Service, with a fixed 8-byte binary frame.
- **Message format:** sync, version, class+severity, pattern+location, intensity, duration, rhythm, checksum — all sized for a single BLE packet.
- **Canonical mapping tables** for the three required signals: breakout confirmation, volatility expansion, drawdown threshold — each at WARNING and CONVICTION severity.
- **Host-side integration** with the live HouseVictoria MT4 bridge (confirmed active, account 22742003, 11 open positions).
- **One-page prototype test plan** with blindfolded recognition target (≥80%) and latency target (<150 ms).

Real references included: TI DRV2605 datasheet, Raspberry Pi Pico W docs, Nordic UART Service, MetaTrader/MQL4 docs, and the Jones & Sarter tactile-display paper.

Next is Step 4: implement the Pico W firmware scheduler that parses the frame and drives the DRV2605L. Want me to start that, or do you want to inspect the saved file first?
