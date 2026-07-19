# Unreal Editor Drive Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Give Victoria MCP tools to inspect/edit the open Unreal Editor via Epic Remote Control HTTP (:30010).

**Architecture:** `unreal_editor.py` → RC HTTP; register tools on `house_victoria` MCP; AppConfig + env file handoff; Hermes catalog steering.

**Spec:** `Docs/superpowers/specs/2026-07-18-unreal-editor-drive-design.md`

## File map

| File | Responsibility |
|------|----------------|
| `MCPServer/house_victoria_mcp/unreal_editor.py` | RC HTTP client |
| `MCPServer/house_victoria_mcp/server.py` | Register tools + discovery |
| `Tools/unreal_rc_mock.py` | Offline mock |
| `HouseVictoria.Core/Models/PersistenceModels.cs` | AppConfig fields |
| Settings UI + ViewModel + App.xaml.cs | Toggle, URL, env file write |
| `HouseVictoriaToolCatalog.cs` / `HermesAIService.cs` | Steering |
| `Docs/Unreal_Editor_Remote_Control_Setup.md` | Setup + smoke |

## Tasks

- [x] Design spec + this plan
- [x] RC client + mock + smoke
- [x] MCP registration
- [x] AppConfig + settings + env handoff
- [x] Catalog + Hermes
- [x] Setup doc + smoke checklist
