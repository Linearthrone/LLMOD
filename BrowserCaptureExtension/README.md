# House Victoria Browser Capture

Captures the **active browser tab** (screenshot + interactive page map) for House Victoria MCP — bypassing desktop framebuffer issues caused by the Topmost WPF overlay.

## Why

`computer_use` / `CopyFromScreen` capture the composited desktop. House Victoria runs as a full-screen **Topmost** overlay, so screenshots often show HV chrome instead of the browser underneath.

This extension captures **inside the browser** via `chrome.tabs.captureVisibleTab` and a DOM element map with viewport coordinates.

## Desktop live preview (Instrument Stack → Desktop tab)

When you open the **Desktop** tab in House Victoria:

1. HV enables cast and connects as **consumer** on `ws://127.0.0.1:17891/ws/cast`
2. The extension connects as **producer** and pushes tab frames over the socket (~750ms)
3. Frames appear instantly in the live preview (no HTTP polling, no overlay capture)

HTTP `/latest.png` and MCP `/capture` remain as fallbacks.

## Architecture

```
Chrome extension (producer) ──WebSocket──► Bridge :17891/ws/cast ──WebSocket──► House Victoria (consumer)
       │                                           │
       └──────── HTTP poll (MCP jobs) ────────────┘ browser_capture_tab
```

## Install (one-time)

### 1. Start the bridge

```powershell
cd C:\Users\kurtw\LLMOD\LLMOD-max-master
.\Tools\install-browser-capture.ps1
```

Or manually:

```powershell
MCPServer\.venv\Scripts\python.exe BrowserCaptureBridge\bridge_server.py
```

Verify: `Invoke-WebRequest http://127.0.0.1:17891/health -UseBasicParsing`

### 2. Load the extension

**Chrome:** `chrome://extensions` → Developer mode → **Load unpacked** → select `BrowserCaptureExtension`

**Edge:** `edge://extensions` → Developer mode → **Load unpacked** → same folder

Click the extension icon — popup should show **bridge connected :17891**.

### 3. Restart Hermes / House Victoria

MCP tools are on the existing `house_victoria` server (no new Hermes MCP entry needed):

- `browser_capture_tab` — screenshot + page map
- `browser_bridge_health` — check bridge status

Hermes tool names:

- `mcp_house_victoria_browser_capture_tab`
- `mcp_house_victoria_browser_bridge_health`

## MCP tool output

```json
{
  "ok": true,
  "url": "https://example.com",
  "title": "Example",
  "screenshot_path": "C:\\Users\\...\\.house_victoria\\browser_captures\\tab-123.png",
  "page_map": {
    "elements": [
      { "tag": "button", "text": "Submit", "center": { "x": 120, "y": 340 }, "selector": "#submit" }
    ]
  }
}
```

Use `page_map.elements[].center` with `computer_use` clicks for browser interactions.

## Routing (automatic)

When desktop control is ON and the user asks about a **browser tab / webpage**, `HermesAIService` forces `browser_capture_tab` instead of `computer_use get_screenshot`.

Desktop-wide requests still use `computer_use`.
