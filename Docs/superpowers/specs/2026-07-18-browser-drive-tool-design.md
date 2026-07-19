# Browser Drive Tool — Design Spec

**Date:** 2026-07-18  
**Status:** Approved for planning  
**Goal:** Give House Victoria mouse and keyboard control of the **same Chrome/Edge tab** she captures with the Browser Capture extension, without relying on OS-level `computer_use` (incompatible with the Topmost WPF overlay).

---

## Problem

- `browser_capture_tab` already screenshots the active tab and returns a `page_map` (in-tab, overlay-safe).
- Clicks/typing still go through external `computer_use` (OS input + desktop screenshots).
- House Victoria’s full-screen Topmost overlay pollutes desktop capture and makes OS mouse targeting unreliable for browser work.
- Hermes `browser` / Puppeteer paths are intentionally disabled (separate “ghost” Chromium ≠ user’s session).

## Decisions (from design review)

| Topic | Choice |
|-------|--------|
| Capability | **C** — Element actions first; coordinate + key fidelity as fallback |
| Availability | **B** — Available whenever bridge + extension are healthy; **not** gated on `AllowComputerControl` |
| Debugger banner | **A** — Accept Chrome’s debugging banner for coordinate/key path; element path stays banner-free |
| Architecture | **1** — Extend existing capture stack (bridge job queue + same extension) |

---

## Architecture

```
Victoria (Hermes)
    → house_victoria MCP tools (browser_*)
        → BrowserCaptureBridge :17891
            POST /action  (job queue, sibling to /capture)
                → Chrome MV3 extension (same as capture)
                    ├─ element path: chrome.scripting.executeScript
                    └─ coord/key path: chrome.debugger → CDP Input.*
                        → active tab (same profile/cookies as capture)
```

- Capture (`/capture`, cast WebSocket, page_map) remains unchanged in role.
- Drive is a **sibling job kind** on the same poll loop — not a second extension, not OS mouse.
- Desktop `computer_use` remains for **non-browser** desktop control only.
- Target tab (v1): **active tab** in the last focused Chrome/Edge window (same as capture). No multi-tab targeting.

---

## MCP tool surface

Existing (unchanged role):

- `browser_capture_tab` — screenshot + page_map
- `browser_bridge_health` — bridge/extension health

New drive tools on `house_victoria` (separate verbs for clear Hermes selection):

| Tool | Purpose |
|------|---------|
| `browser_click` | Click by `selector` or page_map `index`, **or** viewport `x,y` (+ optional button) |
| `browser_type` | Type text into target element (`selector`/`index`) or current focus; optional `clear` |
| `browser_key` | Key / combo (`Enter`, `Tab`, `Ctrl+A`, etc.) via CDP |
| `browser_scroll` | Scroll by `delta_x`/`delta_y` or toward an element |

**Steered usage pattern:**

1. `browser_capture_tab` → read `page_map`
2. Prefer `browser_click` / `browser_type` with selector or index
3. Fall back to `x,y` / `browser_key` when map miss or canvas-like UI
4. Recapture after meaningful actions to verify

**Catalog / Hermes routing:** For browser-page interact tasks, steer to these tools. Stop instructing use of `computer_use` clicks on `page_map` centers. Do not require `AllowComputerControl` for browser drive tools.

---

## Bridge action protocol

Mirror the capture job queue:

1. MCP → `POST /action` with `{ action, ...params, timeout_seconds }`
2. Bridge enqueues a job (`kind: "action"`) alongside capture jobs
3. Extension `GET /poll` returns action fields when claimed
4. Extension executes, then `POST /result` with `{ ok, error?, detail? }`
5. Bridge waits (same timeout pattern as `/capture`) and returns result to MCP

### Example payloads

```json
{ "action": "click", "selector": "#submit" }
{ "action": "click", "index": 3 }
{ "action": "click", "x": 420, "y": 180, "button": "left" }
{ "action": "type", "text": "hello", "selector": "input[name=q]", "clear": true }
{ "action": "key", "key": "Enter" }
{ "action": "key", "key": "a", "modifiers": ["ctrl"] }
{ "action": "scroll", "delta_y": 400 }
{ "action": "scroll", "selector": "#footer" }
```

### Extension routing

| Inputs | Path |
|--------|------|
| `selector` and/or `index` (click/type/scroll-to) | DOM via `executeScript` — **no** debugger |
| `x`/`y`, `key`, modifier combos | `chrome.debugger` + CDP `Input.dispatchMouseEvent` / `Input.dispatchKeyEvent` |
| Both element and coord supplied | Prefer selector/index |

### Manifest

- Add `debugger` permission (keep existing `scripting`, tabs, host access to bridge).
- User reloads the unpacked extension once after the update.

### Debugger lifecycle

- Attach when a coordinate/key (or other CDP) action needs it.
- Banner is acceptable while attached.
- Detach after ~60s idle (no CDP actions) or on extension unload, so the banner is not permanent.
- Exact idle timer may be tuned in implementation; default **60 seconds**.

---

## Errors

Return structured failures to Victoria (do not swallow):

| Code | Meaning |
|------|---------|
| `extension_timeout` | Extension did not claim/finish in time (same hint as capture) |
| `no_active_tab` | No usable active tab |
| `element_not_found` | Bad selector/index |
| `debugger_attach_failed` | Could not attach Chrome debugger |
| `action_failed` | Execution failed; include short message |

Bridge-down / health failures reuse existing `browser_bridge_health` guidance.

---

## Safety / non-goals (v1)

**In scope:**

- Localhost bridge trust model (unchanged)
- Active tab only
- Mouse (click, optional button), keyboard (type + keys/combos), scroll
- Same Chrome/Edge profile the user already has open

**Out of scope for v1:**

- OS file pickers / native dialogs
- Multi-tab or window targeting by id
- Re-enabling Hermes Puppeteer / separate Chromium
- Replacing desktop `computer_use` for non-browser apps
- Download automation, extension store packaging

---

## Implementation touchpoints

| Area | Files (expected) |
|------|------------------|
| Bridge | `BrowserCaptureBridge/bridge_server.py` — `/action`, job kind, poll fields |
| Extension | `BrowserCaptureExtension/background.js`, `manifest.json` — action executor, debugger |
| MCP | `MCPServer/house_victoria_mcp/browser_capture.py`, `server.py` — new tools |
| Steering | `HouseVictoria.Services/Persona/HouseVictoriaToolCatalog.cs`, `HermesAIService.cs` as needed |
| Docs | `BrowserCaptureExtension/README.md` |
| Smoke | Extend `Tools/browser-capture-smoke` or sibling for `/action` |

---

## Testing

1. **Bridge:** enqueue action → mock `/result` → client gets ok/error within timeout.
2. **Manual smoke:** capture → click by index → type → key Enter → recapture; then click by `x,y`.
3. **Negative:** bad selector → `element_not_found`; bridge down → clear timeout/hint.
4. **Regression:** capture + cast stream still work; desktop `computer_use` path unchanged when control is on.

---

## Success criteria

- Victoria can click and type in the **same** tab she screenshots, while the HV overlay is up.
- Element path works without debugger banner; coordinate/key path may show banner.
- Browser interact guidance no longer depends on OS `computer_use` clicks on page_map centers.
- Tools available whenever bridge + extension are healthy, independent of `AllowComputerControl`.
