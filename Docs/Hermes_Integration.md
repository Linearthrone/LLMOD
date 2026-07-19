# Hermes Agent integration (House Victoria)

House Victoria routes chat through **Hermes Agent** when `PrimaryLLM` is `hermes` (default after setup) or when a persona has `AdditionalServers["hermes"] = "true"`.

## Architecture

```
SMS / Remote Companion
        │
        ▼
  FallbackAIService
        │
        ▼ (PrimaryLLM=hermes)
  HermesAIService  ──POST──►  Hermes gateway :8642/v1/chat/completions
                                    │
                                    ├── terminal, browser, skills
                                    └── MCP plugins (filesystem, computer-use, …)
```

Hermes runs the **tool loop**. House Victoria keeps personas, UI, SQLite history, remote companion, and Unreal hooks.

## One-time setup (Windows)

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\setup-hermes-integration.ps1
```

This script:

1. Installs Hermes (native Windows) if missing
2. Writes `%USERPROFILE%\.hermes\.env` with `API_SERVER_ENABLED=true` and matching API key
3. Registers House Victoria MCP as `mcp_servers.house_victoria` in `config.yaml` (stdio; includes **MT4 tools**)
4. Sets `PrimaryLLM=hermes` and `MCPServerEndpoint=<http://127.0.0.1:8080>` in `App.config`

For persona MCP + MetaTrader wiring only (no Hermes reinstall):

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\setup-persona-mcp.ps1
```

## Persona MCP + MetaTrader

Each AI persona stores **`MCPServerEndpoint`** (default `http://localhost:8080`). That HTTP server exposes memory tools and MT4 bridge tools:

| Tool | Purpose |
|------|---------|
| `mt4_status` | Bridge connection + account snapshot |
| `mt4_list_symbols` | Broker symbol list + base→broker map (e.g. EURUSD → EURUSD.pro) |
| `mt4_get_market_data` | Bid/ask for a symbol |
| `mt4_get_open_positions` | Open positions (HouseVictoria magic) |
| `mt4_execute_trade` | Place a trade; **success only when ticket verified in OpenPositions.json** |
| `mt4_close_position` | Close by ticket from `mt4_get_open_positions`; verified when ticket leaves OpenPositions.json |
| `mt4_verify_ticket` | Confirm a ticket exists in OpenPositions.json |

**In the app:** Settings → MCP Server = `http://localhost:8080`. New personas inherit this automatically.

**With Hermes (PrimaryLLM=hermes):** Chat tool loops use Hermes MCP, not the persona HTTP field. Ensure `mcp_servers.house_victoria` is in `~/.hermes/config.yaml` (added by the setup scripts above). Restart `hermes gateway` after changes.

**MT4 prerequisites:** `MT4DataPath` in `App.config`, `HouseVictoriaBridge.mq4` attached with AutoTrading on. See `Docs/MT4_INTEGRATION_SUMMARY.md`.

## Start stack

```bat
start.bat
```

When primary is Hermes, `start.bat` starts **Ollama** (LLM backend), **MCP**, and **`hermes gateway`**.

Or use **System Monitor** → start **Hermes Agent** / **Ollama** / **MCP**.

## Settings (in-app)

**Settings → LLM Servers → Hermes Agent**

| Field | Purpose |
|-------|---------|
| API endpoint | Default `http://127.0.0.1:8642/v1` |
| API key | Must match `API_SERVER_KEY` in `~/.hermes/.env` |
| Model id | Cosmetic; default `hermes-agent` |
| Auto-start gateway | Spawn `hermes gateway` on app launch |
| Primary checkbox | Route all chat through Hermes |

**Test** tries to start the gateway and probe `/health` + `/v1/models`.

## Per-persona Hermes (mixed mode)

Keep `PrimaryLLM=ollama` for fast chat, enable Hermes only for one persona:

In the persona databank `config.json` or via code, set:

```json
"AdditionalServers": { "hermes": "true" }
```

That persona’s messages use Hermes tools; others stay on Ollama.

## Desktop / computer use

| Platform | Mechanism |
|----------|-----------|
| **macOS** | Hermes built-in `computer_use` toolset via **cua-driver** (`hermes computer-use install`) |
| **Windows** | **`computer-use-mcp`** MCP server (screenshot, click, type, scroll) |

House Victoria's setup script configures the right path for your OS:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\setup-hermes-integration.ps1
```

To skip desktop control: add `-SkipComputerUse`.

**Windows:** setup adds `computer_use` under `mcp_servers:` in the active Hermes config (`hermes config path`, usually `%LOCALAPPDATA%\\hermes\\config.yaml` on native Windows installs). Restart `hermes gateway` after setup.

**macOS:** setup runs `hermes computer-use install` and adds the `computer_use` toolset.

Hermes also provides built-in **terminal**, **browser**, and **file** tools via `hermes-cli` and `web` toolsets.

## Security

- Hermes API exposes **full shell access**. Keep `API_SERVER_KEY` strong and bind to `127.0.0.1` only.
- Remote companion does not expose Hermes directly; it uses the same `IAIService` path with auth on `:17890`.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Hermes Test fails | Run `Tools/setup-hermes-integration.ps1`; then `hermes gateway` manually |
| Empty / timeout replies | Tool run in progress — wait (15 min client timeout). Check `Media\hermes-gateway.log` |
| MCP tools missing | Ensure MCP on :8080; verify `mcp_servers.house_victoria` in config.yaml |
| Computer use unavailable | **Windows:** verify `mcp_servers.computer_use` in config; restart gateway. **macOS:** `hermes computer-use install` + `computer_use` toolset |
| Wrong LLM | Configure Hermes provider: `hermes setup` or edit `~/.hermes/config.yaml` |
| `pydantic_core._pydantic_core` missing | Project `.venv` leaked into Hermes via `PYTHONPATH`/`VIRTUAL_ENV`. Run: `hermes gateway stop`, then `$env:PYTHONPATH=$null; $env:VIRTUAL_ENV=$null; hermes gateway start` (or restart via `start.bat`, which clears these vars) |
| AI says it has no `computer_use` tool | Hermes 0.18+ registers `mcp__computer_use__computer` (double underscores). Rebuild/restart the app so prompts match; ensure **Allow computer control** is ON in Agent Desktop; restart gateway after `cua-driver` install |

## Files added

| Path | Role |
|------|------|
| `HouseVictoria.Services/AIServices/HermesAIService.cs` | OpenAI client for Hermes API |
| `HouseVictoria.Services/Hermes/HermesGatewayService.cs` | Health + auto-start gateway |
| `Tools/setup-hermes-integration.ps1` | Install + config merge |
