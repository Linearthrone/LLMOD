# House Victoria → Unreal control script (wire commands)

This document matches what **`UnrealEnvironmentService`** actually sends over the WebSocket (`HouseVictoria.Services/VirtualEnvironment/UnrealEnvironmentService.cs`). Use it to implement your Unreal command dispatcher.

There are **two inbound shapes** on the same socket:

## 1) Plain text (most UI / virtual-environment controls)

Messages are a **single UTF-8 line** (no JSON). The first whitespace-delimited token is the verb.

| Verb | Example | Notes |
|------|---------|--------|
| `status` | `status` | App requests a refresh; respond with JSON `status` (see §3). |
| `get_scene_info` | `get_scene_info` | Your server can reply with text or JSON. |
| `capture_scene` | `capture_scene` | App expects a **base64** payload in the reply path used today — prefer returning JSON with base64 until the C# side is upgraded. |
| `spawn_avatar` | `spawn_avatar MyName C:/Models/A.fbx 0 0 0` | `modelPath` may contain spaces in the future; treat everything between name and the last three floats as path if you extend the protocol. |
| `update_pose` | `update_pose {id} px py pz rx ry rz [facial]` | |
| `move_avatar` | `move_avatar {id} x y z rotY` | |
| `animate_avatar` | `animate_avatar {id} Idle` | `Idle` should match a montage/state in your Animation BP. |
| `get_avatar_state` | `get_avatar_state {id}` | |

## 2) JSON (remote companion → Unreal)

When **Settings → Notify Unreal after each reply** is on, the app sends one JSON object (same line). Shape:

```json
{
  "type": "command",
  "payload": {
    "name": "companion_remote_exchange",
    "args": {
      "user": "…",
      "assistant": "…",
      "correlation_id": "…"
    }
  }
}
```

See [Unreal_Protocol.md](./Unreal_Protocol.md) for field semantics.

## 3) JSON you should send **to** House Victoria (server → client)

The WPF client parses JSON text frames with a top-level **`type`** field.

### `status` (fills Virtual Environment status UI)

Top-level keys (same object as `type` — **not** nested under `payload` today):

```json
{
  "type": "status",
  "scene": "YourLevelName",
  "avatar_count": 1,
  "fps": 59.8,
  "rendering": true
}
```

### `scene_update` (optional events)

```json
{
  "type": "scene_update",
  "scene": "YourLevelName",
  "update_type": "CustomReason"
}
```

## Recommended Unreal implementation

1. **WebSocket server** accepts House Victoria as client.
2. **Router**: if message trim-start is `{`, parse JSON; else parse plain text tokens.
3. **Blueprint**: dispatch on verb / `payload.name` → Control Rig parameters, AnimInstance variables, facial curves, etc.

The **`HouseVictoriaBridge`** UE plugin in `Unreal/Plugins/HouseVictoriaBridge` exposes **`Parse Web Socket Message`** so Blueprint/C++ can share one parser.
