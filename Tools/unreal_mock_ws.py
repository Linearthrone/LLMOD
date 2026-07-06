"""
Minimal WebSocket mock for Unreal protocol smoke tests.
Run: python Tools/unreal_mock_ws.py
Then point House Victoria to ws://127.0.0.1:8888
Requires: pip install websockets
"""
import asyncio
import json
import logging

try:
    import websockets
except ImportError:
    raise SystemExit("Install websockets: pip install websockets")

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("unreal_mock_ws")


async def handler(ws):
    peer = getattr(ws, "remote_address", "?")
    logger.info("Client connected: %s", peer)
    try:
        async for raw in ws:
            if isinstance(raw, (bytes, bytearray)):
                raw = raw.decode("utf-8", errors="replace")
            # House Victoria virtual-environment UI sends plain text (e.g. "status", "get_scene_info").
            if isinstance(raw, str) and not raw.lstrip().startswith("{"):
                cmd = raw.strip()
                logger.info("Plain text command: %s", cmd[:200])
                if cmd.startswith("touch_interact"):
                    logger.info("  -> touch handler")
                elif cmd.startswith("wander"):
                    logger.info("  -> walk/wander handler")
                elif cmd.startswith(("look_at", "capture_scene", "get_scene_info")):
                    logger.info("  -> see handler")
                elif cmd.startswith(("focus_avatar", "set_locomotion")):
                    logger.info("  -> embodiment setup")
                elif cmd.startswith("animate_avatar"):
                    logger.info("  -> talk/anim handler")
                await ws.send(
                    json.dumps(
                        {
                            "type": "status",
                            "scene": "MockPlain",
                            "avatar_count": 0,
                            "fps": 0.0,
                            "rendering": True,
                        }
                    )
                )
                continue
            try:
                msg = json.loads(raw)
            except json.JSONDecodeError:
                await ws.send(json.dumps({"type": "error", "payload": {"message": "invalid json"}}))
                continue
            mtype = (msg or {}).get("type", "")
            if mtype == "ping":
                await ws.send(json.dumps({"type": "pong", "payload": msg.get("payload", {})}))
            elif mtype == "command":
                inner = (msg or {}).get("payload") or {}
                cmd_name = inner.get("name")
                args = inner.get("args") or {}
                if cmd_name == "companion_remote_exchange":
                    user_t = (args.get("user") or "")[:200]
                    asst_t = (args.get("assistant") or "")[:200]
                    corr = args.get("correlation_id") or ""
                    logger.info(
                        "companion_remote_exchange corr=%s user=%r assistant=%r",
                        corr,
                        user_t,
                        asst_t,
                    )
                    await ws.send(
                        json.dumps(
                            {
                                "type": "status",
                                "payload": {
                                    "handled": "companion_remote_exchange",
                                    "correlation_id": corr,
                                },
                            }
                        )
                    )
                else:
                    await ws.send(
                        json.dumps(
                            {
                                "type": "scene",
                                "payload": {"name": "MockLevel", "echo": msg},
                            }
                        )
                    )
            else:
                await ws.send(
                    json.dumps(
                        {
                            "type": "scene",
                            "payload": {"name": "MockLevel", "echo": msg},
                        }
                    )
                )
    except websockets.ConnectionClosed:
        pass
    finally:
        logger.info("Client disconnected: %s", peer)


async def main():
    async with websockets.serve(handler, "127.0.0.1", 8888):
        logger.info("Listening on ws://127.0.0.1:8888")
        await asyncio.Future()


if __name__ == "__main__":
    asyncio.run(main())
