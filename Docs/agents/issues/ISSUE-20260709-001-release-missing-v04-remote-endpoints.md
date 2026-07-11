---
type: issue
issue_id: "20260709-001"
title: "Running Release HouseVictoria.App missing v0.4 remote companion API routes"
severity: P1
status: Closed (2026-07-09 — QA TASK-20260709-006: all v0.4 endpoints pass)
qa_task: TASK-20260709-006
qa_last_run: 2026-07-09
qa_result: PASS
owner: OPS-01 / DEV-01
---

# ISSUE · Release binary missing v0.4 remote endpoints

**[Fully closed 2026-07-09]** QA TASK-20260709-006 re-run: all endpoints pass (5/5). See `QA-REPORT-20260709-02.md`.

## Summary

QA regression TASK-20260709-003 found that the **live** `HouseVictoria.App` process on port **17890** is an **older Release build** that does not register v0.4 remote companion routes added in TASK-20260709-002. Source code contains the routes; the running executable returns **404**.

## Evidence (live, 2026-07-09)

Process:

```
Id          : 94404
ProcessName : HouseVictoria.App
Path        : C:\Users\kurtw\LLMOD\LLMOD-max-master\HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe
```

| Endpoint | Expected | Actual |
|----------|----------|--------|
| `GET /api/remote/v1/health` | 200 | **200** `{"ok":true,"service":"house-victoria-remote","version":2}` |
| `GET /api/remote/v1/system/status` (Bearer token) | 200 + metrics | **404 Not Found** |
| `GET /api/remote/v1/media/models` | 200 | **404** |
| `POST /api/remote/v1/media/generate` | 200 or 400 | **404** |
| `POST /api/remote/v1/chat-image` | 200 or 400 | **404** |
| `GET /api/remote/v1/contacts` | 200 | **200** (legacy route works) |

Source defines missing routes in `HouseVictoria.App/RemoteCompanion/RemoteCompanionWebHost.cs` (e.g. `system/status` at line 155, `media/models` at 167, `chat-image` at 241).

`dotnet build HouseVictoria.Services` succeeds (0 errors) — compile is fine; **deploy/restart** is the gap.

## Impact

- **TC-C01** system monitor on Android home cannot load metrics (API 404).
- **TC-C05** MediaGen cannot generate via remote API.
- **TC-C06** chat image attach cannot reach PC.
- **TC-C07** persona "draw a sunset" image reply blocked (chat-image 404; chat alone timed out at 30s).
- **TC-C08** fails per acceptance criteria (`system/status` must return 200).

## Recommended fix

1. **OPS:** Stop stale Release `HouseVictoria.App`, rebuild from current `master` (`dotnet build -c Release` or run Debug), restart.
2. Verify after restart:
   ```powershell
   $h = @{ Authorization = "Bearer REDACTED_TEST_TOKEN_2026" }
   Invoke-WebRequest "http://127.0.0.1:17890/api/remote/v1/system/status" -Headers $h -UseBasicParsing
   ```
   Expect **200** with CPU/GPU/RAM/uptime JSON.
3. **QA:** Re-run TASK-20260709-003 matrix.

## Related

- QA report: `Docs/agents/reports/QA-REPORT-20260709-01.md`
- DEV completion: `Docs/agents/reports/TASK-20260709-002-DEV01-to-PM01.md`
