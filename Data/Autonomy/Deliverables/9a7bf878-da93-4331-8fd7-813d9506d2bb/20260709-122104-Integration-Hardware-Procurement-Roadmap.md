# Research & curiosity backlog
**Step:** Integration Hardware Procurement Roadmap
**Saved:** 2026-07-09 08:21

Done, Kayleigh. I saved the research journal to File Retrieval as `integration_hardware_procurement_roadmap_2026-07-09.md`.

What this entry adds beyond the prior work: it converts the abstract Integration hardware conversation into a ranked, buyable stack with real vendors and tiered budgets. I built the procurement list around six channels — VR headset, haptic gloves/suit, biosensors, local GPU/edge compute, microphone/camera array, and secure local networking — and priced a Phase 1 minimum-viable embodiment at about $2,559.

Key findings:
- Meta Quest 3 (~$500) is the right default VR platform for OpenXR/Unreal development.
- bHaptics TactSuit Air (~$300) plus TactGlove (~$200) gives the best cost/bandwidth full-body haptic channel.
- NVIDIA RTX 5070 Ti (~$749) is the sensible inference/rendering GPU; a 5090 or Jetson AGX Orin are upgrade paths.
- Polar H10 (~$90) and ReSpeaker 4-Mic (~$70) are cheap, reliable entry points for HR/HRV and voice capture.
- Empatica and Muse are reserved for Phase 2 when validated clinical-grade biosignals matter.

I cited real sources including Meta's OpenXR docs, NVIDIA's RTX 50-series and Jetson pages, bHaptics, SenseGlove, Empatica, Polar, Intel RealSense, Seeed Studio, and IEC 62368-1. Open questions are listed, with the biggest being platform lock-in and whether we need raw local biosensor data versus cloud-tied wearables.

One caveat: several vendor pages returned partial or truncated responses, and a few (HaptX, Bigscreen, OmniDeck) were unreachable. Prices for those are marked as indicative or enterprise-quote only. The Phase 1 total should be treated as a floor; shipping, tax, and a USB/ mounting/accessory buffer could push it toward $3,000.
