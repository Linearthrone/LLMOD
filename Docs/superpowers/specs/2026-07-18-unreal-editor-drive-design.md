# Unreal Editor Drive Tool — Design Spec

**Date:** 2026-07-18  
**Status:** Approved for implementation  
**Goal:** Give House Victoria Hermes MCP tools to inspect and edit the **open Unreal Editor** via Epic Remote Control HTTP, so she can help stand up the vessel—without OS mouse and without the world WebSocket lane.

---

## Problem

- World/embodiment control already exists over `ws://:8888` (`UnrealEnvironmentService`) for PIE/game avatar ops.
- There is no MCP surface for **Editor** work (assets, properties, spawn, console).
- `computer_use` on the editor window fights the Topmost overlay and is imprecise for Content Browser / Details panels.

## Decisions

| Topic | Choice |
|-------|--------|
| Lane | **Editor only** — do not extend world WS protocol |
| Transport | Epic **Web Remote Control HTTP** `http://127.0.0.1:30010` (configurable) |
| Bridge | **None** — MCP client calls RC directly (RC is already request/response) |
| Target project | Whatever Editor has RC listening |
| v1 ops | Search assets, get/set property, call UFunction, console, spawn actor, viewport screenshot |
| Safety | Read tools always; writes require `AllowUnrealEditorControl` + MCP write gate |

## Architecture

```
Victoria (Hermes)
    → house_victoria MCP tools (unreal_editor_*)
        → unreal_editor.py
            → Unreal Editor Remote Control :30010
                → open .uproject
```

World path remains separate (`VictoriaEmbodimentService` → `:8888`).

## MCP tool surface

| Tool | Role | Gate |
|------|------|------|
| `unreal_editor_health` | RC `/remote/info` | — |
| `unreal_editor_screenshot` | Viewport → PNG under `~/.house_victoria/unreal_editor_captures/` | — |
| `unreal_editor_search_assets` | `/remote/search/assets` | — |
| `unreal_editor_get_property` | property READ | — |
| `unreal_editor_set_property` | property WRITE | write |
| `unreal_editor_call` | `/remote/object/call` | write |
| `unreal_editor_console` | Console command (blocklist) | write |
| `unreal_editor_spawn_actor` | EditorLevelLibrary spawn | write |

**Steered loop:** health → screenshot/search → mutate → verify.

Hermes names: `mcp_house_victoria_unreal_editor_*`.

## Config / handoff

- `AppConfig.AllowUnrealEditorControl` (default false)
- `AppConfig.UnrealRemoteControlUrl` (default `http://127.0.0.1:30010`)
- App writes `%USERPROFILE%\.house_victoria\unreal_editor.env` for the MCP process:
  - `HOUSE_VICTORIA_UNREAL_EDITOR_WRITE=0|1`
  - `HOUSE_VICTORIA_UNREAL_RC_URL=...`

## Out of scope (v1)

- World MCP tools (`unreal_world_*`)
- Pixel clicking editor chrome
- Arbitrary Python `exec` in the editor
- Multi-editor / remote hosts

## Success criteria

- With Editor + RC up and allow flag on: search asset → set property or spawn → screenshot path returned.
- With RC down: health returns clear enablement hint.
- World WebSocket path unchanged.
