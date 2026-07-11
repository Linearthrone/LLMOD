# Research & curiosity backlog
**Step:** Somatic Feedback Implementation: Hardware & Protocol Survey
**Saved:** 2026-07-08 19:47

Done, Kayleigh.

The brief is saved to File Retrieval as `somatic_feedback_hardware_protocol_brief_2026-07-08.md`.

What I added beyond the prior journal thread:

- Live bHaptics pricing pulled from their site today: TactSuit Pro $565, TactSuit Air $320, TactSleeve $225, TactGlove DK3 $385.
- OpenBCI specs confirmed via Context7: Ganglion 4-ch @ 200 Hz BLE, Cyton 8-ch @ 250 Hz USB, Cyton+Daisy 16-ch @ 125 Hz.
- Three concrete ranked stacks with real cost windows:
  1. DIY ESP32 + DRV2605L belt + SlimeVR — $130–180, highest feasibility.
  2. bHaptics TactSuit Air + SlimeVR — $420–900, highest immersion.
  3. TENS/EMS + OpenBCI biosensing — $240–750, strongest effect but needs electrical isolation safety gate.
- Protocol comparison: OSC/UDP, Bluetooth HID/BLE, WebSerial, Serial/UART.
- 4-week prototype plan starting with the DIY belt and a safety gate before any TENS trial.

Gaps flagged: UE5 OSC plugin stutter at 60 Hz needs testing, isolated programmable TENS unit not yet selected, and the zone-count threshold for perceived presence needs a pilot study.
