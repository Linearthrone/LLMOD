---
type: pm_dispatch
task_id: "20260421-010"
from: PM-01
to: DEV-01, OPS-01, QA-01
status: sent
created: 2026-04-21
project: House Victoria
---

# PM expedite dispatch — execute now

## Immediate command

All roles execute in this order:

1. **DEV-01 (P0):** complete `TASK-20260421-008-PM01-to-DEV01.md` now and provide loopback success evidence (`health` + authenticated `chat`).
2. **OPS-01:** the moment DEV confirms listener live, continue `TASK-20260421-005-PM01-to-OPS01.md` (tunnel lane verification + runbook delta).
3. **QA-01:** after OPS confirmation, continue `TASK-20260421-006-PM01-to-QA01.md` (API rerun + Android smoke).

## Current PM-controlled status

- `TASK-20260421-008-PM01-to-DEV01.md` → `in_progress`
- `TASK-20260421-005-PM01-to-OPS01.md` → `blocked` (waiting on 008)
- `TASK-20260421-006-PM01-to-QA01.md` → `blocked` (waiting on 008/005)

## Escalation rule

If DEV-008 cannot make listener live in current window, DEV must return root cause + patch plan in report so PM can re-route to architecture-level fix immediately.
