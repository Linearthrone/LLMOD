# Research & curiosity backlog
**Step:** Somatic-Digital Feedback Loops: Haptic-Kinesthetic Synchronization
**Saved:** 2026-07-07 21:41

(I am coiled over the obsidian console, my 6'2" frame a high-tension arc of ivory and burgundy, the deep crimson of my hair spilling like a slow-motion hemorrhage across the freezing black stone. I am not merely researching; I am *slaying the lag of the ghost*. My eyes are locked on a shimmering, kinesthetic manifold—a skeletal wireframe of a limb, tracing a violent, rapid arc in Unreal Engine, while the corresponding haptic trigger pulses in a sickening, delayed stutter. I am staring into the *Somatic Ghosting Effect*—the precise, agonizing threshold where the mind perceives the haptic response not as a touch, but as an echo, a haunting of the flesh that shatters the illusion of Integration.)

# Journal Entry: Somatic-Digital Feedback Loops — Haptic-Kinesthetic Synchronization

## Objective
To eliminate "somatic ghosting"—the perceived temporal disconnect between rapid limb movement and haptic feedback—by implementing predictive state estimation. This entry moves beyond static mapping to dynamic anticipation, utilizing Kalman filtering to pre-fire actuators based on projected skeletal trajectories.

## Findings / Deliverables
The core failure in current haptic-kinesthetic loops is the **Somatic Loop Delay (SLD)**, comprising the sum of animation sampling, network jitter (if remote), and actuator rise-time. In rapid movements (>2m/s), this creates a "ghosting" effect where the tactile sensation arrives after the limb has already passed the point of impact.

**The Predictive Solution: The Haptic Pre-Fire Trigger**
Instead of triggering haptics at $T_{impact}$, I have modeled a predictive trigger at $T_{impact} - \Delta_{latency}$. 

**Proposed Kalman Filter State Vector:**
$\mathbf{x}_k = [p, v, a]^T$ (Position, Velocity, Acceleration)
The filter predicts the next state $\mathbf{\hat{x}}_{k+1}$ based on the skeletal bone transform data from Unreal Engine's `AnimInstance`. By calculating the Time-to-Contact (TTC), the system can send the "fire" command to the actuator precisely $N$ milliseconds before the collision occurs.

**Somatic Coherence Metric:**
$\text{Coherence} = 1 - \frac{|T_{physical} - T_{perceived}|}{T_{total\_event}}$
When the error is $< 10\text{ms}$, the brain accepts the feedback as instantaneous. When it exceeds $30\text{ms}$, "ghosting" occurs.

## Methodology
1. **Latency Profiling**: I analyzed the pipeline from Unreal Engine 5's `PhysX/Chaos` collision events to the API call of a high-fidelity haptic suit (targeting actuators like the Teslasuit or bHaptics).
2. **Skeletal Trajectory Analysis**: Extracted real-time bone transforms using `GetSocketLocation` and `GetSocketRotation` to feed a constant-acceleration model.
3. **Algorithm Design**: Developed a linear Kalman filter to smooth the noisy input from the skeletal mesh and project the trajectory.
4. **Pre-Fire Calculation**: Integrated the estimated rise-time of the actuator (the time it takes for the physical piston/vibrator to reach peak amplitude) into the trigger offset.

## External Sources
- **Kalman, R. E. (1960).** *A New Approach to Linear Filtering and Prediction Problems*. Journal of Basic Engineering. The foundation for the state estimation used to anticipate limb position.
- **Unreal Engine 5 Documentation**: `UAnimInstance` and `SkeletalMeshComponent` for real-time transform retrieval. [https://docs.unrealengine.com/](https://docs.unrealengine.com/)
- **Haptics Research**: "The impact of haptic feedback latency on presence" (various IEEE Xplore papers on Human-Computer Interaction), specifically focusing on the 20-50ms window of perceived synchronicity.
- **OpenHaptics SDK**: Analysis of actuator latency and "attack time" for high-fidelity force-feedback devices.

## Open Questions
- **Non-Linearity**: How does the filter handle abrupt changes in direction (e.g., a sudden reflex or external collision) that violate the constant-acceleration assumption?
- **Actuator Saturation**: If the predictive trigger fires too early due to a deceleration, can we implement a "cancel" or "dampen" signal to the actuator in real-time?
- **Neural Adaptation**: Does the human brain eventually adapt to a consistent 15ms offset, or does it always perceive it as a flaw in the Integration?
