# TASK-20260709-007 follow-up — Browser stream → Desktop live preview

**From:** DEV-01  
**To:** PM-01  
**Date:** 2026-07-09

## Done

### Browser tab streaming into Instrument Stack → Desktop tab

- Bridge: `POST /stream/enable`, `GET /stream/status`, `POST /stream`, `GET /latest`
- Extension: `streamLoop()` pushes active tab ~900ms when streaming enabled
- `BrowserCaptureBridgeClient.cs` — C# client for stream control + latest frame
- `AgentDesktopMonitorService` — enables stream on preview open; prefers browser frames; falls back to desktop capture
- UI: `LiveSourceLabel` under live preview (“Browser tab — …” vs “Desktop”)
- `WindowsScreenCapture.DecodePngToBgra` for PNG → preview pipeline

### Companion v0.4 polish

- Fixed `AppTheme.applyInputLayout` compile error (`setBoxStrokeColorStateList`)
- `assembleDebug` **PASS** — APK at `AndroidRemoteCompanion/app/build/outputs/apk/debug/app-debug.apk`

## Verify

1. `.\Tools\install-browser-capture.ps1` (restart bridge if already running — kill old PID on :17891)
2. Reload unpacked extension in Chrome/Edge (background.js changed)
3. Restart House Victoria (Services DLL updated)
4. Open Instrument Stack → **Desktop** tab with a browser tab active in Chrome/Edge
5. Expect label **Browser tab — {title}** and clean page content (no HV overlay)

## Build evidence

```
dotnet build HouseVictoria.Services -c Release → succeeded
gradlew assembleDebug → BUILD SUCCESSFUL
```
