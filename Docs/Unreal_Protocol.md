# House Victoria ↔ Unreal Engine WebSocket Protocol

The WPF app uses `UnrealEnvironmentService` ([HouseVictoria.Services/VirtualEnvironment/UnrealEnvironmentService.cs](../HouseVictoria.Services/VirtualEnvironment/UnrealEnvironmentService.cs)) as a **JSON-over-WebSocket** client. Your Unreal project (or a mock server) must accept connections and exchange messages on the configured endpoint (default `ws://localhost:8888`).

## Connection

- Client: `System.Net.WebSockets.ClientWebSocket`
- On connect, the service sets internal status to connected and starts a receive loop.
- Reconnect uses exponential backoff (see service implementation).

## Recommended JSON envelope

All messages should be UTF-8 JSON objects with at least:

| Field | Type | Description |
|-------|------|----------------|
| `type` | string | Message kind: `ping`, `pong`, `scene`, `command`, `error`, `status` |
| `payload` | object | Optional body |

### Examples

**Heartbeat (app → Unreal):**

```json
{ "type": "ping", "payload": { "t": 1710000000 } }
```

**Heartbeat (Unreal → app):**

```json
{ "type": "pong", "payload": { "t": 1710000000 } }
```

**Scene info (Unreal → app):**

```json
{
  "type": "scene",
  "payload": {
    "name": "MainLevel",
    "avatarCount": 1,
    "objects": 42
  }
}
```

**Command (app → Unreal):**

```json
{
  "type": "command",
  "payload": {
    "name": "move",
    "args": { "dx": 1, "dy": 0, "dz": 0 }
  }
}
```

**Error (either direction):**

```json
{ "type": "error", "payload": { "message": "Unknown command" } }
```

## `companion_remote_exchange` (remote phone chat → Unreal)

After each successful **remote companion** chat reply, House Victoria may send this command so Unreal can drive embodiment (animation, lipsync hooks, logging, etc.). Emission is controlled by **Settings → Notify Unreal after each reply** (`AppConfig.RemoteCompanionNotifyUnreal`). The app only sends when the virtual-environment WebSocket is **connected**; failures are logged and do not affect the chat API response.

**Source:** [HouseVictoria.Services/RemoteCompanion/RemoteCompanionChatService.cs](../HouseVictoria.Services/RemoteCompanion/RemoteCompanionChatService.cs) (`TryNotifyUnrealAsync`).

#### Envelope

Same top-level pattern as other commands: `type` = `"command"`, `payload.name` identifies the command.

#### `payload` shape

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Always `"companion_remote_exchange"` |
| `args` | object | See below |

#### `args` fields

| Field | Type | Description |
|-------|------|-------------|
| `user` | string | User message text (phone / STT), same turn as the AI reply |
| `assistant` | string | AI reply text |
| `correlation_id` | string | Optional. Opaque id (e.g. GUID without dashes) for logs and tracing one exchange end-to-end |

#### Example (as sent by the app)

```json
{
  "type": "command",
  "payload": {
    "name": "companion_remote_exchange",
    "args": {
      "user": "How was your day?",
      "assistant": "Quiet and productive — thanks for asking!",
      "correlation_id": "a1b2c3d4e5f6478980abcdef12345678"
    }
  }
}
```

#### Unreal handling (minimum viable)

1. Parse UTF-8 JSON text from the socket.
2. If `type` is `"command"` and `payload.name` is `"companion_remote_exchange"`, read `payload.args.user` and `payload.args.assistant`.
3. **v1:** Log both strings; optionally trigger a Blueprint event / play a neutral idle animation.
4. Do **not** assume `correlation_id` is present (older builds); use it only when present for debug logs.

Until a full UE plugin lands in-repo, validate the wire format with **`Tools/unreal_mock_ws.py`**, which acknowledges this command without crashing.

## Validation

1. Start your Unreal build or the mock server in `Tools/unreal_mock_ws.py` (logs `companion_remote_exchange` when received).
2. Set **Settings → Virtual Environment → Unreal Engine** to the same URL and use **Test Connection**.
3. Open **Virtual Environment Controls** from the system monitor drawer and verify status/events.

## Notes

- The exact `payload` schema can evolve; keep Unreal and this doc in sync for your project.
- Until a real Unreal plugin matches this envelope, treat integration as **experimental**.
