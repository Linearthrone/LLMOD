# House Victoria — Complete User Guide

**Version:** 2.0  
**Last updated:** June 2026  

This is the **single user-facing guide** for House Victoria. For how the project evolved, what is incomplete, and planned future work, see **[HouseVictoria_Evolution.md](HouseVictoria_Evolution.md)**.

---

## Table of Contents

1. [What House Victoria Is](#1-what-house-victoria-is)
2. [Prerequisites](#2-prerequisites)
3. [Installation & First Launch](#3-installation--first-launch)
4. [Daily Startup](#4-daily-startup)
5. [The Overlay Interface](#5-the-overlay-interface)
6. [SMS/MMS Chat](#6-smsmms-chat)
7. [AI Models & Personas](#7-ai-models--personas)
8. [Projects & Goals](#8-projects--goals)
9. [Journals & After Action Reports](#9-journals--after-action-reports)
10. [Data Banks & File Handling](#10-data-banks--file-handling)
11. [Autonomy & Cognition Vitals](#11-autonomy--cognition-vitals)
12. [Settings Reference](#12-settings-reference)
13. [Integrations](#13-integrations)
14. [Troubleshooting & FAQ](#14-troubleshooting--faq)

---

## 1. What House Victoria Is

House Victoria is a **Windows desktop overlay** (WPF, .NET 8) inspired by Xbox Game Bar. It provides:

- **SMS/MMS-style AI chat** with personas, media attachments, and voice-style calls
- **Project management** with roadblocks, artifacts, and AI collaboration logs
- **Research journals** and **After Action Reports** when projects complete
- **Background autonomy** — the AI can work on projects, research, art, and trading while you are away
- **System monitoring** with server management (Ollama, MCP, Hermes, TTS, STT, Unreal, ComfyUI)
- **Cognition vitals** — a heart-monitor-style UI showing what the AI is doing
- **Optional integrations:** Unreal Engine avatar, remote phone companion, Elite Dangerous (COVAS), MetaTrader 4, image generation

**Architecture in one sentence:** The PC is the brain (LLMs, MCP, SQLite, optional Postgres vectors); Unreal is the optional embodied home; the phone is thin remote I/O when you are away.

---

## 2. Prerequisites

| Requirement | Purpose | Default |
|-------------|---------|---------|
| **Windows 10/11** | Host OS | — |
| **.NET 8.0 Runtime** | Run the app | Install from Microsoft |
| **Ollama** | Local LLM backend | `http://localhost:11434` |
| **Python MCP server** | Agent tools, memory, MT4 bridge tools | `http://localhost:8080` |
| **Hermes Agent** (recommended) | Tool-loop agent with terminal/browser/MCP | `http://127.0.0.1:8642/v1` |

**Optional services:**

| Service | Purpose | Default |
|---------|---------|---------|
| Piper / Kokoro TTS | Spoken AI replies | `http://localhost:5000` |
| STT server (faster-whisper) | Voice transcription | `http://localhost:8000/transcribe` |
| ComfyUI / Stability Matrix | Local image generation | `http://localhost:8188` |
| A2E API | Cloud image generation | Token in Settings |
| Unreal Engine | 3D avatar / world | `ws://localhost:8888` |
| Postgres + pgvector | Semantic memory (advanced) | Connection string in Settings |
| MetaTrader 4 | AI-assisted trading | File bridge via `MT4DataPath` |

---

## 3. Installation & First Launch

### One-time setup

From the repository root:

```bat
install.bat
```

This restores NuGet packages, builds the solution, and sets up the MCP Python virtual environment, STT, and TTS dependencies.

### Hermes integration (recommended)

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\setup-hermes-integration.ps1
```

This installs Hermes (if missing), configures the gateway API key, registers House Victoria MCP (including MT4 tools), and sets `PrimaryLLM=hermes` in `App.config`.

### First launch checklist

1. Ensure **Ollama** is installed and at least one model is pulled (e.g. `ollama pull llama3.2`).
2. Run **`start.bat`** (starts Ollama, MCP, Hermes gateway, TTS, STT, and the app).
3. Open **Settings** and use **Test** buttons for each service until status shows connected.
4. Create your first **AI persona** in AI Models & Personas.
5. Open **SMS/MMS** and start a conversation.

### Windows installer (optional)

See `Installer/README.md` for building `HouseVictoriaSetup.exe` with Inno Setup.

---

## 4. Daily Startup

```bat
start.bat
```

`start.bat` starts services based on your Settings (primary LLM, auto-start flags) and launches House Victoria.

**Remote-only mode** (API host without UI):

```bat
set HV_REMOTE_COMPANION_ONLY=1
HouseVictoria.App.exe --remote-only
```

Both the environment variable **and** `--remote-only` are required.

**Headless validation scripts** (from repo root):

```powershell
.\scripts\Verify-HouseVictoriaStack.ps1
.\scripts\Verify-CrossRepoIntegration.ps1
```

---

## 5. The Overlay Interface

House Victoria uses **four collapsible edge trays** plus popup windows. Trays auto-hide when configured; hover the screen edge to reveal them.

### Main Tray (bottom-right)

| Button | Opens |
|--------|-------|
| SMS/MMS | Chat window |
| AI Models | Personas & model management |
| Settings | Full configuration |

Windows opened from here can minimize to the system tray. Right-click the tray icon to restore.

### Top Tray (top edge)

| Control | Action |
|---------|--------|
| **Drag-and-drop** | Upload files into a "Dropped Files" data bank |
| **Generated Files** | Open folder of AI-generated outputs |
| **Global Log Directory** | Categorized application logs |
| **Projects/Goals** | Project board |
| **Data Bank Management** | CRUD for data banks and entries |
| **Journals** | Research journal library |
| **After Action Reports** | Review completed project reports |

**Supported drag-drop types:** `.txt`, `.md`, `.json`, `.xml`, `.csv`, `.log`, `.cs`, `.js`, `.py`, `.html`, `.css`, and any binary (metadata stored).

### System Monitor Drawer (left edge)

- **CPU / RAM / uptime** — real-time (500 ms refresh)
- **CPU temperature** — via WMI
- **GPU usage / temp / fan** — via NVIDIA NVML when available; otherwise 0
- **AI status** — primary persona, active contact, virtual environment connection
- **Server list** — Ollama, MCP, Hermes, TTS, STT, Unreal, ComfyUI with start/stop/restart
- **Virtual Environment Controls** — scene/avatar commands when Unreal is connected

### Cognition Vitals Drawer (upper-left)

A **heart-monitor-style** display of the AI's cognitive state:

- **Rhythm** — Resting, Waiting, Reflecting, Research, Creative Calm, Project Work, Priority Urgent, Trading Active, Environment
- **BPM & waveform** — visual intensity of current activity
- **Autonomy status** — whether the background loop is running and what it last did

Visible when **Settings → Autonomy → Enable Autonomy** is on.

---

## 6. SMS/MMS Chat

### Layout

- **Left:** Conversation list (sorted by last message)
- **Center:** Messages (last 100 per conversation)
- **Right:** Contact picker (human and AI contacts)
- **Bottom:** Text input + attachment button

### Sending messages

- **Enter** — send
- **Shift+Enter** — new line
- **Attachment (📎)** — images, video, audio, documents (max **50 MB**)

**Supported media:** jpg, jpeg, png, gif, bmp, mp4, avi, mov, wmv, mp3, wav, ogg, pdf, doc, docx, txt.

Media is stored under `Data/Media/{ConversationId}/`. Click a preview to open the full file.

### AI responses

- Default timeout: **5 minutes** (configurable per persona via Max Tokens / context)
- Timeout errors include suggestions (reduce tokens, try a faster model)
- Personas load automatically when you select an AI contact

### Voice-style calls (not full video)

**Prerequisites:** STT on port 8000, TTS on port 5000 (or Windows TTS fallback).

1. Tap the **green phone** button on an AI contact.
2. When connected, use **Record** → speak → **Stop** to transcribe and send.
3. The AI reply is generated and played via TTS.

**Note:** `VideoCallWindow` exists for call state UI but **real WebRTC video/audio is not implemented**. See Evolution doc for status.

### Message management

- Themes and bubble styling are configurable
- Message deletion is supported (per conversation)

---

## 7. AI Models & Personas

### Creating a persona

1. Click **Create Persona**.
2. Set **Name**, **Model** (from Ollama), **System Prompt**, **Description**.
3. Configure **LLM parameters** (optional):
   - Temperature (0–2, default 0.7)
   - TopP, TopK, Repeat Penalty
   - Max Tokens (-1 = unlimited)
   - Context Length (default 4096)
4. **MCP Server endpoint** — default `http://localhost:8080` for agent tools.
5. **Shared information** checkboxes — control what house-level context (journals, data banks, etc.) the persona can access.
6. **Hermes per-persona** — set `AdditionalServers["hermes"] = "true"` for tool-loop chat on one persona while others use Ollama.

### Model management

- **Load Model** — load into Ollama memory
- **Pull Model** — download from Ollama hub (30-minute timeout for large models)
- **Edit** — change system prompt only
- **Delete** — removes persona; conversation history is preserved

### Primary & secondary personas

Settings and `IPersonaContext` track a **primary** and optional **secondary** AI. COVAS and remote companion can target a specific contact ID or fall back to primary.

### Image generation (AI Models window)

When ComfyUI or A2E is configured:

- Enter a prompt, generate, preview, save to generated files folder
- Provider: **A2E** (cloud, token required) or **ComfyUI** (local)
- Custom ComfyUI workflows supported via placeholders — see Settings → Image

---

## 8. Projects & Goals

### Creating a project

Fields: Name, Type (Development, Research, Personal, Business, Other), Description, Priority (1–10), Start/Deadline dates, Phase, Assigned AI contact, Initial roadblocks.

### Project list

- Card view with priority color, type/phase badges, deadline, completion %, roadblock count
- **Filter** by type and phase
- **Sort** by name, priority, deadline, completion

### Project detail dialog (tabs)

| Tab | Features |
|-----|----------|
| **Overview** | Edit details, phase dropdown, timeline, assigned AI, statistics |
| **Roadblocks** | Add/remove obstacles |
| **Artifacts** | Upload, preview, download, delete files |
| **AI Logs** | Timeline of AI actions; filter by contact; search |

### Phases

Planning → InProgress → Review → Completed (also OnHold, Cancelled).

Completing a project can trigger an **After Action Report** (see below).

---

## 9. Journals & After Action Reports

### Journals

Opened from **Top Tray → Journals**.

- Research journals created by autonomy, agents, or manual flows
- Consolidated from autonomy `journal.jsonl` into `journals.json`
- **Journal Reader** window for full-text reading
- Personas can share house journals via **Share House Journals** checkbox

### After Action Reports (AAR)

Opened from **Top Tray → After Action Reports**.

When a project reaches a completion milestone:

1. The app generates an AI-written **After Action Report**
2. You **Accept** (rewards persona / logs success) or **Reject** (reopens project with feedback)
3. Reports persist under the autonomy data path (`aar.json`)

---

## 10. Data Banks & File Handling

### Drag-and-drop (Top Tray)

Files land in a **Dropped Files** data bank automatically. Text files are indexed; binaries store metadata.

### Data Bank Management

- Create, rename, delete banks
- Add, edit, remove entries
- Search and filter
- Used by MCP tools and persona context

### Generated files

AI outputs (art, research, code, autonomy artifacts) go to the configured generated-files folder. Open via Top Tray → Generated Files.

### Global Log Directory

- Tree of categories with unread badges
- Export as TXT, JSON, or CSV
- Auto-mark read on selection

---

## 11. Autonomy & Cognition Vitals

When **Enable Autonomy** is on, a background loop runs on a configurable interval. The AI may:

| Activity | Description |
|----------|-------------|
| Work on priority projects | Advance high-priority goals with multi-step plans |
| Write research | Journal entries and research documents |
| Create art | ComfyUI image generation (rate-limited) |
| Reflect | Quiet cognitive processing |
| Explore environment | Unreal commands when connected |
| Trading | Scan markets, run backtests, execute trades (when MT4 configured) |
| Generate goals | Propose new projects when idle |

### Settings (Settings → Autonomy)

| Setting | Purpose |
|---------|---------|
| Enable Autonomy | Master switch |
| Tick interval | Seconds between autonomy ticks (30–600) |
| Min idle minutes | Wait after your last chat before acting |
| High priority threshold | Project priority level to prefer |
| Max actions per hour | Rate limit |
| Max art per hour | ComfyUI rate limit |
| Enable art generation | Allow autonomous image creation |
| Autonomy AI contact | Which persona performs background work |

### Cognition vitals UI

The upper-left drawer shows real-time rhythm, BPM, and last activity summary. Trading activity switches rhythm to **Trading Active** when MT4 is connected.

---

## 12. Settings Reference

Settings are organized into tabs: **All**, **Core**, **Image**, **Remote**, **Memory**, **Autonomy**, **UI**, **Advanced**.

### Core

| Area | Key settings |
|------|--------------|
| **LLM servers** | Primary LLM: `ollama`, `hermes`, or `lmstudio`; endpoints; auto-start; test buttons |
| **Hermes Agent** | API endpoint, API key, model id, auto-start gateway |
| **MCP Server** | `http://localhost:8080` |
| **TTS** | Endpoint, voice, test |
| **STT** | Transcription endpoint |
| **Virtual Environment** | Unreal WebSocket URL |
| **COVAS bridge** | Elite Dangerous OpenAI-compatible bridge |
| **Overlay** | Enable, opacity, auto-hide delay |

### Image

| Setting | Purpose |
|---------|---------|
| Image provider | `a2e` (cloud) or `comfyui` (local) |
| A2E API token | Bearer token from video.a2e.ai |
| ComfyUI endpoint | Default `http://localhost:8188` |
| Stability Matrix path | Launch local ComfyUI stack |
| Custom workflow JSON | Placeholders: `{{positive}}`, `{{negative}}`, `{{width}}`, `{{height}}`, `{{seed}}`, `{{filename_prefix}}` |
| Preferred checkpoint | e.g. `sd_xl_base_1.0.safetensors` |

### Remote

| Setting | Purpose |
|---------|---------|
| Remote Companion Enabled | HTTP API on PC |
| Listen port | Default **17890** |
| API token | **≥ 16 characters** (Bearer auth) |
| AI contact ID | Default persona for remote chat |
| Listen on LAN | `false` = loopback only (use with tunnel) |
| Notify Unreal | Send `companion_remote_exchange` after remote replies |

### Memory

| Setting | Purpose |
|---------|---------|
| Enable memory | Persistent conversation memory |
| Memory path, max entries, importance, retention | SQLite + FTS |
| Semantic memory | Postgres connection, embedding model, dimensions |
| Enable PgVector | Hybrid lexical + vector search |

### UI

- Color scheme / theme selection (multiple themes)
- Avatar and locomotion parameters (for Unreal)
- Tools permissions (filesystem, network, system commands)

### Import / export

- **Export Settings** → JSON backup
- **Import Settings** → restore
- **Reset to Defaults** → confirmation required

All settings persist to `App.config` (or the `.dll.config` beside the running executable).

---

## 13. Integrations

### Hermes Agent (primary LLM path)

Chat routes through Hermes when `PrimaryLLM=hermes`. Hermes runs the tool loop (terminal, browser, MCP). House Victoria keeps personas, UI, SQLite history, and Unreal hooks.

Setup: `Tools/setup-hermes-integration.ps1`  
Docs detail: see Evolution doc § Hermes.

### MCP Server (Python)

Provides memory tools, web/system tools, and **MT4 bridge tools** (`mt4_status`, `mt4_execute_trade`, etc.).

```bash
cd MCPServer
pip install -e .
python -m house_victoria_mcp
```

Environment: `PGVECTOR_CONNECTION_STRING`, `OLLAMA_HOST`, `OLLAMA_EMBEDDING_MODEL` for vector search.

### Remote Companion + Android app

**PC:** Enable in Settings → Remote. API:

- `GET /api/remote/v1/health`
- `POST /api/remote/v1/chat` (Bearer token)
- `POST /api/remote/v1/chat-audio` (multipart `audio` field)

**Phone:** Build `AndroidRemoteCompanion/` in Android Studio. Configure base URL (tunnel or Tailscale IP) and token.

**Security:** Prefer loopback + **Tailscale** or **Cloudflare Tunnel**. Do not port-forward WAN → 17890 without TLS and auth at the edge.

### Unreal Engine

- App is WebSocket **client** → Unreal **server** on `ws://localhost:8888`
- Start Unreal first, then House Victoria
- Commands: plain text (`move_avatar`, `status`) and JSON (`companion_remote_exchange`)
- Cross-repo runbook: `Docs/CrossRepo_Integration_Runbook.md`

### COVAS: Next (Elite Dangerous)

Enable `CovasBridgeEnabled` in `App.config`. Point COVAS API base to `http://localhost:11435`. Your ship-computer persona becomes the in-game AI voice.

### MetaTrader 4

1. Set `MT4DataPath` in `App.config`
2. Attach `HouseVictoriaBridge.mq4` in MT4 with AutoTrading on
3. MCP tools and autonomy can scan markets, backtest, and trade

### Image generation

- **A2E** — cloud, set token in Settings
- **ComfyUI** — local via Stability Matrix or standalone; custom workflows supported
- Autonomy can generate art when enabled and rate limits allow

---

## 14. Troubleshooting & FAQ

### AI responses slow or timing out

- Lower **Max Tokens** and **Context Length** on the persona
- Use a smaller/faster Ollama model
- Check Ollama and Hermes gateway logs (`Media\hermes-gateway.log`)
- Hermes tool runs can take up to 15 minutes

### Service connection tests fail

1. Confirm the service is running (`start.bat` or System Monitor → Start)
2. Verify endpoint URLs in Settings
3. Check Windows Firewall (especially if LAN listen is enabled for remote API)
4. Restart the service and retest

### GPU metrics show 0%

Expected on non-NVIDIA GPUs. NVIDIA systems need `nvml.dll` (NVIDIA drivers). CPU/RAM metrics always work via Performance Counters.

### Voice transcription fails

- Run `start.bat` so STT starts on port 8000
- Test STT endpoint in Settings
- Optional: set `OPENAI_API_KEY` for Whisper cloud fallback

### Remote companion unauthorized

- Token must match Settings exactly (≥ 16 chars)
- Include `Authorization: Bearer <token>` header
- Health endpoint does not require auth; chat endpoints do

### Unreal not connecting

1. Start Unreal project first (WebSocket server on 8888)
2. Match endpoint in Settings
3. Run `Verify-CrossRepoIntegration.ps1`
4. Check Global Log Directory for WebSocket errors

### Image generation fails

- **A2E:** verify token and provider setting
- **ComfyUI:** ensure server is running, checkpoint exists, workflow JSON is valid API format
- Check Settings → Image → Test connection

### Where is data stored?

| Data | Location |
|------|----------|
| Conversations | SQLite database in app data directory |
| Media | `Data/Media/{ConversationId}/` |
| Autonomy / journals / AAR | `AutonomyDataPath` (configurable) |
| Generated files | Configured generated-files folder |
| Logs | Serilog + Global Log Directory |

### FAQ

**Q: Do I need Ollama if I use Hermes?**  
A: Yes — Hermes typically uses Ollama (or another provider) as its LLM backend.

**Q: Can I use House Victoria without Unreal?**  
A: Yes. Unreal, remote companion, COVAS, and MT4 are all optional.

**Q: Is the phone app a full copy of the desktop?**  
A: No. MVP v2 supports text + audio chat only. Video from Unreal to phone is planned for later.

**Q: How do I back up everything?**  
A: Export Settings JSON; back up the SQLite database and autonomy data folder.

---

*For project history, technical inventory, gaps, and roadmap, see [HouseVictoria_Evolution.md](HouseVictoria_Evolution.md).*
