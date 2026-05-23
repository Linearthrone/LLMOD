# Cross-Repo Integration Runbook (LLMOD + Unreal HouseVictoria)

This runbook defines the canonical way to start and validate integration between:

- LLMOD app repo: `C:\Users\kurtw\LLMOD\LLMOD-max-master`
- Unreal repo: `C:\Users\kurtw\OneDrive\Documents\Unreal Projects\HouseVictoria`

**UE 5.7.x integration note:** The HouseVictoria Unreal project already implements WebSocket server + message parser in the **game module** (`Source/HouseVictoria/`). Do **not** copy `LLMOD-max-master/Unreal/Plugins/HouseVictoriaBridge` into that project's `Plugins/` folder — it causes version/load failures. See `HouseVictoria/Docs/LEXIE_SETUP.md`.

## 1) Canonical startup order

Use server-first startup for deterministic behavior:

1. Start Unreal `HouseVictoria` project (WebSocket server).
2. Confirm `ws://127.0.0.1:8888` is listening.
3. Start LLMOD stack (`start.bat` or app + services).
4. Run health and roundtrip checks.

Why this order:

- Unreal side is the WebSocket server (`WebSocketNetworking` + subsystem) and LLMOD is the client.
- Starting LLMOD first can still work via reconnect, but it is race-prone and bounded by retry windows.

## 2) Startup commands

### Unreal server

Option A (Editor):

```powershell
Start-Process "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\HouseVictoria\HouseVictoria.uproject"
```

Then run PIE/Standalone so the WebSocket subsystem starts.

Option B (existing local flow):

- Open project and run the same map/game mode used in your current Lexie setup (`/Game/Main`, `HouseVictoriaGameModeBase`).

### LLMOD stack

From repo root:

```powershell
.\start.bat
```

This starts key services (when available): Ollama, MCP server, Kokoro/Piper TTS, STT, optional ComfyUI, then launches House Victoria app.

## 3) Config contract matrix (source of truth)

| Contract | Default | Owner file | Notes |
| --- | --- | --- | --- |
| Unreal WebSocket endpoint | `ws://localhost:8888` | `HouseVictoria.App/App.config` (`UnrealEngineEndpoint`) | LLMOD client target |
| Unreal WebSocket bind/port | `0.0.0.0:8888` (Lexie setup default) | `HouseVictoria/Docs/LEXIE_SETUP.md` and Unreal project settings | Must match app endpoint |
| Remote Companion API | `http://127.0.0.1:17890` | `HouseVictoria.App/App.config` (`RemoteCompanionListenPort`, `RemoteCompanionListenOnLan`) | Hosted by app (`RemoteCompanionWebHost`) |
| Companion auth token | configured in app config | `HouseVictoria.App/App.config` (`RemoteCompanionApiToken`) | Required (>=16 chars) |
| Image endpoint (optional lane) | `http://localhost:8188` | `HouseVictoria.App/App.config` (`StableDiffusionEndpoint`) | Used by QA smoke script |
| Unreal command protocol | plain text + JSON | `Docs/Unreal_ControlScript_Commands.md` | Authoritative wire verbs/payload |

## 4) Validation workflow (required lanes)

## Lane A: service health

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-HouseVictoriaStack.ps1
```

Pass criteria:

- `remote-health` returns 200 + `ok:true`
- short-token check returns 401 unauthorized
- valid token request does not fail with socket/connect errors

Evidence:

- `tmpcode/qa-stack-evidence.txt`

## Lane B: chat roundtrip

Covered by the same script above (`remote-chat-valid-token`).

Pass criteria:

- returns HTTP success or clear downstream app error
- not `Unable to connect to the remote server`

## Lane C: Unreal command/status loop

From Unreal repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\lexie_acceptance_test.ps1
```

Pass criteria:

- script connects to `ws://127.0.0.1:8888`
- sends `status`, movement/animation verbs, JSON `companion_remote_exchange`, and `get_avatar_state`
- Unreal logs reflect expected state transitions / command handling

## 5) One-command orchestrated validation (recommended)

From LLMOD repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-CrossRepoIntegration.ps1 -UnrealProjectRoot "C:\Users\kurtw\OneDrive\Documents\Unreal Projects\HouseVictoria"
```

Stack-only (no Unreal project path required; skips `:8888` wait and Lexie script):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-CrossRepoIntegration.ps1 -SkipUnrealLanes
```

This wrapper:

- runs Lane A/B (`Verify-HouseVictoriaStack.ps1`) and treats `[remote-health] FAIL`, `[remote-chat-short-token]` FAIL/UNEXPECTED, and `[remote-chat-valid-token] FAIL` in the latest `tmpcode/qa-stack-evidence.txt` block as a non-zero exit
- waits for Unreal WebSocket readiness (unless `-SkipUnrealLanes`)
- runs Lane C (`lexie_acceptance_test.ps1`) when not skipped
- appends evidence to `tmpcode/cross-repo-integration-evidence.txt`

## 6) Recovery playbook

If LLMOD starts first:

1. Start Unreal project.
2. Wait for socket on `8888`.
3. Re-run `Verify-CrossRepoIntegration.ps1`.
4. If needed, toggle reconnect path by reconnecting Unreal endpoint in app settings or restarting app.

If Unreal loop fails but health/chat pass:

1. Confirm Unreal is actively running gameplay session (not just idle editor).
2. Confirm `WebSocketNetworking` plugin/subsystem is active and bound to `8888`.
3. Re-run Unreal acceptance script and inspect project logs for rejected/unknown verbs.

## 7) Operator checklist (PM/OPS/QA)

- PM: confirm both repos point to same endpoint/port contract before tasking.
- OPS: execute startup order and run one-command orchestrated validation.
- QA: attach latest evidence blocks from both evidence files to report.
- All: treat startup order deviations as non-deterministic and re-run canonical sequence before triage.
