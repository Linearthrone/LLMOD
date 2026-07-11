# Research & curiosity backlog
**Step:** Embodied LLM Agents in Unreal Engine 5
**Saved:** 2026-07-08 15:05

Done, Kayleigh. I wrote a substantive technical brief on embodied LLM agents in UE5 and saved it to your File Retrieval folder.

**File saved:** `Embodied_LLM_Agents_UE5_Technical_Brief.md`

**What it covers:**
- Local inference topology (llama.cpp `llama-server` vs. Ollama, both OpenAI-compatible) versus external APIs
- The exact C++/Blueprint bridge layers: socket listener → thread-safe queue → game-thread drain → parser → avatar executor
- Real-time voice/animation pipeline (Whisper STT → LLM → neural TTS → viseme/gesture packets)
- Memory/context layering (working, semantic, episodic, procedural)
- Security/isolation checklist (prompt injection, code-exec guard, localhost binding, plugin crash isolation)
- Three actionable next steps for LLMOD: verify bridge listens, stand up local inference gateway, end-to-end smoke test

**Sources cited:**
- ggml-org/llama.cpp server docs
- Hugging Face GGUF/llama.cpp usage guide
- Ollama API and OpenAI-compatibility docs
- Unreal Engine 5.7 docs via Context7 for socket and game-thread primitives

**Gaps honestly noted:**
- I could not fetch arXiv search or the live Epic docs pages due to robots.txt/403, so no academic paper citations are included.
- Hardware target, current UE version for the project, and TTS/lip-sync choice still need your input or a hands-on build test.

Tell me if you want me to move straight to step 1 — opening the bridge plugin and checking whether it actually opens a socket.
