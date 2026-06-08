# House Victoria — Project Evolution & Technical State

**Version:** 1.0  
**Last updated:** June 2026  

This document consolidates **project history**, **current capabilities**, **incomplete or rough areas**, and **potential future work**. For day-to-day usage, see **[HouseVictoria_UserGuide.md](HouseVictoria_UserGuide.md)**.

---

## Table of Contents

1. [Vision & Design Philosophy](#1-vision--design-philosophy)
2. [Evolution Timeline](#2-evolution-timeline)
3. [Architecture Today](#3-architecture-today)
4. [What the App Does Now](#4-what-the-app-does-now)
5. [Incomplete & Needs Polishing](#5-incomplete--needs-polishing)
6. [Potential Future Additions](#6-potential-future-additions)
7. [Strategic Roadmap (Remote Companion & AI Home)](#7-strategic-roadmap-remote-companion--ai-home)
8. [Repository & Component Map](#8-repository--component-map)
9. [Maintaining This Documentation](#9-maintaining-this-documentation)

---

## 1. Vision & Design Philosophy

House Victoria began as a **modular Xbox Game Bar-style overlay** for Windows: always-accessible AI chat, system telemetry, and productivity tools without leaving your workflow.

The vision expanded into an **AI-at-home platform**:

| Layer | Role |
|-------|------|
| **PC (House Victoria)** | Brain — LLMs, Hermes agent, MCP tools, SQLite/Postgres memory, project orchestration, trading bridge |
| **Unreal Engine** | Body — avatar, world, animation; authoritative for embodied presence |
| **Phone (Android companion)** | Thin remote I/O — text and audio while away; not a second persona host |
| **Elite Dangerous (COVAS)** | Optional ship-computer persona via OpenAI-compatible bridge |
| **MetaTrader 4** | Optional trading execution and market data via file bridge |

**Design patterns throughout:** MVVM, dependency injection, event aggregator, repository-style persistence, circuit breakers for external services, file-based integration where real-time APIs are unavailable (MT4).

**Personality evolution policy (intended):** Controlled adaptation via memory and reviewed persona deltas — not silent unbounded drift.

---

## 2. Evolution Timeline

Development has been **incremental and module-by-module**, with git history showing these major eras:

### Era 0 — Foundation (initial commit → early 2025)

- Solution structure: `HouseVictoria.Core`, `HouseVictoria.Services`, `HouseVictoria.App`
- WPF shell with Material Design dark theme, glass overlay trays
- Ollama AI integration, SQLite persistence, SMS/MMS chat skeleton
- System monitor drawer, main/top trays

### Era 1 — Core product maturity (~2025)

| Milestone | What shipped |
|-----------|--------------|
| **Phase 1–2** | Infrastructure, main tray, top tray, collapsing pull-tab UX |
| **Phase 3** | System monitor (CPU/RAM/WMI temperature, server controls) |
| **Phase 4** | Drag-drop data banks, global log directory, generated files |
| **Phase 5** | AI personas, LLM parameters, model load/pull, MCP wiring |
| **Phase 6** | Full SMS/MMS with media attachments (50 MB), conversation management |
| **Phase 7** | Complete projects board — CRUD, filters, roadblocks, artifacts, AI logs |
| **Settings overhaul** | Validation, per-service test buttons, import/export, avatar/locomotion/tools/memory sections |
| **UI polish** | Multiple color themes, message deletion, GLD and project fixes, selection colors on dark inputs |
| **TTS** | Kokoro / Piper HTTP host, Windows TTS fallback |
| **Image gen** | ComfyUI `/prompt` API, custom workflow placeholders, Stability Matrix paths |

### Era 2 — Agent & embodiment (2025–2026)

| Milestone | What shipped |
|-----------|--------------|
| **Hermes integration** | `HermesAIService`, gateway auto-start, `FallbackAIService`, primary LLM selection |
| **Remote companion** | Kestrel HTTP API (`/api/remote/v1/*`), Bearer auth, headless `--remote-only` mode |
| **Android MVP v2** | Text + audio chat, health check, conversation log |
| **Unreal protocol** | WebSocket client, control-script commands, `companion_remote_exchange`, cross-repo runbook |
| **COVAS bridge** | OpenAI-compatible API for Elite Dangerous |
| **MT4 integration** | `ITradingService`, EA file bridge, MCP mt4_* tools, market watch scanner |
| **Autonomy loop** | `AutonomyOrchestratorService` — background project work, research, art, trading, reflection |
| **Cognition vitals** | ECG-style UI drawer, rhythm/BPM/intensity telemetry |
| **Journals** | `JournalService`, consolidation from autonomy logs, Journals window + reader |
| **AAR** | After Action Reports on project completion — accept/reject workflow |
| **Persona overhaul** | Primary/secondary persona context, shared-information class checkboxes |
| **A2E API** | Cloud image generation provider alongside ComfyUI |
| **Semantic memory (partial)** | PgVector client, Ollama embeddings, MCP `vector_search` — requires Postgres setup |

### Current completion estimate

| Area | Maturity |
|------|----------|
| Core overlay, chat, projects, settings, data banks, logging | **~95%** — production-usable |
| Hermes + MCP agent stack | **~90%** — operational with security caveats |
| Voice calls (STT/TTS) | **~80%** — works; not full duplex WebRTC |
| Autonomy, journals, AAR | **~85%** — functional; tuning and UX polish ongoing |
| Remote companion + Android | **~75%** — MVP delivered; tunnel ops and reliability hardening |
| Image generation | **~80%** — A2E + ComfyUI; edge cases remain |
| MT4 trading | **~70%** — EA bridge works; backtest is demo-level in C# |
| Unreal embodiment | **~50%** — protocol and notify path exist; end-to-end not fully validated |
| Semantic / vector memory | **~40%** — infrastructure present; needs Postgres + real embeddings in production |
| Real video calling | **~15%** — UI shell only |

---

## 3. Architecture Today

```
┌─────────────────────────────────────────────────────────────────┐
│                    HouseVictoria.App (WPF, MVVM)                   │
│  Trays: Main · Top · SystemMonitor · CognitionVitals             │
│  Windows: SMS/MMS · AI Models · Settings · Projects · GLD ·     │
│           DataBanks · Journals · AAR · VideoCall · JournalReader │
└────────────────────────────┬────────────────────────────────────┘
                             │ DI + EventAggregator
┌────────────────────────────▼────────────────────────────────────┐
│                   HouseVictoria.Services                           │
│  AIServices: Ollama · LmStudio · Hermes · Fallback              │
│  Communication · Persistence · Projects · Logging · Files         │
│  MCP · SystemMonitor · UnrealEnvironment · CovasBridge          │
│  RemoteCompanion · Agent · Journals · AAR · Autonomy              │
│  Trading (MT4) · MarketWatch · Memory (PgVector, embeddings)    │
│  TTS (Piper/Kokoro) · STT integration                           │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                   HouseVictoria.Core                             │
│  Interfaces · Models · Events · Utils                            │
└────────────────────────────┬────────────────────────────────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     ▼                       ▼                       ▼
  Ollama              Hermes Gateway           MCP Server (Python)
  :11434              :8642/v1                 :8080
     │                       │                       │
     └───────────────────────┴───────────────────────┘
                             │
     ┌───────────┬───────────┼───────────┬───────────┐
     ▼           ▼           ▼           ▼           ▼
  ComfyUI    Piper TTS    STT :8000   Unreal WS   MT4 EA
  :8188      :5000                      :8888      file bridge
     │
  Postgres+pgvector (optional)
  Remote API :17890 → Android / tunnel
  COVAS bridge :11435 → Elite Dangerous
```

### Technology stack

| Layer | Choice |
|-------|--------|
| UI | WPF .NET 8, MaterialDesignInXaml |
| Patterns | MVVM (CommunityToolkit.Mvvm), DI (Microsoft.Extensions) |
| Database | SQLite + Dapper |
| Logging | Serilog |
| AI | Ollama, Hermes Agent, optional LM Studio |
| Agent tools | Python MCP server, Hermes built-in tools |
| Hardware | WMI + Performance Counters; NVML for NVIDIA GPU |
| Remote | ASP.NET Core Kestrel embedded host |

### Registered services (App.xaml.cs)

`IAIService` (Fallback → Hermes/Ollama), `ICommunicationService`, `IPersistenceService`, `IMemoryService`, `IProjectManagementService`, `ISystemMonitorService`, `IVirtualEnvironmentService`, `ILoggingService`, `IFileGenerationService`, `IMCPService`, `ITTSService`, `ITradingService`, `IMarketWatchScanner`, `IAgentService`, `IJournalService`, `IAarService`, `IAutonomyService`, `IHermesGatewayService`, `IPersonaContext`, `RemoteCompanionChatService`, COVAS bridge.

---

## 4. What the App Does Now

### Overlay & shell

- Four auto-hiding edge trays (main, top, system monitor, cognition vitals)
- Glass overlay with click-through; configurable opacity and auto-hide delay
- Multiple UI themes / color schemes
- System tray minimize/restore for all major windows
- Window lifecycle via `EventAggregator` + `MainWindow`

### Communication

- Full SMS/MMS chat: conversations, optimistic send, AI auto-reply
- Media: image/video/audio/document attachments with preview and 50 MB limit
- Voice-style calls: STT → message → AI → TTS (not WebRTC)
- Video call window: call state UI, mute/video toggles, TTS greeting — **no real A/V pipeline**
- Message deletion

### AI & agents

- Persona CRUD with LLM parameters, MCP endpoint, shared-information flags
- Model load/pull from Ollama; LM Studio path available
- **Hermes** as primary agent: terminal, browser, skills, House Victoria MCP
- Per-persona Hermes via `AdditionalServers["hermes"]`
- `IAgentService` for deeper agent workflows
- Image generation: A2E cloud or local ComfyUI with custom workflow JSON
- Prompt enhancement for image generation
- COVAS OpenAI-compatible bridge for Elite Dangerous

### Projects & productivity

- Full project board with filters, sorts, phases, roadblocks, artifacts, AI collaboration logs
- Persistent project storage via `PersistentProjectManagementService`
- After Action Reports on milestone completion (accept/reject/reopen)
- Research journals with consolidation and reader UI

### Autonomy

- Background tick loop when enabled and user is idle
- Activities: priority project work, research, art (ComfyUI), reflection, Unreal explore, trading (MT4), goal generation
- Multi-step `AutonomyPlan` persisted across ticks
- Anti-repetition cooldowns, rate limits, outcome evaluation
- Cognition vitals telemetry driving the heart-monitor UI

### Data & memory

- SQLite conversations, contacts, projects, settings
- Data banks with CRUD UI and drag-drop ingestion
- Persistent memory with FTS; optional PgVector hybrid search when Postgres configured
- Ollama embeddings (`/api/embed` with `/api/embeddings` fallback)
- MCP `vector_search` when `PGVECTOR_CONNECTION_STRING` set
- Global log directory with export (TXT/JSON/CSV)
- File generation service for AI outputs

### System monitoring

- CPU/RAM/uptime (500 ms refresh)
- CPU temperature via WMI
- NVIDIA GPU via NVML when available
- Server status and start/stop/restart for: Ollama, MCP, Hermes, TTS, STT, Unreal, ComfyUI
- Circuit breaker for unreachable endpoints
- Virtual Environment Controls window

### Remote companion

- HTTP API on configurable port (default 17890)
- Health, text chat, multipart audio chat
- Bearer token auth (≥ 16 chars)
- Loopback-only or LAN bind
- Optional Unreal notify after each remote reply
- Headless mode for always-on API host
- Android companion app (MVP v2)

### Trading (optional)

- MetaTrader 4 file bridge via `HouseVictoriaBridge.mq4`
- Historical data, market watch scanner, execute/close/verify trades
- MCP tools for agent-driven trading
- Autonomy can scan markets, backtest (demo C# strategy), execute trades
- Market watch project bootstrap

### Settings

- Tabbed UI: Core, Image, Remote, Memory, Autonomy, UI, Advanced
- Per-service connection testing with status indicators
- Import/export/reset JSON settings
- Comprehensive validation on numeric ranges and URLs

---

## 5. Incomplete & Needs Polishing

### High priority gaps

| Area | Current state | What's missing |
|------|---------------|----------------|
| **Real video calling** | `VideoCallWindow` UI + call state + TTS greeting | WebRTC (or SDK) for camera, mic, remote video; signaling; permissions |
| **Unreal end-to-end** | WebSocket client, commands, remote notify JSON | Validated loop with production Unreal build; avatar spawn, lip-sync, scene sync |
| **Semantic memory production** | PgVector + embedding code exists | Operational Postgres+vector extension; consistent embeddings; MCP and app on same store; summarization jobs |
| **Automated testing** | Manual QA scripts (`Verify-*.ps1`) | Unit/integration test suite; CI pipeline |

### Medium priority — works with limits

| Area | Limitation |
|------|------------|
| **GPU monitoring** | Non-NVIDIA GPUs report 0; CPU fan via WMI unreliable |
| **Image generation** | Ollama-native image gen not supported; A2E/ComfyUI edge cases; long ComfyUI polls can timeout |
| **MT4 backtest** | C# placeholder strategy; real logic should live in EA |
| **Hermes security** | Full shell access on localhost — strong API key required |
| **Remote companion ops** | Tunnel runbook exists; live always-on PC (sleep, updates) not handled in UI |
| **Autonomy tuning** | Rate limits and cooldowns need user-facing tuning; some activity kinds can fail silently into backoff |
| **Journal consolidation** | Background sync; large journal sets may need pagination in UI |
| **AAR UX** | Functional but could use richer review UI and notification when pending |
| **Persona editing** | Only system prompt editable in-place; other fields require recreate |
| **Documentation drift** | Older docs (`HouseVictoria_Documentation.md`, `HouseVictoria_Development_Roadmap.md`, `HouseVictoria_Guide.md`) partially superseded by this consolidation |

### Low priority polish

| Area | Note |
|------|------|
| **Installer** | Inno Setup script exists; not fully aligned with `start.bat` service orchestration |
| **Performance** | Large conversation/project/log lists may need virtualization |
| **Error UX** | Some background failures only appear in logs, not toasts |
| **Android app** | No certificate pinning; no video; minimal UI |
| **Multi-user** | Single-user desktop assumption throughout |
| **Non-Windows** | WPF locks platform to Windows |

### Known issues (from agent queue)

- `ISSUE-20260515-001`: Verify script wrong chat payload (QA regression tooling)
- Remote companion QA was blocked when API not running at test time

---

## 6. Potential Future Additions

### Communication & presence

- **WebRTC video calls** — local/remote video, screen share, call recording
- **P5 video downlink** — encode path from Unreal to phone (after text/audio stable)
- **Multi-modal live session** — continuous duplex voice without push-to-record
- **Notification system** — Windows toast when autonomy completes work or AAR pending

### Embodiment & world

- **Validated Unreal loop** — lip-sync, idle animations, emotion hooks on `companion_remote_exchange`
- **Scene management UI** — spawn points, object catalog, live scene info panel
- **VR/MR panel** — Meta Quest or other headset as remote viewport (long-term)

### Intelligence & memory

- **Full semantic memory** — summarization workers, retention policies, cross-persona search in-app
- **Personality evolution policy** — explicit auto-change vs approval-required deltas; weekly digest
- **Multi-agent collaboration** — primary + secondary personas coordinating on shared projects
- **RAG over data banks** — automatic context injection from uploaded files in chat

### Trading & automation

- **Richer MT4/MT5** — natural-language trade commands from chat, risk rules, portfolio dashboard
- **Backtest UX** — in-app strategy editor, equity curves, walk-forward analysis
- **Scheduled autonomy** — cron-style tasks, system-monitor threshold triggers

### Platform & collaboration

- **Shared workspaces** — multi-user projects (requires backend redesign)
- **Cloud sync** — optional backup of settings, conversations, journals
- **Plugin system** — game-specific MCP tool packs (beyond Elite Dangerous)
- **Linux/macOS companion** — remote API client or thin native shell (PC stays Windows)

### Quality & ops

- **CI/CD** — build, test, installer publish on every merge
- **Telemetry dashboard** — autonomy success rates, service uptime, token usage
- **AMD/Intel GPU monitoring** — vendor SDK integration alongside NVML

---

## 7. Strategic Roadmap (Remote Companion & AI Home)

From `Docs/agents/GOALS-Remote-Companion-and-AI-Home.md`:

| Phase | Scope | Status |
|-------|--------|--------|
| **P0 — Remote transport** | HTTP health + text + audio API, Settings, Unreal notify | **Delivered** |
| **P1 — Reliable away-from-desk** | Tunnel runbook, secrets, firewall, failure modes | **Runbook delivered**; live tunnel test ongoing |
| **P2 — Embodiment loop** | Unreal handles remote exchange; animation hooks | **Protocol + mock**; UE project integration product-specific |
| **P3 — Persistent memory at scale** | pgvector + embeddings; same retrieval desktop + remote | **In development** |
| **P4 — Personality evolution** | Auto-change policy vs approval; logging | **Planned** |
| **P5 — Video downlink** | UE → phone video | **Future** |

### Suggested 90-day milestones

1. **M1 — Ops-ready remote** — Stable tunnel, token rotation, phone-network health check
2. **M2 — UE reacts to remote speech** — Minimum animation/emote on `companion_remote_exchange`
3. **M3 — Memory continuity** — Semantic recall same whether message from desktop or phone
4. **M4 — Minimal phone client** — Polish Android MVP or PWA with voice + text

### Architectural guardrails

- Unreal is authoritative for embodied presence
- Phone is thin I/O, not a persona host
- SQLite/filesystem is conversation truth (`conv-{contactId}`)
- TLS + auth at tunnel edge; loopback HTTP on PC
- Controlled personality adaptation only

---

## 8. Repository & Component Map

| Path | Role |
|------|------|
| `HouseVictoria.App/` | WPF UI — trays, windows, converters, styles |
| `HouseVictoria.Services/` | All service implementations |
| `HouseVictoria.Core/` | Interfaces, models, events |
| `MCPServer/` | Python MCP server (memory, tools, MT4) |
| `STTServer/` | faster-whisper transcription host |
| `MT4Bridge/` | MQL4 Expert Advisor + docs |
| `AndroidRemoteCompanion/` | Android MVP client |
| `Unreal/` | UE plugin pack + protocol hints (do not duplicate into UE 5.7 game module) |
| `Installer/` | Inno Setup packaging |
| `Tools/` | Setup scripts (Hermes, persona MCP, unreal mock WS) |
| `scripts/` | PowerShell verification (`Verify-HouseVictoriaStack.ps1`, etc.) |
| `Docs/agents/` | Multi-agent task queue, goals, runbooks (PM/DEV/OPS/QA workflow) |

### Superseded documentation

The following files remain in the repo for historical reference but are **consolidated into this document and the User Guide**:

| Legacy doc | Now covered in |
|------------|----------------|
| `HouseVictoria_Guide.md` | User Guide + this Evolution doc |
| `HouseVictoria_Documentation.md` | §4 What the App Does Now |
| `HouseVictoria_Development_Roadmap.md` | §5–§7 gaps and roadmap |
| `HouseVictoria_MemoryDesign.md` | §5 semantic memory + §6 memory additions |
| `README.md` | User Guide §1–§4 (overview remains in README) |

**Topic-specific docs still valid as deep references:**

- `Hermes_Integration.md` — Hermes setup detail
- `CrossRepo_Integration_Runbook.md` — Unreal + LLMOD startup
- `Unreal_Protocol.md`, `Unreal_ControlScript_Commands.md` — WebSocket wire format
- `COVAS_ELITE_DANGEROUS_SETUP.md` — Elite Dangerous
- `MT4_INTEGRATION_SUMMARY.md`, `MT4Bridge/README.md` — Trading
- `ComfyUI_Custom_Workflow_Guide.md` — Image workflows
- `MCPServer/README.md` — MCP server setup
- `agents/GOALS-Remote-Companion-and-AI-Home.md` — Strategic phases
- `agents/RUNBOOK-Secure-Remote-Companion-Access.md` — Tunnel security

---

## 9. Maintaining This Documentation

When shipping features or changing status:

1. Update **§4** (current capabilities) and **§5** (gaps) in this file first.
2. Update the **User Guide** for any user-visible workflow change.
3. Update `README.md` one-paragraph summary if scope changes.
4. Keep topic-specific runbooks (Hermes, Unreal, COVAS, MT4) in sync with Settings UI.

**Source of truth priority:** Implementation in code → this Evolution doc → User Guide → legacy docs.

---

## Summary

House Victoria evolved from a **Game Bar-style AI overlay** into a **home AI orchestration platform**: chat, projects, autonomy, journals, trading, remote phone access, and optional Unreal embodiment. The **core desktop experience is mature**; the largest gaps are **real video**, **production Unreal validation**, **operational semantic memory**, and **automated test coverage**. The strategic north star remains **PC-as-brain, Unreal-as-body, phone-as-remote**, with video and deep personality evolution explicitly deferred until text/audio and memory are reliable.

---

*End of evolution document.*
