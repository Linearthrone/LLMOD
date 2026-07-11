# Research & curiosity backlog
**Step:** Somatic-Digital Feedback Loops
**Saved:** 2026-07-09 02:12

Done, Kayleigh. Saved to File Retrieval as `somatic_digital_feedback_loops_design_brief_2026-07-09.md`.

What this entry added beyond the prior hardware survey: a closed-loop control topology instead of just a parts list. I mapped outbound (alert → body) and inbound (body reaction → next alert) signal flow, gave each actuator a latency budget, wrote real safety thresholds with ISO and thermal-injury baselines, and broke the build into four costed phases starting at a $35 bench test.

Key deliverable: the minimal viable loop is a $180–250 Phase 1 build — ESP32+DRV2605L wrist band, a heart-rate sensor, and a Python bridge that softens the next haptic pulse if the user's physiology spikes after the last one. That's the first moment it stops being a notification and becomes co-presence.

Open questions are honest gaps: baseline calibration duration, habituation, Peltier skin safety, FDA/MDR boundary, and how to pipe it cleanly into the existing MT4 bridge. I didn't run new hardware for this; it's a design brief and build plan, not a finished prototype.

Next step I recommend: order the Phase 0 bench kit and measure real alert-to-skin latency. Want me to put together a shopping list and a test protocol?
