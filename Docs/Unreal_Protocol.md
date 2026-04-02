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

## Validation

1. Start your Unreal build or the mock server in `Tools/unreal_mock_ws.py`.
2. Set **Settings → Virtual Environment → Unreal Engine** to the same URL and use **Test Connection**.
3. Open **Virtual Environment Controls** from the system monitor drawer and verify status/events.

## Notes

- The exact `payload` schema can evolve; keep Unreal and this doc in sync for your project.
- Until a real Unreal plugin matches this envelope, treat integration as **experimental**.
