# Team goals · Remote companion & AI-at-home (House Victoria)

**Owner narrative:** Primary user is the **product owner**. The assistant’s **presence** lives in **Unreal Engine** (avatar + world). The **PC** is the **brain**: LLMs, MCP, SQLite history, optional pgvector memory. The **phone** is **thin comms only** while away—no second “home” for the persona on-device in v1. **Video from Unreal to phone** is explicitly **later**; **audio + text first**.

This document turns that direction into **phased goals** and links to task files under `Docs/agents/tasks/` (closed pairs move to `Docs/agents/log/`).

---

## Architectural guardrails (non-negotiable)

| Principle | Implication |
|-----------|----------------|
| Unreal is the embodied home | Avatar/world state is authoritative for “where she is.” PC orchestrates commands; UE implements locomotion, animation, scene events. |
| Phone is remote I/O | Same auth/session model as desktop; no duplicate persona host on the phone for MVP. |
| Filesystem / SQLite truth | Conversation history uses `conv-{contactId}`; remote API writes the same messages as the SMS-style UI. |
| Security | Local HTTP API is **not** internet-safe alone; **TLS and auth terminate at the tunnel** (Tailscale, Cloudflare Tunnel, etc.). Token ≥ 16 chars; prefer **loopback + tunnel** over wide LAN exposure. |
| Personality “evolution” | **Controlled adaptation** only: memory + reviewed persona deltas—not silent unbounded drift. |

---

## Phase status

| Phase | Scope | Status |
|-------|--------|--------|
| **P0 — Remote transport (MVP)** | HTTP health + text chat + multipart audio (`/api/remote/v1/*`), Settings + `App.config`, optional Unreal notify JSON | **Delivered in codebase** (restart app after changing settings). |
| **P1 — Reliable “away from desk”** | Tunnel runbook, secrets, firewall, monitoring `/health`, document failure modes (PC sleep, UE crash) | **Runbook delivered** — see `RUNBOOK-Secure-Remote-Companion-Access.md`; live tunnel test when app running |
| **P2 — Embodiment loop** | Unreal handles `companion_remote_exchange` (or equivalent), lip-sync / idle / emotion hooks as needed | **Protocol + mock + HV notify** — see `Unreal_Protocol.md`, `Tools/unreal_mock_ws.py`; UE project integration still product-specific |
| **P3 — Persistent memory at scale** | pgvector + embedding pipeline aligned with `HouseVictoria_MemoryDesign.md`; remote + desktop same retrieval path | **DEV** |
| **P4 — Personality evolution policy** | Explicit “what may auto-change” vs “requires approval”; logging + optional weekly digest | **PM + DEV** |
| **P5 — Video downlink** | Encode path from UE or compositor, WebRTC or other—**after** text/audio stable | **Future** |

---

## Milestones (90-day horizon, suggested)

1. **M1 — Ops-ready remote path**  
   Stable tunnel to loopback listener; token rotation documented; quick health check from phone network.

2. **M2 — UE reacts to remote speech**  
   At minimum: receive `companion_remote_exchange` (user + assistant text); optional animation/emote hooks documented in `Docs/Unreal_Protocol.md`.

3. **M3 — Memory continuity**  
   Semantic search / recall works for the same contact whether messages came from desktop or remote (verify end-to-end).

4. **M4 — Minimal phone client**  
   One screen: text send + optional voice record upload to `chat-audio`; display reply. (Can be web PWA or native—team choice.)

---

## Roles & handoffs

| Role | Focus |
|------|--------|
| **PM** | Priority order (M1→M4), persona/evolution policy, acceptance criteria. |
| **DEV** | House Victoria + UE protocol + memory integration + phone client when scheduled. |
| **OPS** | Tunnel, firewall, uptime, secrets, deploy/runbook for “always-on” PC caveats. |
| **QA** | API regression, STT path, auth negative tests, UE mock / integration checklist. |

Task queue state:

- **Archived** (`log/`): 20260420-001 (DEV), 20260420-002 (OPS).  
- **Blocked**: `TASK-20260420-003-PM01-to-QA01.md` (API not running at QA time).  
- **Active now (2026-04-21):**
  - `TASK-20260421-004-PM01-to-DEV01.md` (Android v2: audio + reliability)
  - `TASK-20260421-005-PM01-to-OPS01.md` (live endpoint lane + runbook delta)
  - `TASK-20260421-006-PM01-to-QA01.md` (API rerun + Android smoke)

---

## Risks to track

- **PC asleep / updates** → remote unavailable; set expectations in UI/docs.  
- **UE + WebSocket stability** → reconnect policy already in app; extend for production noise.  
- **Scope creep on phone** → keep MVP to chat + audio until video is explicitly scheduled.

---

*Last updated: aligned with repository state and remote companion implementation.*
