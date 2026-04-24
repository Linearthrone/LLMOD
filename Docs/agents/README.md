# Agent task queue (House Victoria)

This folder coordinates **PM / DEV / OPS / QA** using files:

| Folder | Purpose |
|--------|---------|
| `tasks/` | Outbound task assignments (`TASK-{date}-{id}-PM01-to-{ROLE}.md`) |
| `reports/` | Completion reports (`TASK-{id}-{ROLE}-to-PM01.md`, QA reports, etc.) |
| `log/` | Optional archive for closed task pairs |

**Naming:** match task id between task and report so PM can pair them for archive.

Role playbooks (long-form) remain under `Docs/agents/agents/` (e.g. `PM-01.md`, `DEV-01.md`).

**Strategic goals:** `GOALS-Remote-Companion-and-AI-Home.md` in this directory.
