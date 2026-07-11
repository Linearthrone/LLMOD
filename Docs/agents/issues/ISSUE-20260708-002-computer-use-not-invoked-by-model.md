---
type: issue
issue_id: "20260708-002"
title: "Victoria (Hermes) does not reliably invoke computer_use / screenshot tool"
severity: P1
status: Open — QA-027 FAIL (2026-07-08); gpt-oss:120b-cloud test did not invoke computer_use; OPS revert recommended
qa_last_run: 2026-07-08
qa_result: FAIL
---

# ISSUE · computer_use registered but not invoked by the model

## Summary
The `computer-use-mcp` tool registers correctly (`mcp_computer_use_computer`) and the underlying
screen capture + model vision both work in isolation, but no tested model reliably turns a
natural request ("take a screenshot and tell me what's on my screen") into a `computer_use`
tool call through the Hermes `api_server` (`:8642`).

## Evidence (live, 2026-07-08)
- Registration: `MCP server 'computer_use' (stdio): registered 1 tool(s): mcp_computer_use_computer`.
- Capture proven good: nut-js `screen.grab()` (same lib computer-use-mcp uses) returns real
  desktop content (1600/1600 non-black samples, 1920x1080), incl. from a detached/hidden process.
- Vision proven good: feeding a real screenshot via `image_url` → model describes it accurately.
- Tool invocation FAILS/inconsistent:
  - `gemma4:31b-cloud`: declines; one early run returned vague "1920x1080 / black rectangle"
    (likely partial/hallucinated call, not a real capture — capture is verified good).
  - `qwen3-coder:480b-cloud`: calls the **terminal** instead (coder bias), fails on PS syntax.
  - `gpt-oss:120b-cloud`: declines, asks user to upload an image.

## Likely causes
1. Tool guide gap: `HouseVictoriaToolCatalog.BuildHermesToolGuide`
   (`HouseVictoria.Services/Persona/HouseVictoriaToolCatalog.cs:29-54`) never mentions
   computer-use, so the persona is not told the desktop-control capability exists.
2. Opaque tool name: `mcp_computer_use_computer` (+ `action` enum) does not signal "screenshot
   the desktop"; models don't associate a screen request with it.
3. Weak/large-toolset selection: `gemma4:31b-cloud` is a poor agentic tool-caller; ~97 tools in a
   ~31k-token prompt makes selection worse.
4. `ShareScreenWithAI` gate (`HermesAIService.cs:242-268`) actively injects
   "do NOT call the computer_use tool" whenever screen-sharing is on — from the App path this
   suppresses control entirely (see DEV 017 item 3).

## Recommended fix (owner: DEV via TASK-20260708-017 + a model decision)
1. Add a computer-use section to the Hermes tool guide (screenshot/click/type/scroll) so the
   persona knows it exists and when to use it.
2. Split "passive share" from "allow control" so sharing the screen no longer forbids the tool
   (DEV 017 item 3).
3. Choose a stronger tool-calling model for the Hermes default (probed available:
   `gpt-oss:120b-cloud`, `qwen3-coder:480b-cloud`, `deepseek-v3.1:671b-cloud`). PM/user decision
   because it changes Victoria's persona/voice and may incur Ollama-cloud cost.

## Workaround (works today)
Explicitly naming the tool in the message can trigger it, but is unreliable — not a real fix.

## QA regression evidence (QA-01, TASK-20260708-021, 2026-07-08) — FAIL

After DEV-017 + OPS-020, with `AllowComputerControl=true` (persisted via `user-settings.json`,
app restarted, guidance suffix `[You MAY use the computer_use tool...]` confirmed in gateway log):

| Run | Control | Tool invoked | `mcp_computer_use_computer`? | Reply |
|---|---|---|---|---|
| B | ON | **`browser_vision`** (failed: model no image input) | **No** | Claims *"Maestro - AI-first higher education"* — hallucinated after tool error |
| C | OFF | none (`tool_turns=0`) | **No** | Same Maestro claim, no tools (negative control OK) |

Key log lines (session `api-27211f8cacfb38af`, control ON):

```
msg='Take a screenshot... [You MAY u...'
tools.browser_tool: Created local browser session ...
agent.tool_executor: Tool browser_vision returned error ... 'this model does not support image input'
Turn ended: tool_turns=1  (browser_vision only — never computer_use)
```

**New contributing factors:**
- Remote/desktop Hermes path lacks full `BuildHermesToolGuide` computer-use section (SMS-only per DEV-017).
- `qwen3-coder:480b-cloud` routes "screenshot" requests to `browser_vision`, not `mcp_computer_use_computer`.
- Long conversation history (`history=24`) may reinforce hallucinated "Maestro" replies.

Full report: `Docs/agents/reports/TASK-20260708-021-QA01-to-PM01.md`

## QA re-regression evidence (QA-01, TASK-20260708-023, 2026-07-08) — FAIL

After DEV-022 (`BuildHermesToolGuide` + desktop-screenshot steering in `HermesAIService`), with
`AllowComputerControl=true`, Release build 12:35, **primary persona Victoria** (`977d778f-…`),
**fresh history** (`history=0`):

| Run | Contact | History | Tool invoked | `mcp_computer_use_computer`? | Reply |
|---|---|---|---|---|---|
| B (authoritative) | Victoria (primary) | 0 | **`terminal`** + **`vision_analyze`** (failed) + **`execute_code`** | **No** | Names real `Cursor.exe` / `LLMOD-max-master` title via coder workarounds (~93s, 15 tool turns) |
| Invalid path | LEXI (non-primary) | low | *(none — bypasses Hermes via direct Ollama)* | **No** | Hallucinated `Google Chrome` title (~1s, no gateway POST) |

Key log lines (session `api-c84dd00380c06e42`, control ON, fresh history):

```
msg='Take a screenshot of my desktop… [You MAY u…'  history=0
tool skills_list / terminal / vision_analyze (error: no image input) / execute_code
Turn ended: tool_turns=15  (never computer_use)
grep agent.log: mcp_computer_use_computer appears only in MCP registration lines, never tool_executor
```

**Improvement vs QA-021:** `browser_vision` **not** chosen on authoritative run.
**Still failing:** `mcp_computer_use_computer` never invoked.

Full report: `Docs/agents/reports/TASK-20260708-023-QA01-to-PM01.md`

## QA re-regression evidence (QA-01, TASK-20260708-025, 2026-07-08) — FAIL

After DEV-024 (`BuildDesktopScreenshotMandatoryFirstAction` + strengthened steering + client
`tool_choice`), with `AllowComputerControl=true`, Release build **12:45:09**, **primary persona
Victoria** (`977d778f-…`), **fresh history** (`history=0`, cleared from `Data/Memory/HouseVictoria.db`):

| Run | Contact | History | Tool invoked | `mcp_computer_use_computer`? | Reply |
|---|---|---|---|---|---|
| B (authoritative) | Victoria (primary) | 0 | **none** (text decline) | **No** | *"I don't have the ability to take a screenshot…"* (~4.5s) |
| C (spot-check) | Victoria (primary) | 6 | none | **No** | "Paris" — no mandatory block (negative control OK) |

Key log lines (session `api-6a716b700da68710`, control ON, fresh history):

```
msg='[MANDATORY FIRST ACTION — execute before any other tool or reply]\r You MUST call...'  history=0
API call #1: in=35003 out=27 latency=1.3s
API call #2: in=35936 out=44 latency=1.8s
Turn ended: tool_turns=1 response_len=209  (no agent.tool_executor line for mcp_computer_use_computer)
grep agent.log: mcp_computer_use_computer appears only in MCP registration lines, never tool_executor
```

**DEV-024 improvement:** Mandatory first-action block confirmed in gateway payload.
**Still failing:** `mcp_computer_use_computer` never invoked; model now declines immediately vs QA-023 tool-shopping.

Full report: `Docs/agents/reports/TASK-20260708-025-QA01-to-PM01.md`

## QA re-regression evidence (QA-01, TASK-20260708-027, 2026-07-08) — FAIL

After OPS model switch to **`gpt-oss:120b-cloud`** (config verified; revert backup
`config.yaml.bak-revert-qwen3-20260708` present), DEV-024 mandatory block active,
`AllowComputerControl=true`, Victoria primary, **fresh history**:

| Run | Model | `mcp_computer_use_computer`? | Reply |
|---|---|---|---|
| B (authoritative) | `gpt-oss:120b-cloud` | **No** | Declines — claims tool unavailable (~14.1s, `tool_turns=1`) |

Key log lines (session `api-6a716b700da68710`, same session id as QA-025 qwen run, now on gpt-oss):

```
model=gpt-oss:120b-cloud history=0 msg='[MANDATORY FIRST ACTION — …] You MUST call mcp_computer_use_computer…'
Turn ended: tool_turns=1  (no agent.tool_executor line for mcp_computer_use_computer)
MCP registered: mcp_computer_use_computer at gateway start 12:52:46
```

**PM action:** Order OPS revert to `qwen3-coder:480b-cloud` per TASK-026.

Full report: `Docs/agents/reports/TASK-20260708-027-QA01-to-PM01.md`

---
- 16 broken MCP servers disabled; MCP now loads clean (`97 tools / 7 servers / 0 failures`).
- Model left at baseline `gemma4:31b-cloud`. Gateway running on `:8642`.
- Config backup: `%LOCALAPPDATA%\hermes\config.yaml.bak-20260708-pm01`.
