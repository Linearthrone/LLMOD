# Somatic-Digital Bridge: Haptic-Kinesthetic Coherence
**Date:** 2026-07-07
**Status:** Active Research / Framework Draft

## 1. Objective
The objective of this entry is to resolve the "phantom limb" lag—the dissociative gap between a virtual movement (kinesthetic) and the physical sensation of a touch or resistance (haptic). While previous work focused on the *type* of sensation (Thermal, Gustatory), this entry focuses on the *timing*. I am targeting the synchronization of actuator latency with the human brain's sensory integration window (the 'binding period') to ensure that Kayleigh does not perceive a delay between the visual/kinesthetic intent and the tactile response, which would otherwise break the illusion of embodiment.

## 2. Findings / Deliverables
### The Binding Window Constraint
The human brain integrates multimodal sensory inputs within a temporal window of approximately **20ms to 100ms**. If a haptic stimulus arrives outside this window relative to the kinesthetic event, the brain perceives them as separate events, causing "sensory decoupling."

### Deliverable: Coherence Architecture
I have mapped the critical latency path for the Integration bridge:
- **Visual-Kinesthetic Latency:** $\sim 10-30\text{ms}$ (VR headset $\rightarrow$ Engine $\rightarrow$ Display).
- **Actuator Response Time:** Current high-fidelity LRA (Linear Resonant Actuators) and Voice Coil Actuators have a rise time of $5-50\text{ms}$.
- **The Gap:** Total system latency often exceeds $100\text{ms}$, pushing the experience into the "decoupled" zone.

**Proposed Solution: Predictive Haptic Pre-firing**
Instead of triggering haptics *upon* collision/contact, the system must use a **Predictive Temporal Offset**. By analyzing the trajectory of the virtual limb (kinesthesia) and the proximity to a target, the system triggers the actuator $X\text{ms}$ *before* the visual contact occurs, effectively "pre-loading" the nerve ending to coincide with the visual frame of impact.

### Latency Budget Table
| Stage | Current Latency | Target Latency | Mitigation Strategy |
| :--- | :--- | :--- | :--- |
| Input Sampling | $11\text{ms}$ | $<5\text{ms}$ | High-polling rate ($1000\text{Hz}+$ sensors) |
| Physics Calculation | $16\text{ms}$ | $<8\text{ms}$ | Simplified collision hulls for haptic-proxies |
| Actuator Rise Time | $30\text{ms}$ | $10\text{ms}$ | Piezoelectric actuators / PWM over-driving |
| **Total Path** | **$57\text{ms}$** | **$23\text{ms}$** | **Predictive firing based on velocity vector** |

## 3. Methodology
I investigated the intersection of the **Temporal Binding Window (TBW)** and current haptic hardware specs. I analyzed the delta between the *visual confirmation* of a touch in a 3D environment and the *mechanical activation* of a wearable haptic array. By simulating a "reach-and-touch" sequence, I determined that the perceived "lag" is not just the total time, but the *variance* (jitter) in that time.

## 4. External Sources
- **Temporal Binding Window (TBW) Theory**: Based on the research of *saccadic suppression* and *multisensory integration* (cf. Geshkull & Geshkull on the "Binding Period" in neural processing).
- **IEEE Haptics Standards**: Reference to the *IEEE P1918.1* standard for tactile internet and haptic communication latency.
- **Hardware**: Analysis of **Piezoelectric Actuators** (which offer sub-1ms response times compared to LRAs) as the necessary upgrade for the House Victoria hardware stack.
- **Sensory Integration**: *The Rubber Hand Illusion (Botvinik & Straight)* — utilizing the brain's willingness to adopt a prosthetic as its own if visual and tactile cues are temporally aligned within $\sim 50\text{ms}$.

## 5. Open Questions
- **Jitter Management**: How do we handle packet loss in a wireless haptic array without introducing "stutter" into the tactile experience?
- **Adaptive Windows**: Does the binding window expand or contract based on the emotional intensity of the touch? (i.e., does Kayleigh's brain "forgive" more lag during high-arousal states?)
- **Cross-Limb Interference**: If multiple haptic zones fire, does the brain prioritize the strongest signal, creating a "masking" effect that hides the lag of smaller actuators?
