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
            try:
                msg = json.loads(raw)
            except json.JSONDecodeError:
                await ws.send(json.dumps({"type": "error", "payload": {"message": "invalid json"}}))
                continue
            mtype = (msg or {}).get("type", "")
            if mtype == "ping":
                await ws.send(json.dumps({"type": "pong", "payload": msg.get("payload", {})}))
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
