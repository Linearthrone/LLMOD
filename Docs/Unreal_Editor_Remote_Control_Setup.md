# Unreal Editor Remote Control — House Victoria setup

Victoria’s **editor** tools talk to Epic **Web Remote Control HTTP** (default `http://127.0.0.1:30010`).  
This is **not** the world/embodiment WebSocket on `:8888`.

## Enable in your Unreal project

1. Open the `.uproject` in Unreal Editor.
2. **Edit → Plugins** — enable:
   - **Remote Control API**
   - **Remote Control Web Interface** (Web Remote Control)
   - **Editor Scripting Utilities** (needed for spawn / some editor calls)
3. **Edit → Project Settings → Plugins → Remote Control** (wording varies by UE version):
   - Enable the HTTP remote control server
   - Port **30010** (default)
   - Note any HTTP passphrase if you set one (optional env `HOUSE_VICTORIA_UNREAL_RC_PASS`)
4. Restart the Editor.
5. Keep the Editor **open** while Victoria uses editor tools.

## Verify Remote Control

PowerShell:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:30010/remote/info
```

You should get JSON (HttpServerName / version). If this fails, editor tools will return `rc_unreachable`.

Offline mock (when :30010 is busy or Editor is closed):

```powershell
python Tools/unreal_rc_mock.py --port 30011
$env:HOUSE_VICTORIA_UNREAL_RC_URL = "http://127.0.0.1:30011"
$env:HOUSE_VICTORIA_UNREAL_EDITOR_WRITE = "1"
```

## House Victoria Settings

**Settings → Virtual Environment (Unreal Engine):**

| Setting | Purpose |
|---------|---------|
| Endpoint (`ws://…`) | World / vessel WebSocket (unchanged) |
| Editor RC URL | Remote Control HTTP base (default `http://127.0.0.1:30010`) |
| Allow Unreal Editor Control | Enables write tools (set/call/console/spawn) |

On save (and app startup), House Victoria writes:

`%USERPROFILE%\.house_victoria\unreal_editor.env`

```
HOUSE_VICTORIA_UNREAL_RC_URL=http://127.0.0.1:30010
HOUSE_VICTORIA_UNREAL_EDITOR_WRITE=0|1
```

The MCP server reads this file (process env overrides the file).

## MCP tools

| Tool | Gate |
|------|------|
| `unreal_editor_health` | — |
| `unreal_editor_screenshot` | — |
| `unreal_editor_search_assets` | — |
| `unreal_editor_get_property` | — |
| `unreal_editor_set_property` | write |
| `unreal_editor_call` | write |
| `unreal_editor_console` | write (blocklist: quit/exit/…) |
| `unreal_editor_spawn_actor` | write |

Hermes names: `mcp_house_victoria_unreal_editor_*`.

## Live smoke checklist

1. Editor open, RC verified with `/remote/info`.
2. Settings: set Editor RC URL; enable **Allow Unreal Editor Control**; Save.
3. Confirm `unreal_editor.env` has `WRITE=1`.
4. Restart / ensure MCP `house_victoria` server is running.
5. In chat (as Victoria): ask to check Unreal Editor health → expect `unreal_editor_health`.
6. Ask to search assets (e.g. “Cube”) → `unreal_editor_search_assets`.
7. Ask to read a property on a known object path → `unreal_editor_get_property`.
8. Ask to set a safe property or spawn a simple actor → write tools succeed.
9. Ask for a viewport screenshot → marker under `~/.house_victoria/unreal_editor_captures/`; PNG under project `Saved/Screenshots/`.

## Client smoke (mock)

```powershell
python Tools/unreal_rc_mock.py --port 30011
cd MCPServer
$env:HOUSE_VICTORIA_UNREAL_RC_URL="http://127.0.0.1:30011"
$env:HOUSE_VICTORIA_UNREAL_EDITOR_WRITE="1"
.\.venv\Scripts\python.exe -c "from house_victoria_mcp.unreal_editor import remote_control_health; print(remote_control_health())"
```

## Out of scope (v1)

- World/vessel MCP tools (`move_avatar`, etc. on `:8888`)
- Pixel-clicking Editor UI via `computer_use`
- Arbitrary Python `exec` inside the Editor
