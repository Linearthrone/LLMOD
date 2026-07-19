# Browser Drive Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Victoria in-tab mouse/keyboard control of the same Chrome/Edge tab she captures, via the existing bridge + extension (no OS `computer_use` for browser clicks).

**Architecture:** Extend `BrowserCaptureBridge` with `POST /action` jobs; extension executes element actions via `scripting.executeScript` and coordinate/key via `chrome.debugger` + CDP Input; expose `browser_click` / `browser_type` / `browser_key` / `browser_scroll` on `house_victoria` MCP; update tool catalog so browser drive is always advertised (not gated on `AllowComputerControl`).

**Tech Stack:** FastAPI bridge (Python), Chrome MV3 extension (JS), house_victoria FastMCP, C# `HouseVictoriaToolCatalog`.

## Global Constraints

- Element path preferred; coordinate/key uses debugger (banner OK).
- Tools available whenever bridge + extension are healthy — **not** gated on `AllowComputerControl`.
- Active tab only (same as capture).
- Debugger idle detach default: **60 seconds**.
- Do not re-enable Puppeteer / Hermes browser toolset.
- Spec: `Docs/superpowers/specs/2026-07-18-browser-drive-tool-design.md`

## File map

| File | Responsibility |
|------|----------------|
| `BrowserCaptureBridge/bridge_server.py` | Job kinds, `/action`, poll fields, result `detail` |
| `BrowserCaptureExtension/manifest.json` | Add `debugger`, bump version |
| `BrowserCaptureExtension/background.js` | Route action jobs; DOM + CDP executors |
| `MCPServer/house_victoria_mcp/browser_capture.py` | HTTP client for `/action` |
| `MCPServer/house_victoria_mcp/server.py` | Register four drive tools + tool list text |
| `HouseVictoria.Services/Persona/HouseVictoriaToolCatalog.cs` | Always-on browser drive routing; stop OS-click guidance for tabs |
| `BrowserCaptureExtension/README.md` | Document drive tools + banner |
| `Tools/browser-capture-smoke/` or ad-hoc PowerShell | Smoke `/action` against live bridge |

---

### Task 1: Bridge `/action` job protocol

**Files:**
- Modify: `BrowserCaptureBridge/bridge_server.py`
- Test: PowerShell/curl against running bridge (or pytest if added)

**Interfaces:**
- Produces: `POST /action` body fields `action`, `selector`, `index`, `x`, `y`, `button`, `text`, `clear`, `key`, `modifiers`, `delta_x`, `delta_y`, `timeout_seconds`
- Produces: poll payload includes `kind` (`capture`|`action`) plus action fields when kind is action
- Produces: result may include `detail` (string)

- [ ] **Step 1:** Generalize job dataclass to support `kind`, action params; keep capture defaults.
- [ ] **Step 2:** Add `ActionRequest` pydantic model and `POST /action` wait loop mirroring `/capture`.
- [ ] **Step 3:** Update `/poll` to return `kind` and action fields; capture jobs set `kind: "capture"`.
- [ ] **Step 4:** Extend `ResultPayload` with optional `detail: str | None`.
- [ ] **Step 5:** Update capture finalize hint to prefer browser drive tools over computer_use clicks.
- [ ] **Step 6:** Commit bridge changes.

**Acceptance:** `POST /action` with no extension → `extension_timeout` within timeout; with mocked `/result` → returns ok/detail.

---

### Task 2: Extension action executor (DOM + CDP)

**Files:**
- Modify: `BrowserCaptureExtension/manifest.json`
- Modify: `BrowserCaptureExtension/background.js`

**Interfaces:**
- Consumes: poll `kind` + action fields from Task 1
- Produces: `/result` with `{ ok, error?, detail?, tab_id, url, title }`

- [ ] **Step 1:** Add `"debugger"` to permissions; bump version to `1.3.0`.
- [ ] **Step 2:** In `pollBridge`, if `job.kind === "action"` → `runActionJob(job)`, else capture.
- [ ] **Step 3:** Implement DOM helpers: resolve by selector or page_map index; click; type (optional clear); scroll to element / by delta via `window.scrollBy`.
- [ ] **Step 4:** Implement debugger attach + CDP mouse click at `x,y` and key/combo; track last-used tab; idle detach after 60s.
- [ ] **Step 5:** Prefer selector/index over x/y when both present for click/type/scroll.
- [ ] **Step 6:** Commit extension changes.

**Acceptance:** Manual — capture still works; click by index on a test page; click by x,y shows debugger banner; key Enter submits.

---

### Task 3: MCP client + tools

**Files:**
- Modify: `MCPServer/house_victoria_mcp/browser_capture.py`
- Modify: `MCPServer/house_victoria_mcp/server.py` (`register_browser_tools`, `list_house_victoria_tools` browser section)

**Interfaces:**
- Produces: `request_browser_action(**kwargs) -> dict`
- Produces: tools `browser_click`, `browser_type`, `browser_key`, `browser_scroll`

- [ ] **Step 1:** Add `request_browser_action` POST to `/action` (same error pattern as capture).
- [ ] **Step 2:** Register four `@mcp_server.tool()` functions calling it with the right `action` field.
- [ ] **Step 3:** Update `list_house_victoria_tools` / browser_capture_extension dict entries.
- [ ] **Step 4:** Commit MCP changes.

**Acceptance:** Import module; call helpers against bridge (timeout without extension is OK for smoke).

---

### Task 4: Catalog + docs

**Files:**
- Modify: `HouseVictoria.Services/Persona/HouseVictoriaToolCatalog.cs`
- Modify: `BrowserCaptureExtension/README.md`

- [ ] **Step 1:** Add constants for new Hermes tool names.
- [ ] **Step 2:** Always include a BROWSER TAB section (capture + drive) in `BuildHermesToolGuide`, even when `includeComputerUse` is false.
- [ ] **Step 3:** When control is on: browser interact → drive tools; computer_use only for non-browser desktop. Remove “use computer_use clicks on page_map centers” guidance.
- [ ] **Step 4:** Update `BuildBrowserCaptureSteering` / mandatory first action to mention drive tools for interact tasks.
- [ ] **Step 5:** Update extension README with tools, debugger banner, reload note.
- [ ] **Step 6:** Commit.

---

### Task 5: End-to-end smoke

- [ ] **Step 1:** Restart bridge; reload unpacked extension.
- [ ] **Step 2:** `POST /capture` → ok with page_map.
- [ ] **Step 3:** `POST /action` click by index → ok.
- [ ] **Step 4:** `POST /action` type + key → ok.
- [ ] **Step 5:** Document any manual reload steps for the user.

---

## Spec coverage check

| Spec item | Task |
|-----------|------|
| Element + coord/key | Task 2 |
| Always available (not AllowComputerControl) | Task 4 |
| Debugger banner OK | Task 2 |
| Separate MCP tools | Task 3 |
| Bridge `/action` protocol | Task 1 |
| Errors listed in spec | Tasks 1–2 |
| 60s idle detach | Task 2 |
| Catalog routing | Task 4 |
| README | Task 4 |
| Active tab only | Task 2 |
| No Puppeteer re-enable | (non-change) |
