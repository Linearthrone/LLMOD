---
type: qa_report
report: QA-REPORT-20260420-remote-companion
task_ref: TASK-20260420-003-PM01-to-QA01
project: House Victoria
date: 2026-04-20
tester: QA-01 (assistant)
environment: Windows, local loopback — API not running
---

# Remote companion API — regression & security smoke

## Scope

Endpoints under test (from task):

- `GET /api/remote/v1/health`
- `POST /api/remote/v1/chat` — JSON `{ "message", "contactId?" }`, auth `Authorization: Bearer <token>` or `X-Api-Key`
- `POST /api/remote/v1/chat-audio` — multipart field `audio`, optional `contactId`

## Preconditions (not met this run)

House Victoria exposes this API only when:

- `RemoteCompanionEnabled` is true,
- `RemoteCompanionApiToken` is **≥ 16 characters**,
- app is running (`RemoteCompanionWebHost`).

Current workspace default in `HouseVictoria.App/App.config`: `RemoteCompanionEnabled=false`, empty token — Kestrel remote host does not start unless changed in Settings/App.config and app restarted.

## Execution evidence (PowerShell)

Run from repo machine:

```text
--- Tc0: port 17890
TcpTestSucceeded: False (127.0.0.1:17890)

--- Case1: GET /api/remote/v1/health
Unable to connect to the remote server

--- Case2: POST /api/remote/v1/chat (no auth)
Unable to connect to the remote server

--- Case3: wrong token
Unable to connect to the remote server

--- Case6: chat-audio wrong content-type / no multipart
Unable to connect to the remote server
```

Commands used: `Test-NetConnection -ComputerName 127.0.0.1 -Port 17890`; `Invoke-WebRequest` to `http://127.0.0.1:17890/api/remote/v1/...`.

## Matrix (minimum)

| # | Case | Expected | Result | Notes |
|---|------|-----------|--------|-------|
| 1 | Health without auth | 200, `ok: true` | **Blocked** | No listener on 17890 — cannot observe HTTP status/body |
| 2 | Chat without auth | 401 | **Blocked** | Same |
| 3 | Chat wrong token | 401 | **Blocked** | Same |
| 4 | Chat valid token, short message | 200, `reply` present | **Blocked** | Same |
| 5 | Same conversation id (`conv-{contactId}` for resolved AI contact) | Matches service behavior | **Blocked** | Implementation uses `conversationId = $"conv-{contact.Id}"` in `RemoteCompanionChatService.ChatAsync` — runtime check pending |
| 6 | Audio empty/missing field | 400 | **Blocked** | Same |
| 7 | (If STT available) small wav/webm upload | 200 or clear STT error | **Not run** | Depends on STT endpoint + API up |

## Summary

- **Pass/Fail**: No row could be marked **Pass** — remote API was **not reachable** on this machine at test time.
- **Follow-up**: Enable remote companion with a **test-only** ≥16-char token, restart House Victoria, re-run same PowerShell probes; redact tokens in reports.

## Sign-off

Regression smoke **not completed** — environment prerequisite (listening API) absent. Recommend re-test after OPS/DEV confirms local API availability.
