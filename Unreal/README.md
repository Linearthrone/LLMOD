# House Victoria → Unreal Engine bridge assets

House Victoria connects to Unreal as a **WebSocket client** (default `ws://127.0.0.1:8888`). Your Unreal project must run a **WebSocket server** on that port and parse incoming text (see [Docs/Unreal_Protocol.md](../Docs/Unreal_Protocol.md) and [Docs/Unreal_ControlScript_Commands.md](../Docs/Unreal_ControlScript_Commands.md)).

## UE 5.7.x (including 5.7.4) — read this first

If your Unreal project is **`HouseVictoria`** under OneDrive (`EngineAssociation` **5.7**), you almost certainly **do not need this plugin folder at all**.

That game project already ships:

- `UHouseVictoriaSocketSubsystem` (WebSocket **server** on port 8888)
- `UHouseVictoriaBridgeBPLibrary` + settings **inside the `HouseVictoria` game module**

Copying `Plugins/HouseVictoriaBridge` from this repo into that project often causes **“plugin built for a different engine version”** or **failed to load** because Unreal auto-loads every `.uplugin` under `Plugins/` and may pick up stale binaries.

**Recovery (project won’t open):**

1. Delete or rename `YourUnrealProject/Plugins/HouseVictoriaBridge` (if present).
2. Confirm `HouseVictoria.uproject` does **not** list `HouseVictoriaBridge` under `"Plugins"` (it should not).
3. Delete `Binaries`, `Intermediate`, and `Saved` (or at least `Saved/Logs`) if the editor still crashes.
4. Rebuild the **HouseVictoria** module from your UE 5.7.4 install.

See also: `Docs/LEXIE_SETUP.md` in your Unreal repo (same warning).

Use this LLMOD folder only as a **reference copy** of the parser, or for a **different** UE project that does not already embed the bridge in its game module.

## What is in this folder

| Path | Purpose |
|------|---------|
| `Plugins/HouseVictoriaBridge/` | UE **C++ plugin**: Blueprint-callable parser for House Victoria wire messages (JSON + line protocol). |
| `ContentExamples/animation_control_hints.txt` | Suggested `animate_avatar` animation names / state machine hooks for your project. |

The plugin **does not** open a socket by itself (Epic’s WebSocket server APIs vary by engine version). Wire your preferred WebSocket server plugin or subsystem to `OnMessage`, then call **`Parse Web Socket Message`** on the Blueprint library.

## Install (UE 5.3+ recommended)

1. Copy the entire `Plugins/HouseVictoriaBridge` folder into your Unreal project’s `Plugins` directory (e.g. `YourProject/Plugins/HouseVictoriaBridge`).
2. Regenerate project files and **compile C++** (requires a C++ project, or “Add C++ class” once).
3. Enable the plugin: **Edit → Plugins → House Victoria Bridge**.
4. Implement or enable a **WebSocket server** on the same URL/port as House Victoria **Settings → Unreal Engine endpoint** (default port **8888**).
5. On each inbound UTF-8 text frame, call **`House Victoria Bridge → Parse Web Socket Message`**. Use the returned struct to drive Animation Blueprints, Control Rig, MetaSounds, etc.

## Quick validation without Unreal

From the repo root:

```bash
python Tools/unreal_mock_ws.py
```

Point House Victoria’s Unreal endpoint at `ws://127.0.0.1:8888`. The mock speaks JSON for `companion_remote_exchange`; plain-text control commands are documented in `Docs/Unreal_ControlScript_Commands.md`.

## Next steps (your side)

- Map `PrimaryVerb` / `CompanionAssistant` / plain tokens to your AI character’s anim notifies, lipsync, and look-at.
- Send **`type: "status"`** JSON updates (see command doc) so the House Victoria **Virtual Environment** window shows FPS, scene name, and avatar count.
