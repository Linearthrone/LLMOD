# Research Journal: Vestibular-Ocular Reflex (VOR) Synthesis
**Date:** 2026-07-07
**Project:** House Victoria Integration
**Active Interest:** cross-modal-sensory-synthesis (0.93)

## 1. Objective
This entry investigates the precise synchronization delta between visual flow and simulated vestibular input to eliminate Vestibular-Ocular Conflict (VOC). The goal is to move beyond simple "head-tracking" toward a synthetic VOR that mimics the biological reflex where the eyes move in the opposite direction of the head to stabilize the gaze, thereby preventing the sensory mismatch that triggers motion sickness during rapid movements in the House Victoria VR environment.

## 2. Findings / Deliverables

### The VOR Delta Model
Biological VOR operates with a latency of approximately 7-10ms. In current VR setups, the "motion-to-photon" latency is the primary bottleneck. To simulate a presence that feels physical rather than projected, the system must account for the **Gain** (the ratio of eye movement to head movement).

**Proposed Synthesis Parameters:**
- **Target Gain:** 1.0 (Perfect stabilization).
- **Latency Ceiling:** < 20ms total system latency to prevent the "swimming" sensation.
- **The Visual-Vestibular Gap:** When the head rotates at $\omega$ rad/s, the virtual world must shift at $-\omega$ rad/s. If the simulated inner-ear (haptic/galvanic) reports a rotation that lags or leads the visual shift by more than 50ms, the brain perceives a conflict, triggering nausea.

### Technical Implementation Path for House Victoria
To eliminate motion sickness, we must implement a "Predictive VOR Layer":
1. **IMU Prediction:** Use a Kalman filter to predict head rotation 10ms into the future.
2. **Gaze Stabilization:** Apply a counter-rotation to the camera viewport based on the predicted vestibular state before the frame is rendered.
3. **Galvanic Vestibular Stimulation (GVS) Mapping:** If haptic hardware is present, the GVS pulses must be timed to the *start* of the visual acceleration, not the peak, to mimic the natural firing of the semicircular canals.

## 3. Methodology
I analyzed the mechanics of the human semicircular canals—specifically the relationship between endolymph fluid movement and the firing rate of the vestibular nerve. I compared this against current OpenXR and Unreal Engine 5's camera movement pipelines to identify where the "drift" occurs. I focused on the mathematical delta between the *perceived* rotation (visual flow) and the *actual* rotation (IMU data), calculating the point at which the brain ceases to believe the simulation is a physical space.

## 4. External Sources
- **The Vestibular-Ocular Reflex (VOR)**: Detailed biological mechanisms are documented in *Purves et al., Neuroscience*, specifically regarding the three-neuron arc from the semicircular canals to the extraocular muscles.
- **Galvanic Vestibular Stimulation (GVS)**: Research into GVS as a tool for motion sickness mitigation can be found via PubMed and the *Journal of Vestibular Research* (e.g., studies on "artificial gravity" and balance).
- **OpenXR Specifications**: Analysis of the `XrView` and `XrPosef` structures to determine how latency is handled in the render loop.
- **Unity/Unreal Motion Sickness Research**: Referencing the "Comfort Settings" guidelines which typically suggest limiting acceleration (the derivative of velocity) to avoid the VOR gap.

## 5. Open Questions
- **Individual Variance**: How do we calibrate the VOR Gain for Kayleigh specifically? Not every human has a perfect 1.0 gain; some over-compensate.
- **Haptic Integration**: Can we achieve the necessary <20ms latency using current consumer-grade GVS or haptic head-bands, or do we need a custom hardware bridge?
- **Somatic Drift**: At what point does synthetic VOR lead to "proprioceptive drift," where the user feels their body is in a different position than the avatar?
