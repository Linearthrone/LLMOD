# House Victoria — Consolidated Guide

**Last updated:** March 2026  

This document is the **single entry point** for understanding the program, where development stands, and which other docs to open for depth. Detailed references remain in the linked files below.

---

## 1. What House Victoria Is

A **Windows desktop overlay** (WPF, .NET 8) in the spirit of Xbox Game Bar: glass trays, SMS/MMS-style AI chat, project management, system monitoring, data banks, optional voice (STT/TTS), optional Unreal WebSocket, optional MetaTrader 4 bridge, and an optional **COVAS: Next** bridge for Elite Dangerous.

**Required for core AI chat:** Ollama (and typically the Python **MCP server** on port 8080 for agent features). Other services are optional.

---

## 2. Documentation Map (What to Read)

| Need | Document |
|------|----------|
| **Day-to-day use, windows, settings, troubleshooting** | [HouseVictoria_UserGuide.md](HouseVictoria_UserGuide.md) |
| **Feature-by-feature technical inventory (screens, services, gaps)** | [HouseVictoria_Documentation.md](HouseVictoria_Documentation.md) |
| **Roadmap detail, phased plans, risks, success metrics** | [HouseVictoria_Development_Roadmap.md](HouseVictoria_Development_Roadmap.md) |
| **Unified memory / vector architecture (target vs stubs)** | [HouseVictoria_MemoryDesign.md](HouseVictoria_MemoryDesign.md) |
| **Elite Dangerous + COVAS: Next** | [COVAS_ELITE_DANGEROUS_SETUP.md](COVAS_ELITE_DANGEROUS_SETUP.md) |
| **MetaTrader 4 file bridge** | [MT4_INTEGRATION_SUMMARY.md](MT4_INTEGRATION_SUMMARY.md), `MT4Bridge/README.md` |
| **ComfyUI custom workflows for image gen** | [ComfyUI_Custom_Workflow_Guide.md](ComfyUI_Custom_Workflow_Guide.md) |
| **MCP server (Python)** | `MCPServer/README.md`, `MCPServer/QUICK_START.md` |
| **Speech-to-text server** | `STTServer/README.md` |
| **Build / repo overview** | [README.md](../README.md) |
| **Multi-agent task queue, team goals (`Docs/agents`)** | [Agent_Task_Queue_And_Goals.md](Agent_Task_Queue_And_Goals.md) |

The three large docs (**UserGuide**, **Documentation**, **Roadmap**) overlap on status; **this guide** is the canonical **current status** summary. When they disagree, prefer **implementation in code** and **this section** first.

---

## 3. Where Development Stands (March 2026)

### Mature / production-usable

- **Shell & UX:** Main tray, top tray (drag-drop → data banks, projects, logs, generated files), system monitor drawer, dark Material Design theme, settings validation and import/export.
- **AI:** Ollama (and LlamaCpp path), personas, LLM parameters, model load/pull, MCP wiring per persona, system prompt editing.
- **Chat:** SMS/MMS window with conversations, media attachments, AI timeouts, STT/TTS paths for voice-style calls (transcription + spoken replies — not full WebRTC video).
- **Projects:** Full CRUD, filters, sorts, detail dialog (roadblocks, artifacts, AI logs).
- **Data banks:** Management window from top tray; CRUD for banks and entries.
- **Logs:** Global log directory with export.
- **System monitor:** CPU/RAM/uptime; CPU temp via WMI; **NVIDIA GPU** usage/temp/fan when NVML (`nvml.dll`) is available; otherwise GPU fields may read zero. Server status/restart patterns for Ollama, MCP, TTS, Unreal endpoint.
- **Integrations (optional):** `ITradingService` / MetaTrader 4 file bridge; COVAS OpenAI-compatible bridge (`App.config`); ComfyUI-oriented image generation settings per app.

### Partially done (works with limits)

| Area | State |
|------|--------|
| **Virtual environment (Unreal)** | `UnrealEnvironmentService` implements WebSocket connect, send/receive, reconnect — **not end-to-end validated** with a real Unreal build; protocol must match your game/plugin. |
| **Video “calls”** | `VideoCallWindow` + call state + TTS greeting exist; **no WebRTC** / no real camera or remote video pipeline. |
| **Image generation** | Stable Diffusion / ComfyUI-style paths supported when configured; Ollama-native image gen may be limited or throw — use SD/ComfyUI as documented. |
| **Hardware** | Non-NVIDIA GPUs: no vendor SDK integration; WMI-only fallbacks are weak for GPU sensors. |

### Semantic / vector memory (when enabled)

- **Ollama embeddings** (`/api/embed` with fallback to `/api/embeddings`) populate vectors; configure **Settings → Persistent Memory → Semantic memory** (Postgres connection, embedding model, dimensions).
- **PgVector** table `house_victoria_memory_embeddings` is created automatically; hybrid search combines lexical FTS with vector similarity.
- **MCP** `vector_search` uses the same Postgres table when **`PGVECTOR_CONNECTION_STRING`** is set in the MCP environment (see `MCPServer/house_victoria_mcp/memory/vector_search.py`). Also set **`OLLAMA_HOST`** / **`OLLAMA_EMBEDDING_MODEL`** if not using defaults.
- If pgvector is off, behavior is SQLite + FTS only (see [HouseVictoria_MemoryDesign.md](HouseVictoria_MemoryDesign.md) for the full target architecture).

### Rough completion picture

Core product (chat, projects, settings, data banks, monitoring, logging): **high maturity**.  

Advanced items (real video calling, validated Unreal loop, true vector memory, broad GPU support): **partial or planned**.

---

## 4. What Is Left To Do (Prioritized)

1. **Video calling (real A/V)** — Choose WebRTC (or hosted SDK), wire signaling, local/remote video, permissions. Largest communication gap.
2. **Virtual environment** — Validate against a real Unreal WebSocket server; align JSON protocol; exercise `VirtualEnvironmentControlsWindow` against live scenes/avatars if desired.
3. **Semantic memory** — Replace embedding stubs; implement pgvector (or another vector store); wire MCP `vector_search` to the same backend.
4. **Image generation** — Harden paths (Ollama vs SD vs ComfyUI); keep [ComfyUI_Custom_Workflow_Guide.md](ComfyUI_Custom_Workflow_Guide.md) in sync with Settings UI.
5. **GPU monitoring** — Optional AMD/Intel/vendor paths beyond NVML+WMI.
6. **Quality bar** — Automated tests, performance work on large lists, installer/docs alignment.

---

## 5. Potential Features (Relevant to the System)

- **Trading:** Richer MT4/MT5 workflows, NL commands from chat, risk rules, backtest UX in-app.
- **Games / sims:** Deeper COVAS persona tooling; game-specific tool plugins via MCP.
- **Collaboration:** Multi-user or shared project workspaces (would need backend design).
- **Memory:** Summarization jobs, retention policies, cross-persona search UX in the app.
- **Automation:** Scheduled tasks, triggers from system monitor thresholds.
- **Platform:** Port or companion service on non-Windows (only Windows/WPF today).

---

## 6. Architecture (Abbreviated)

```
HouseVictoria.App (WPF, MVVM)
    → HouseVictoria.Services (Ollama, SMS/MMS, SQLite/Dapper, MCP client, monitors, Unreal WS, logging, files, optional MT4)
    → HouseVictoria.Core (interfaces, models, events)
    → External: Ollama, MCP HTTP, optional TTS/STT, Unreal, MT4 via files, optional Postgres for future vectors
```

---

## 7. Maintaining This Guide

When you ship a feature or change status:

1. Update **§3** and **§4** here first.  
2. Adjust [README.md](../README.md) if user-visible scope changes.  
3. Refresh the detailed doc that owns the topic (UserGuide for UX, Documentation for technical inventory, Roadmap for long-range planning) so they do not contradict this file.

---

*End of consolidated guide.*
