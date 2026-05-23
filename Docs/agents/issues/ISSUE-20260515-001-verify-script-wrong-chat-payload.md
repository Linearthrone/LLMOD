---
issue_id: ISSUE-20260515-001
发现时间: 2026-05-15
严重程度: P2（一般）
状态: 待修复
---

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
