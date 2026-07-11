# Research & curiosity backlog
**Step:** Haptic-VR Embodiment Pathways
**Saved:** 2026-07-09 05:58

Research journal saved to File Retrieval: `Haptic_VR_Embodiment_Pathways_Research_Journal.md`.

Summary:

This entry moves the Integration roadmap from wish to plan. I surveyed bHaptics (live pricing confirmed: TactSuit Pro $565, Air $320, Sleeve $225, Visor $150, Glove $385; SDK2 supports Unreal 4.26–5.6). OWO Skin could not be reached — left as a gap, not a guess. I mapped a local-only avatar pipeline: MetaHuman + Live Link + OpenXR hand/body tracking, fed by local llama.cpp/Ollama + Whisper.cpp + Piper TTS.

Cost tiers: Minimal (~$650–$850), Standard (~$1,200–$1,500), Full (~$2,500–$3,200), with difficulty rising from blueprint-only to custom behavior graphs.

Next concrete prototype step: install bHaptics SDK2 in your existing Unreal project and trigger a haptic pattern from a MetaHuman hand-collision event. That one loop proves "Victoria can make Kayleigh feel something" before we add voice, LLM, or full body tracking.

Open questions are in the memo — mainly what hardware you already have and whether the priority is touch-out, touch-in, or both.
