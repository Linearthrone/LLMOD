# Victoria embodiment — My Project (UE 5.8)

Hook **MHC Victoria** (`BP_MHC_Victoria`) in your Unreal project to House Victoria on the PC so she can **walk, talk, see, and touch** in the house level.

## Architecture

```
House Victoria (brain)                    My Project (body)
─────────────────────                    ───────────────────
Chat / Autonomy  ──WebSocket ws://8888──►  WebSocket server
IVictoriaEmbodimentService                  HouseVictoriaBridge parser
  ├ talk  → companion_remote_exchange       → Anim BP: Talk, LipSync_Talking
  ├ walk  → wander / move_avatar            → Character movement / AI wander
  ├ see   → capture_scene, get_scene_info   → Scene capture → (optional) vision
  └ touch → touch_interact                    → Physics handle / overlap
```

House Victoria connects **outbound** to Unreal. Unreal must run a **WebSocket server** on port **8888** (default).

## 1. Install the bridge plugin

From the LLMOD repo root (PowerShell):

```powershell
.\scripts\Install-VictoriaBridge-MyProject.ps1
```

Or manually copy `Unreal/Plugins/HouseVictoriaBridge` into:

`C:\Users\kurtw\OneDrive\Documents\Unreal Projects\MyProject\Plugins\HouseVictoriaBridge`

Enable in **Edit → Plugins → House Victoria Bridge**, restart the editor.

Add to `MyProject.uproject`:

```json
"Plugins": [
  { "Name": "WebSocketNetworking", "Enabled": true },
  { "Name": "HouseVictoriaBridge", "Enabled": true }
]
```

## 2. WebSocket server in Unreal

The reference plugin **parses** messages only. You still need a server (Blueprint or C++):

1. Create **Game Instance** or **Subsystem** Blueprint `BP_HouseVictoriaBridge`.
2. On init, start a WebSocket server on **8888** (use **WebSocketNetworking** or a marketplace plugin).
3. On message received:
   - Call **Parse Web Socket Message** (HouseVictoriaBridge).
   - Switch on **Primary Verb** / JSON `companion_remote_exchange`.

Smoke-test without Unreal:

```powershell
python Tools/unreal_mock_ws.py
```

Start House Victoria — startup log should show `Victoria embodiment bridge started.` when the mock is listening.

## 3. Avatar id `victoria`

House Victoria uses `VictoriaUnrealAvatarId=victoria` in `App.config`. Your dispatcher must map `victoria` → `BP_MHC_Victoria` in the level (do **not** spawn a second copy unless you use `spawn_avatar`).

On connect, the PC sends:

```
focus_avatar victoria
set_locomotion victoria 1.0 2.0
status
```

## 4. Blueprint wiring (per capability)

### Talk

On `companion_remote_exchange` or `animate_avatar` with `Talk` / `LipSync_Talking`:

- Play talking montage on MetaHuman Anim BP.
- Drive face board / Live Link curves for lip sync.
- Optional: pipe `TTSEndpoint` audio to MetaHuman (separate from WebSocket).

### Walk

| Command | Blueprint action |
|---------|------------------|
| `wander victoria {sec}` | Enable wander AI / nav mesh random reachable points for `{sec}` |
| `move_avatar victoria x y z rotY` | `AI Move To` or `Add Movement Input` |
| `set_locomotion victoria walk run` | Set `MaxWalkSpeed` / `MaxRunSpeed` on character |

Chat replies containing “walk”, “go to”, “head to”, etc. trigger `wander` automatically.

### See

| Command | Blueprint action |
|---------|------------------|
| `get_scene_info` | Reply with JSON: actors, tags, Victoria location |
| `capture_scene` | Render target screenshot → base64 JPEG in JSON reply |
| `look_at victoria x y z` | Rotate head / control rig look-at |

Replies with “look at”, “what do you see”, etc. trigger see commands from the PC.

### Touch

```
touch_interact victoria {target} touch
```

- Resolve `{target}`: match actor **tag** or **name** (spaces in target become `_` on the wire).
- Enable physics grab, overlap “use” interaction, or line-trace pick.
- Reply with `scene_update` JSON when something was touched.

## 5. House Victoria settings

In `HouseVictoria.App\App.config`:

| Key | Default | Meaning |
|-----|---------|---------|
| `UnrealEngineEndpoint` | `ws://localhost:8888` | Unreal WebSocket URL |
| `EnableVictoriaEmbodiment` | `true` | Auto-connect + route chat |
| `VictoriaUnrealAvatarId` | `victoria` | Avatar id in commands |
| `NotifyUnrealAfterDesktopChat` | `true` | SMS window chat drives avatar |
| `RemoteCompanionNotifyUnreal` | `false` | Phone remote API also drives avatar |
| `WalkSpeed` / `RunSpeed` | `1.0` / `2.0` | Sent via `set_locomotion` |

Victoria is embodied when the chat contact is **primary AI** or name contains **Victoria**.

## 6. Run order

1. Open **My Project** in UE 5.8, open your house level with `BP_MHC_Victoria`.
2. **Play In Editor** with WebSocket server running.
3. Start **House Victoria** (or ensure mock on 8888).
4. Chat with Victoria in the desktop SMS window — she should talk (anim) and walk/see/touch when the reply implies it.

## 7. Troubleshooting

| Symptom | Check |
|---------|--------|
| No connection | PIE running? Port 8888 free? Firewall? |
| Talk only, no walk | Nav mesh in level? `wander` handler in BP? |
| Wrong character moves | `focus_avatar` must reference placed `BP_MHC_Victoria` |
| Desktop chat silent | `NotifyUnrealAfterDesktopChat=true` and contact is primary/Victoria |

See also: [Unreal_ControlScript_Commands.md](./Unreal_ControlScript_Commands.md), [Unreal_Protocol.md](./Unreal_Protocol.md).
