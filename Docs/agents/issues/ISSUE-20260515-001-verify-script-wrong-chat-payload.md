---
issue_id: ISSUE-20260515-001
发现时间: 2026-05-15
严重程度: P2（一般）
状态: 已修复并关闭（PM-01 2026-07-08）。DEV 修复 TASK-016，QA 实机回归 TASK-019 PASS（真实 :17890，无 message_required）。016+019 已归档 log/。
verified_by: QA-01
verified: 2026-07-08
closed_by: PM-01
closed: 2026-07-08
---

## QA 实机回归结论（QA-01, 2026-07-08 · TASK-20260708-019）

**PASS（真机）。** 启动 Release 版 `HouseVictoria.App.exe`，`127.0.0.1:17890` 起监听（pid 52120），
对真实 Remote Companion 端点运行 `scripts/Verify-HouseVictoriaStack.ps1`：

- `[remote-health] 200 {"ok":true,"service":"house-victoria-remote","version":2}`（真实 host 签名）
- `[remote-chat-short-token] PASS HTTP 401 {"error":"unauthorized"}`
- `[remote-chat-valid-token] PASS 200`（真实 LLM 回复，非 mock 定值），**全程无 `message_required`**
- 脚本 `EXITCODE: 0`，证据追加至 `tmpcode/qa-stack-evidence.txt`（第 109-116 行）
- 测试后已关闭该 app 实例，恢复测试前状态。

原症状（valid-token 误报 `message_required`）已消除。详见
`Docs/agents/reports/TASK-20260708-019-QA01-to-PM01.md`。
（注：`[comfy-checkpoint-metadata] NOT_REACHABLE` 为 ComfyUI 未运行，属本 issue 范围外。）

## 问题描述

`scripts/Verify-HouseVictoriaStack.ps1` 在 `[remote-chat-valid-token]` 步骤发送的请求体为 OpenAI 风格 `messages` 数组，但 Remote Companion API 期望字段为 `message`（见 `RemoteCompanionWebHost.cs`）。导致在 app 已监听且 token 有效时，脚本仍输出 `FAIL HTTP 400 {"error":"message_required"}`，不能真实验证鉴权后的聊天链路。

## 复现步骤

1. 启动 `HouseVictoria.App.exe`，确认 `17890` 在监听。
2. 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Verify-HouseVictoriaStack.ps1`
3. 观察 `[remote-chat-valid-token] FAIL HTTP 400 {"error":"message_required"}`

## 期望结果

脚本使用 `{"message":"ping"}`（或与 `RemoteChatRequest` 一致的 JSON），在 token 有效时得到 `200` 及 `reply`，或明确的下游错误（如 LLM/联系人未配置），且日志行应反映真实结果而非误报 `message_required`。

## 实际结果

脚本固定发送 `{"messages":[{"role":"user","content":"ping"}]}`，服务端返回 `400 message_required`。

## 截图/日志

手动补测（正确 payload）：

```
POST http://127.0.0.1:17890/api/remote/v1/chat
Authorization: Bearer <App.config RemoteCompanionApiToken>
Body: {"message":"ping"}
=> HTTP 400 {"error":"No AI contact found. Create one in the app or set RemoteCompanionAiContactId."}
```

（说明：鉴权已通过，失败点为业务配置，非 socket/401。）

## 影响范围

- QA 自动化冒烟对 `[remote-chat-valid-token]` 的结论易误导为 FAIL
- PM/DEV 可能误判 Remote chat 未就绪
