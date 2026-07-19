"""MCP client helpers for Unreal Editor Remote Control HTTP API."""

from __future__ import annotations

import json
import os
import re
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

DEFAULT_RC_URL = "http://127.0.0.1:30010"
DEFAULT_TIMEOUT_SECONDS = 30
ENV_FILE_NAME = "unreal_editor.env"
CAPTURE_DIR_NAME = "unreal_editor_captures"

EDITOR_LEVEL_LIBRARY = "/Script/EditorScriptingUtilities.Default__EditorLevelLibrary"
UNREAL_EDITOR_ENGINE = "/Script/UnrealEd.Default__UnrealEditorSubsystem"

CONSOLE_BLOCKLIST = frozenset(
    {
        "quit",
        "exit",
        "exit_complete",
        "restartlevel",
        "debugcrash",
        "crash",
        "ensure",
        "shutdown",
    }
)

_WRITE_TRUE = frozenset({"1", "true", "yes", "on"})


def _house_victoria_dir() -> Path:
    return Path.home() / ".house_victoria"


def _load_env_file() -> dict[str, str]:
    path = _house_victoria_dir() / ENV_FILE_NAME
    if not path.is_file():
        return {}
    out: dict[str, str] = {}
    try:
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, _, value = line.partition("=")
            out[key.strip()] = value.strip().strip('"').strip("'")
    except OSError:
        return {}
    return out


def _config() -> dict[str, str]:
    """Merge env file with process env (process env wins)."""
    merged = _load_env_file()
    for key in (
        "HOUSE_VICTORIA_UNREAL_RC_URL",
        "HOUSE_VICTORIA_UNREAL_EDITOR_WRITE",
        "HOUSE_VICTORIA_UNREAL_RC_PASS",
    ):
        if key in os.environ and os.environ[key].strip():
            merged[key] = os.environ[key].strip()
    return merged


def get_rc_base_url() -> str:
    cfg = _config()
    url = cfg.get("HOUSE_VICTORIA_UNREAL_RC_URL") or DEFAULT_RC_URL
    return url.rstrip("/")


def writes_allowed() -> bool:
    cfg = _config()
    return cfg.get("HOUSE_VICTORIA_UNREAL_EDITOR_WRITE", "").strip().lower() in _WRITE_TRUE


def ensure_writes_allowed() -> dict[str, Any] | None:
    if writes_allowed():
        return None
    return {
        "ok": False,
        "error": "write_disabled",
        "hint": (
            "Enable Allow Unreal Editor Control in House Victoria Settings "
            "(or set HOUSE_VICTORIA_UNREAL_EDITOR_WRITE=1 / write unreal_editor.env)."
        ),
    }


def _auth_headers() -> dict[str, str]:
    cfg = _config()
    pass_phrase = cfg.get("HOUSE_VICTORIA_UNREAL_RC_PASS", "")
    headers = {"Content-Type": "application/json"}
    if pass_phrase:
        # Epic RC optional HTTP passphrase (Basic-style or custom header varies by version).
        headers["Authorization"] = f"Basic {pass_phrase}"
    return headers


def _request(
    method: str,
    path: str,
    *,
    body: dict[str, Any] | None = None,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    base = get_rc_base_url()
    url = f"{base}{path}"
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers=_auth_headers(),
        method=method.upper(),
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout_seconds) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
            if not raw.strip():
                return {"ok": True, "status": resp.status, "body": {}}
            try:
                parsed = json.loads(raw)
            except json.JSONDecodeError:
                return {"ok": True, "status": resp.status, "body": raw}
            if isinstance(parsed, dict):
                return {"ok": True, "status": resp.status, "body": parsed}
            return {"ok": True, "status": resp.status, "body": parsed}
    except urllib.error.HTTPError as ex:
        err_body = ex.read().decode("utf-8", errors="replace")
        return {
            "ok": False,
            "error": "rc_http_error",
            "status": ex.code,
            "body": err_body,
            "hint": "Check Remote Control is enabled and the object path / payload is valid.",
        }
    except Exception as ex:
        return {
            "ok": False,
            "error": "rc_unreachable",
            "detail": str(ex),
            "hint": (
                f"Start Unreal Editor with Web Remote Control on {base} "
                "(see Docs/Unreal_Editor_Remote_Control_Setup.md)."
            ),
        }


def remote_control_health() -> dict[str, Any]:
    result = _request("GET", "/remote/info", timeout_seconds=5)
    if not result.get("ok"):
        return result
    return {
        "ok": True,
        "rc_url": get_rc_base_url(),
        "writes_allowed": writes_allowed(),
        "info": result.get("body"),
    }


def search_assets(
    query: str = "",
    *,
    package_paths: list[str] | None = None,
    class_names: list[str] | None = None,
    recursive_paths: bool = True,
    recursive_classes: bool = True,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    payload = {
        "Query": query or "",
        "Filter": {
            "PackageNames": [],
            "ClassNames": class_names or [],
            "PackagePaths": package_paths or [],
            "RecursiveClassesExclusionSet": [],
            "RecursivePaths": recursive_paths,
            "RecursiveClasses": recursive_classes,
        },
    }
    result = _request("PUT", "/remote/search/assets", body=payload, timeout_seconds=timeout_seconds)
    if not result.get("ok"):
        return result
    body = result.get("body") or {}
    assets = body.get("Assets") if isinstance(body, dict) else None
    return {"ok": True, "query": query, "assets": assets if assets is not None else body}


def get_property(
    object_path: str,
    property_name: str,
    *,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    payload = {
        "objectPath": object_path,
        "propertyName": property_name,
        "access": "READ_ACCESS",
    }
    result = _request("PUT", "/remote/object/property", body=payload, timeout_seconds=timeout_seconds)
    if not result.get("ok"):
        return result
    return {
        "ok": True,
        "object_path": object_path,
        "property_name": property_name,
        "value": result.get("body"),
    }


def set_property(
    object_path: str,
    property_name: str,
    property_value: Any,
    *,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    blocked = ensure_writes_allowed()
    if blocked:
        return blocked
    payload = {
        "objectPath": object_path,
        "propertyName": property_name,
        "propertyValue": property_value,
        "access": "WRITE_TRANSACTION_ACCESS",
    }
    result = _request("PUT", "/remote/object/property", body=payload, timeout_seconds=timeout_seconds)
    if not result.get("ok"):
        return result
    return {
        "ok": True,
        "object_path": object_path,
        "property_name": property_name,
        "result": result.get("body"),
    }


def call_function(
    object_path: str,
    function_name: str,
    parameters: dict[str, Any] | None = None,
    *,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    blocked = ensure_writes_allowed()
    if blocked:
        return blocked
    payload: dict[str, Any] = {
        "objectPath": object_path,
        "functionName": function_name,
        "parameters": parameters or {},
        "generateTransaction": True,
    }
    result = _request("PUT", "/remote/object/call", body=payload, timeout_seconds=timeout_seconds)
    if not result.get("ok"):
        return result
    return {
        "ok": True,
        "object_path": object_path,
        "function_name": function_name,
        "result": result.get("body"),
    }


def _normalize_console_command(command: str) -> str:
    return re.sub(r"\s+", " ", command.strip())


def _console_blocked(command: str) -> str | None:
    first = command.split(" ", 1)[0].lower()
    if first in CONSOLE_BLOCKLIST:
        return first
    # Block quit/exit even as substrings of first token variants
    if first.startswith("quit") or first.startswith("exit"):
        return first
    return None


def run_console_command(
    command: str,
    *,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    blocked = ensure_writes_allowed()
    if blocked:
        return blocked
    cmd = _normalize_console_command(command)
    if not cmd:
        return {"ok": False, "error": "empty_command"}
    bad = _console_blocked(cmd)
    if bad:
        return {
            "ok": False,
            "error": "console_blocked",
            "command": cmd,
            "hint": f"Command token '{bad}' is blocked for safety.",
        }

    last: dict[str, Any] = {"ok": False, "error": "console_failed", "command": cmd}
    for object_path, function_name, params in (
        (
            "/Script/Engine.Default__KismetSystemLibrary",
            "ExecuteConsoleCommand",
            {"WorldContextObject": None, "Command": cmd, "SpecificPlayer": None},
        ),
        (
            UNREAL_EDITOR_ENGINE,
            "ExecuteConsoleCommand",
            {"Command": cmd},
        ),
    ):
        payload = {
            "objectPath": object_path,
            "functionName": function_name,
            "parameters": params,
            "generateTransaction": False,
        }
        result = _request("PUT", "/remote/object/call", body=payload, timeout_seconds=timeout_seconds)
        if result.get("ok"):
            return {"ok": True, "command": cmd, "via": object_path, "result": result.get("body")}
        last = result
    return last


def spawn_actor(
    asset_path: str,
    *,
    location_x: float = 0.0,
    location_y: float = 0.0,
    location_z: float = 0.0,
    rotation_pitch: float = 0.0,
    rotation_yaw: float = 0.0,
    rotation_roll: float = 0.0,
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    blocked = ensure_writes_allowed()
    if blocked:
        return blocked
    if not asset_path.strip():
        return {"ok": False, "error": "spawn_failed", "detail": "asset_path is required"}

    # SpawnActorFromClass / FromObject — try FromObject with asset path first via EditorLevelLibrary.
    location = {"X": location_x, "Y": location_y, "Z": location_z}
    rotation = {"Pitch": rotation_pitch, "Yaw": rotation_yaw, "Roll": rotation_roll}

    # Load asset then spawn — two-step via EditorAssetLibrary + EditorLevelLibrary when needed.
    # Common pattern: SpawnActorFromObject with soft object path string in parameters.
    payload = {
        "objectPath": EDITOR_LEVEL_LIBRARY,
        "functionName": "SpawnActorFromClass",
        "parameters": {
            "ActorClass": asset_path,
            "Location": location,
            "Rotation": rotation,
        },
        "generateTransaction": True,
    }
    result = _request("PUT", "/remote/object/call", body=payload, timeout_seconds=timeout_seconds)
    if result.get("ok"):
        return {
            "ok": True,
            "asset_path": asset_path,
            "location": location,
            "rotation": rotation,
            "result": result.get("body"),
        }

    # Fallback: SpawnActorFromObject
    payload["functionName"] = "SpawnActorFromObject"
    payload["parameters"] = {
        "ObjectToUse": asset_path,
        "Location": location,
        "Rotation": rotation,
    }
    result2 = _request("PUT", "/remote/object/call", body=payload, timeout_seconds=timeout_seconds)
    if result2.get("ok"):
        return {
            "ok": True,
            "asset_path": asset_path,
            "location": location,
            "rotation": rotation,
            "result": result2.get("body"),
            "via": "SpawnActorFromObject",
        }
    return {
        "ok": False,
        "error": "spawn_failed",
        "asset_path": asset_path,
        "detail": result2.get("body") or result.get("body"),
        "hint": "Enable Editor Scripting Utilities and pass a valid Actor class / asset path.",
    }


def take_screenshot(
    *,
    filename_prefix: str = "viewport",
    timeout_seconds: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    """Request a HighResShot and return the best-effort capture path under ~/.house_victoria."""
    out_dir = _house_victoria_dir() / CAPTURE_DIR_NAME
    out_dir.mkdir(parents=True, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    # HighResShot writes under Saved/Screenshots by default; we also try AutomationLibrary.
    # For MCP consumers, run console HighResShot and report expected Saved path + our dir note.
    cmd = "HighResShot 1"
    console_result = None
    # Screenshot is treated as read-safe (does not require write gate).
    payload = {
        "objectPath": "/Script/Engine.Default__KismetSystemLibrary",
        "functionName": "ExecuteConsoleCommand",
        "parameters": {"WorldContextObject": None, "Command": cmd, "SpecificPlayer": None},
        "generateTransaction": False,
    }
    console_result = _request("PUT", "/remote/object/call", body=payload, timeout_seconds=timeout_seconds)
    marker = out_dir / f"{filename_prefix}-{stamp}.requested.txt"
    marker.write_text(
        f"HighResShot requested at {stamp}\nrc_ok={console_result.get('ok')}\n"
        "Look under the Unreal project Saved/Screenshots/ for the PNG.\n"
        "Copy into this folder if you want a stable MCP path.\n",
        encoding="utf-8",
    )

    # Best-effort: also try Editor screenshot function if present
    shot_call = _request(
        "PUT",
        "/remote/object/call",
        body={
            "objectPath": EDITOR_LEVEL_LIBRARY,
            "functionName": "TakeHighResScreenShot",
            "parameters": {},
            "generateTransaction": False,
        },
        timeout_seconds=timeout_seconds,
    )

    if not console_result.get("ok") and not shot_call.get("ok"):
        return {
            "ok": False,
            "error": "screenshot_failed",
            "console": console_result,
            "editor_call": shot_call,
            "hint": "Ensure Editor is open with Remote Control; try HighResShot manually once.",
        }

    return {
        "ok": True,
        "marker_path": str(marker),
        "capture_dir": str(out_dir),
        "hint": (
            "Viewport capture requested via HighResShot. "
            "PNG lands under the project's Saved/Screenshots/; marker written to capture_dir."
        ),
        "console": console_result,
        "editor_call": shot_call,
    }
