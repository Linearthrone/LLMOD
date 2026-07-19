#!/usr/bin/env python3
"""Minimal Unreal Remote Control HTTP mock for offline MCP smoke tests.

Listens on http://127.0.0.1:30010 by default (same as Epic Web Remote Control).

Usage:
  python Tools/unreal_rc_mock.py
  python Tools/unreal_rc_mock.py --port 30010
"""

from __future__ import annotations

import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any
from urllib.parse import urlparse


class RcMockHandler(BaseHTTPRequestHandler):
    store: dict[str, Any] = {
        "Cube.RelativeLocation": {"X": 0.0, "Y": 0.0, "Z": 100.0},
    }

    def log_message(self, fmt: str, *args: Any) -> None:
        print(f"[unreal_rc_mock] {self.command} {self.path} — {fmt % args}")

    def _read_json(self) -> dict[str, Any]:
        length = int(self.headers.get("Content-Length") or 0)
        if length <= 0:
            return {}
        raw = self.rfile.read(length).decode("utf-8", errors="replace")
        try:
            data = json.loads(raw)
            return data if isinstance(data, dict) else {}
        except json.JSONDecodeError:
            return {}

    def _send(self, code: int, body: Any) -> None:
        payload = json.dumps(body).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self) -> None:  # noqa: N802
        path = urlparse(self.path).path
        if path in ("/remote/info", "/remote"):
            self._send(
                200,
                {
                    "HttpServerName": "HouseVictoriaUnrealRcMock",
                    "Version": "mock-1.0",
                    "Ok": True,
                },
            )
            return
        self._send(404, {"error": "not_found", "path": path})

    def do_PUT(self) -> None:  # noqa: N802
        path = urlparse(self.path).path
        body = self._read_json()

        if path == "/remote/search/assets":
            query = (body.get("Query") or body.get("query") or "").lower()
            assets = [
                {
                    "Name": "SM_Cube",
                    "ObjectPath": "/Game/Geometry/Meshes/1M_Cube.1M_Cube",
                    "AssetClass": "StaticMesh",
                },
                {
                    "Name": "BP_MHC_Victoria",
                    "ObjectPath": "/Game/Characters/BP_MHC_Victoria.BP_MHC_Victoria",
                    "AssetClass": "Blueprint",
                },
            ]
            if query:
                assets = [a for a in assets if query in a["Name"].lower() or query in a["ObjectPath"].lower()]
            self._send(200, {"Assets": assets})
            return

        if path == "/remote/object/property":
            obj = body.get("objectPath") or body.get("ObjectPath") or ""
            prop = body.get("propertyName") or body.get("PropertyName") or ""
            access = (body.get("access") or body.get("Access") or "READ_ACCESS").upper()
            key = f"{obj}.{prop}" if obj and prop else prop
            if "WRITE" in access:
                value = body.get("propertyValue", body.get("PropertyValue"))
                self.store[key] = value
                self._send(200, {prop: value})
                return
            value = self.store.get(key, {"X": 1.0, "Y": 2.0, "Z": 3.0} if "Location" in prop else True)
            self._send(200, {prop: value} if prop else value)
            return

        if path == "/remote/object/call":
            fn = body.get("functionName") or body.get("FunctionName") or ""
            params = body.get("parameters") or body.get("Parameters") or {}
            if fn in ("ExecuteConsoleCommand", "TakeHighResScreenShot"):
                self._send(200, {"ReturnValue": True, "Command": params.get("Command")})
                return
            if fn in ("SpawnActorFromClass", "SpawnActorFromObject"):
                self._send(
                    200,
                    {
                        "ReturnValue": "/Game/Maps/Mock.Mock:PersistentLevel.SpawnedActor_0",
                        "parameters": params,
                    },
                )
                return
            self._send(200, {"ReturnValue": True, "functionName": fn, "parameters": params})
            return

        self._send(404, {"error": "not_found", "path": path})


def main() -> None:
    parser = argparse.ArgumentParser(description="Mock Unreal Remote Control HTTP API")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=30010)
    args = parser.parse_args()
    server = ThreadingHTTPServer((args.host, args.port), RcMockHandler)
    print(f"Unreal RC mock listening on http://{args.host}:{args.port}")
    print("Endpoints: GET /remote/info, PUT /remote/search/assets|object/property|object/call")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
