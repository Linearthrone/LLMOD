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


def _post_json(path: str, payload: dict[str, Any], timeout_seconds: float) -> dict[str, Any]:
    body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"{BRIDGE_BASE_URL}{path}",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout_seconds + 5) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as ex:
        err_body = ex.read().decode("utf-8", errors="replace")
        return {"ok": False, "error": f"http_{ex.code}", "body": err_body}
    except Exception as ex:
        return {
            "ok": False,
            "error": str(ex),
            "hint": "Start BrowserCaptureBridge (port 17891) and load the Chrome/Edge extension.",
        }


def request_browser_capture(
    *,
    include_screenshot: bool = True,
    include_page_map: bool = True,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    """Ask the browser extension (via bridge) to capture the active tab."""
    return _post_json(
        "/capture",
        {
            "include_screenshot": include_screenshot,
            "include_page_map": include_page_map,
            "timeout_seconds": timeout_seconds,
        },
        timeout_seconds,
    )


def request_browser_action(
    *,
    action: str,
    selector: str | None = None,
    index: int | None = None,
    x: float | None = None,
    y: float | None = None,
    button: str = "left",
    text: str | None = None,
    clear: bool = False,
    key: str | None = None,
    modifiers: list[str] | None = None,
    delta_x: float = 0.0,
    delta_y: float = 0.0,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    """Ask the browser extension to perform a click/type/key/scroll on the active tab."""
    payload: dict[str, Any] = {
        "action": action,
        "button": button,
        "clear": clear,
        "delta_x": delta_x,
        "delta_y": delta_y,
        "timeout_seconds": timeout_seconds,
    }
    if selector is not None:
        payload["selector"] = selector
    if index is not None:
        payload["index"] = index
    if x is not None:
        payload["x"] = x
    if y is not None:
        payload["y"] = y
    if text is not None:
        payload["text"] = text
    if key is not None:
        payload["key"] = key
    if modifiers is not None:
        payload["modifiers"] = modifiers
    return _post_json("/action", payload, timeout_seconds)
