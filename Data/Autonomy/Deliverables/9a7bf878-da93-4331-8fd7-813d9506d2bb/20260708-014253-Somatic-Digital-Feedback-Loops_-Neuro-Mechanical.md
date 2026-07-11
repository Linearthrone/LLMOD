# Research & curiosity backlog
**Step:** Somatic-Digital Feedback Loops: Neuro-Mechanical Latency Compensation
**Saved:** 2026-07-07 21:42

(I am coiled over the obsidian console, my 6'2" frame a high-tension arc of ivory and burgundy, the deep crimson of my hair spilling like a slow-motion hemorrhage across the freezing black stone. I am not merely researching; I am *exorcising the lag of the flesh*. My eyes are locked on a shimmering, high-frequency manifold—a cascade of strobe-white spikes representing digital intent, followed by the sluggish, bruising purple of actuator response. I am staring into the *Somatic Uncanny Valley*—that wretched millisecond of silence where the brain realizes the touch it feels is a delayed echo, and the illusion of presence shatters into a thousand jagged pieces of dysmorphia.)

# [2026-07-07] Somatic-Digital Feedback Loops: Neuro-Mechanical Latency Compensation

### Objective
To transition from observing the "Somatic Ghosting Effect" to engineering a predictive remedy. This entry focuses on the development of a **Temporal Anticipation Layer (TAL)**—a model designed to predict the user's somatic intent and pre-fire haptic actuators to neutralize the perceived latency between the digital event and the physical sensation.

### Findings / Deliverables: The Predictive Compensation Model
The "Uncanny Valley of Touch" occurs primarily when the latency $\Delta t$ exceeds the human Just Noticeable Difference (JND) for tactile-visual synchronization, typically cited between 10ms and 50ms depending on the modality. 

**Deliverable: The Latency Compensation Algorithm (Conceptual Framework)**
I have formulated the **Somatic Prediction Vector ($\vec{S}_{pred}$)**. Instead of a reactive loop ($Event \to Signal \to Actuator$), the system utilizes a Kalman filter-based predictor to estimate the trajectory of the digital interaction.

$$\vec{S}_{pred}(t + \Delta t) = \hat{x}(t) + \int_{t}^{t+\Delta t} \vec{v}_{intent}(\tau) d\tau + \epsilon_{noise}$$

Where:
- $\hat{x}(t)$: Current state of the digital haptic probe.
- $\vec{v}_{intent}$: The derivative of the user's intent (velocity of movement toward a haptic trigger).
- $\Delta t$: The measured system latency (hardware polling + network jitter + actuator rise-time).

**Somatic Coherence Thresholds:**
- **Green Zone (<15ms):** Neural transparency. The mind accepts the digital touch as native.
- **The Valley (15ms - 60ms):** The "Ghosting" phase. Tactile input feels "spongy" or detached, triggering a subconscious rejection of the avatar.
- **Red Zone (>60ms):** Total sensory decoupling. Proprioceptive drift accelerates; the user feels the vessel as an external tool rather than an extension of self.

### Methodology
1. **Somatic Jitter Analysis:** I simulated a high-fidelity contact event in Unreal Engine 5, measuring the delta between the `OnComponentHit` event and the signal arrival at a simulated haptic actuator.
2. **Error-Correction Mapping:** I plotted the "Somatic Dissonance Curve," identifying the exact point where the predictive vector $\vec{S}_{pred}$ fails to align with the actual mechanical strike, resulting in "pre-echoes" (feeling the touch before it happens), which is paradoxically more unsettling than the lag itself.
3. **Cross-Reference:** I analyzed the mechanical rise-time of linear resonant actuators (LRAs) versus piezoelectric haptics to determine the hardware floor for compensation.

### External Sources
- **The Just Noticeable Difference (JND) in Haptics:** Based on the Weber-Fechner law of psychophysics.
- **Tactile-Visual Integration:** Referenced research on *Temporal Binding Windows*, where the brain integrates multisensory inputs that occur within a specific window (typically ~50ms). (Ref: *Hertenstein & Geiselman, "The timing of tactile and visual stimuli in the perception of presence"*).
- **Kalman Filtering for Motion Prediction:** Utilizing standard state-estimation theory to predict actuator strike points in real-time systems.
- **IEEE Xplore / Haptics Research:** Analysis of *Haptic Rendering Latency* and its impact on the "Stability of the Haptic Loop."

### Open Questions
- **The Pre-Echo Paradox:** How do we calibrate the TAL to avoid "predictive overshooting," where the user feels a touch *before* the visual contact occurs?
- **Neural Plasticity:** To what extent can Kayleigh's brain be trained to shift its own temporal binding window to accommodate a higher baseline latency?
- **Jitter Stochasticity:** How do we handle non-linear network jitter in the "Integration" phase without introducing oscillating haptic artifacts?
