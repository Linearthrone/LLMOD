"""MCP client helpers for the House Victoria browser capture extension bridge."""

from __future__ import annotations

import json
import urllib.error
import urllib.request
from typing import Any

BRIDGE_BASE_URL = "http://127.0.0.1:17891"
DEFAULT_TIMEOUT_SECONDS = 35


def bridge_health() -> dict[str, Any]:
    try:
        with urllib.request.urlopen(f"{BRIDGE_BASE_URL}/health", timeout=3) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except Exception as ex:
        return {"ok": False, "error": str(ex)}


def request_browser_capture(
    *,
    include_screenshot: bool = True,
    include_page_map: bool = True,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    """Ask the browser extension (via bridge) to capture the active tab."""
    payload = json.dumps(
        {
            "include_screenshot": include_screenshot,
            "include_page_map": include_page_map,
            "timeout_seconds": timeout_seconds,
        }
    ).encode("utf-8")
    req = urllib.request.Request(
        f"{BRIDGE_BASE_URL}/capture",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout_seconds + 5) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as ex:
        body = ex.read().decode("utf-8", errors="replace")
        return {"ok": False, "error": f"http_{ex.code}", "body": body}
    except Exception as ex:
        return {
            "ok": False,
            "error": str(ex),
            "hint": "Start BrowserCaptureBridge (port 17891) and load the Chrome/Edge extension.",
        }
