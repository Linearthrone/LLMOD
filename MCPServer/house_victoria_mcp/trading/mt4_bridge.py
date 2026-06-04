"""MetaTrader 4 file-bridge integration for MCP tools."""



from __future__ import annotations



import json

import os

import re

import time

import uuid

from datetime import datetime, timezone

from pathlib import Path

from typing import Any, Optional



COMMAND_FOLDER = "HouseVictoria"

NON_TERMINAL_FOLDERS = {"Common", "Community", "Help"}

TICKET_PATTERN = re.compile(r"Ticket:\s*(\d+)", re.IGNORECASE)

VERIFY_POLL_SECONDS = 10

VERIFY_POLL_INTERVAL = 0.5





def _terminals_root() -> Path:

    return Path.home() / "AppData" / "Roaming" / "MetaQuotes" / "Terminal"





def _is_writable_terminal(path: Path) -> bool:

    mql4_files = path / "MQL4" / "Files"

    if not (path / "MQL4").is_dir():

        return False

    try:

        mql4_files.mkdir(parents=True, exist_ok=True)

        probe = mql4_files / ".hv_write_probe"

        probe.write_text("ok", encoding="utf-8")

        probe.unlink(missing_ok=True)

        return True

    except OSError:

        return False





def _find_terminal_by_origin(install_or_data_path: str) -> Optional[Path]:

    root = _terminals_root()

    if not root.is_dir():

        return None



    target = str(Path(install_or_data_path).resolve()).rstrip("\\/")

    for entry in root.iterdir():

        if not entry.is_dir() or entry.name in NON_TERMINAL_FOLDERS:

            continue

        origin_file = entry / "origin.txt"

        if not origin_file.is_file():

            continue

        origin = origin_file.read_text(encoding="utf-8", errors="ignore").strip().rstrip("\\/")

        if origin.lower() == target.lower() and (entry / "MQL4").is_dir():

            return entry.resolve()

    return None





def _find_best_terminal_with_bridge() -> Optional[Path]:

    root = _terminals_root()

    if not root.is_dir():

        return None



    best: Optional[Path] = None

    best_activity = datetime.min.replace(tzinfo=timezone.utc)



    for entry in root.iterdir():

        if not entry.is_dir() or entry.name in NON_TERMINAL_FOLDERS:

            continue



        bridge = entry / "MQL4" / "Files" / COMMAND_FOLDER

        ea = entry / "MQL4" / "Experts" / "HouseVictoriaBridge.ex4"

        mq4 = entry / "MQL4" / "Experts" / "HouseVictoriaBridge.mq4"

        if not bridge.is_dir() and not ea.is_file() and not mq4.is_file():

            continue



        candidates = [entry]

        if bridge.is_dir():

            candidates.extend(bridge.rglob("*"))

        if ea.is_file():

            candidates.append(ea)

        if mq4.is_file():

            candidates.append(mq4)



        latest = max(

            datetime.fromtimestamp(p.stat().st_mtime, tz=timezone.utc)

            for p in candidates

            if p.exists()

        )

        if latest > best_activity:

            best_activity = latest

            best = entry.resolve()



    return best





def read_mt4_path_from_app_config() -> Optional[str]:

    repo_root = Path(__file__).resolve().parents[3]

    candidates = [

        repo_root / "HouseVictoria.App" / "App.config",

        repo_root / "HouseVictoria.App" / "bin" / "Release" / "net8.0-windows" / "HouseVictoria.App.dll.config",

        repo_root / "HouseVictoria.App" / "bin" / "Debug" / "net8.0-windows" / "HouseVictoria.App.dll.config",

    ]

    for config_path in candidates:

        if not config_path.is_file():

            continue

        text = config_path.read_text(encoding="utf-8", errors="ignore")

        match = re.search(r'key="MT4DataPath"\s+value="([^"]+)"', text, re.IGNORECASE)

        if match:

            return match.group(1).strip()

    return None





def resolve_terminal_path(configured_path: Optional[str] = None) -> Path:

    configured = configured_path or os.getenv("MT4_DATA_PATH") or read_mt4_path_from_app_config()

    if configured:

        normalized = Path(configured).expanduser().resolve()

        if _is_writable_terminal(normalized):

            return normalized

        by_origin = _find_terminal_by_origin(str(normalized))

        if by_origin is not None:

            return by_origin



    best = _find_best_terminal_with_bridge()

    if best is not None:

        return best



    if configured and Path(configured).exists():

        return Path(configured).expanduser().resolve()



    raise FileNotFoundError(

        "Could not resolve MT4 terminal data folder. Set MT4_DATA_PATH or MT4DataPath in App.config."

    )





def _load_symbol_map(command_root: Path) -> dict[str, str]:

    map_file = command_root / "SymbolMap.json"

    if not map_file.is_file():

        return {}

    try:

        data = json.loads(map_file.read_text(encoding="utf-8"))

        if isinstance(data, dict):

            return {str(k).upper(): str(v) for k, v in data.items()}

    except json.JSONDecodeError:

        pass

    return {}





def _resolve_broker_symbol(command_root: Path, symbol: str) -> Optional[str]:

    base = symbol.upper()

    symbol_map = _load_symbol_map(command_root)

    return symbol_map.get(base)





def _resolve_market_data_file(command_root: Path, symbol: str) -> Optional[Path]:

    base = symbol.upper()

    candidates = [command_root / f"MarketData_{base}.txt"]

    mapped = _load_symbol_map(command_root).get(base)

    if mapped:

        candidates.append(command_root / f"MarketData_{mapped.upper()}.txt")

    for path in candidates:

        if path.is_file():

            return path

    return None





def bridge_paths(terminal_path: Optional[Path] = None) -> dict[str, Path]:

    terminal = terminal_path or resolve_terminal_path()

    command_root = terminal / "MQL4" / "Files" / COMMAND_FOLDER

    return {

        "terminal": terminal,

        "command_root": command_root,

        "responses": command_root / "Responses",

    }





def _latest_write_time(path: Path) -> Optional[datetime]:

    if not path.exists():

        return None

    if path.is_file():

        return datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)



    latest: Optional[datetime] = None

    for file in path.rglob("*"):

        if not file.is_file():

            continue

        ts = datetime.fromtimestamp(file.stat().st_mtime, tz=timezone.utc)

        if latest is None or ts > latest:

            latest = ts

    return latest





def _read_open_positions(command_root: Path) -> list[dict[str, Any]]:

    file_path = command_root / "OpenPositions.json"

    if not file_path.is_file():

        return []

    try:

        raw = json.loads(file_path.read_text(encoding="utf-8"))

        if isinstance(raw, list):

            return [p for p in raw if isinstance(p, dict)]

    except json.JSONDecodeError:

        pass

    return []





def _position_has_ticket(positions: list[dict[str, Any]], ticket: int) -> bool:

    for pos in positions:

        try:

            if int(pos.get("Ticket", -1)) == ticket:

                return True

        except (TypeError, ValueError):

            continue

    return False





def verify_ticket(

    ticket: int,

    timeout_seconds: int = VERIFY_POLL_SECONDS,

    configured_path: Optional[str] = None,

) -> dict[str, Any]:

    """Confirm a ticket exists in OpenPositions.json (written by the EA)."""

    paths = bridge_paths(resolve_terminal_path(configured_path))

    deadline = time.time() + timeout_seconds

    while time.time() < deadline:

        positions = _read_open_positions(paths["command_root"])

        if _position_has_ticket(positions, ticket):

            match = next(p for p in positions if int(p.get("Ticket", -1)) == ticket)

            return {

                "success": True,

                "verified": True,

                "ticket": ticket,

                "position": match,

            }

        time.sleep(VERIFY_POLL_INTERVAL)



    return {

        "success": False,

        "verified": False,

        "ticket": ticket,

        "message": f"Ticket {ticket} not found in OpenPositions.json within {timeout_seconds}s.",

    }





def get_bridge_status(configured_path: Optional[str] = None) -> dict[str, Any]:

    try:

        paths = bridge_paths(resolve_terminal_path(configured_path))

    except FileNotFoundError as exc:

        return {"success": False, "message": str(exc)}



    latest = _latest_write_time(paths["command_root"])

    active = latest is not None and (datetime.now(timezone.utc) - latest).total_seconds() <= 30



    account_file = paths["command_root"] / "AccountInfo.json"

    account = None

    if account_file.is_file():

        try:

            account = json.loads(account_file.read_text(encoding="utf-8"))

        except json.JSONDecodeError:

            account = None



    symbol_map = _load_symbol_map(paths["command_root"])



    return {

        "success": True,

        "terminal_path": str(paths["terminal"]),

        "bridge_active": active,

        "bridge_last_activity_utc": latest.isoformat() if latest else None,

        "account": account,

        "symbol_map": symbol_map,

    }





def list_symbols(configured_path: Optional[str] = None) -> dict[str, Any]:

    paths = bridge_paths(resolve_terminal_path(configured_path))

    symbol_map = _load_symbol_map(paths["command_root"])

    available: list[str] = []

    list_file = paths["command_root"] / "SymbolsAvailable.json"

    if list_file.is_file():

        try:

            raw = json.loads(list_file.read_text(encoding="utf-8"))

            if isinstance(raw, list):

                available = [str(s) for s in raw]

        except json.JSONDecodeError:

            pass



    if not symbol_map and not available:

        return {

            "success": False,

            "symbol_map": {},

            "symbols_available": [],

            "message": (

                "No symbol files from MT4 yet. Attach HouseVictoriaBridge to a chart "

                "with AutoTrading enabled, then retry mt4_list_symbols."

            ),

        }



    return {

        "success": True,

        "symbol_map": symbol_map,

        "symbols_available": available,

        "hint": "Use base symbols like EURUSD; the EA resolves broker suffixes automatically.",

    }





def get_market_data(symbol: str, configured_path: Optional[str] = None) -> dict[str, Any]:

    paths = bridge_paths(resolve_terminal_path(configured_path))

    file_path = _resolve_market_data_file(paths["command_root"], symbol)

    if file_path is None:

        symbol_map = _load_symbol_map(paths["command_root"])

        hint = ""

        if symbol.upper() in symbol_map:

            hint = f" Broker symbol: {symbol_map[symbol.upper()]}."

        elif not symbol_map:

            hint = " Call mt4_list_symbols first to discover broker symbol names."

        return {

            "success": False,

            "message": f"No market data file for {symbol.upper()}.{hint} Recompile/reattach HouseVictoriaBridge EA.",

            "symbol_map": symbol_map,

        }



    parts = file_path.read_text(encoding="utf-8").strip().split(",")

    if len(parts) < 2:

        return {"success": False, "message": f"Invalid market data format in {file_path.name}"}



    bid = float(parts[0])

    ask = float(parts[1])

    spread = float(parts[2]) if len(parts) > 2 else ask - bid

    broker_symbol = _load_symbol_map(paths["command_root"]).get(symbol.upper())

    return {

        "success": True,

        "symbol": symbol.upper(),

        "broker_symbol": broker_symbol,

        "bid": bid,

        "ask": ask,

        "spread": spread,

        "updated_utc": datetime.fromtimestamp(file_path.stat().st_mtime, tz=timezone.utc).isoformat(),

    }





def get_open_positions(configured_path: Optional[str] = None) -> dict[str, Any]:

    paths = bridge_paths(resolve_terminal_path(configured_path))

    positions = _read_open_positions(paths["command_root"])

    return {"success": True, "positions": positions}





def _parse_execution_response(command_id: str, response_text: str) -> dict[str, Any]:

    message = response_text.strip()



    # Structured JSON response from newer EA builds

    if message.startswith("{"):

        try:

            data = json.loads(message)

            ticket_raw = data.get("ticket") or data.get("Ticket")

            ticket = int(ticket_raw) if ticket_raw is not None else None

            reported_success = bool(data.get("success", False))

            broker_symbol = data.get("broker_symbol") or data.get("BrokerSymbol")

            return {

                "success": reported_success,

                "message": str(data.get("message") or message),

                "ticket": ticket,

                "broker_symbol": broker_symbol,

                "command_id": command_id,

                "response_format": "json",

            }

        except (json.JSONDecodeError, TypeError, ValueError):

            pass



    success = (
        "executed successfully" in message.lower()
        or "closed successfully" in message.lower()
    )

    ticket_match = TICKET_PATTERN.search(message)

    ticket = int(ticket_match.group(1)) if ticket_match else None

    broker_symbol = None

    if "->" in message:

        arrow = message.split("Symbol:", 1)

        if len(arrow) > 1:

            broker_symbol = arrow[1].strip().split()[0] if arrow[1].strip() else None



    return {

        "success": success,

        "message": message,

        "ticket": ticket,

        "broker_symbol": broker_symbol,

        "command_id": command_id,

        "response_format": "text",

    }





def _apply_ticket_verification(

    result: dict[str, Any],

    command_root: Path,

    verify_timeout_seconds: int,

) -> dict[str, Any]:

    ticket = result.get("ticket")

    if not result.get("success") or ticket is None:

        result["verified"] = False

        return result



    deadline = time.time() + verify_timeout_seconds

    while time.time() < deadline:

        positions = _read_open_positions(command_root)

        if _position_has_ticket(positions, int(ticket)):

            match = next(p for p in positions if int(p.get("Ticket", -1)) == int(ticket))

            result["verified"] = True

            result["position"] = match

            if not result.get("broker_symbol") and match.get("Symbol"):

                result["broker_symbol"] = match["Symbol"]

            return result

        time.sleep(VERIFY_POLL_INTERVAL)



    result["success"] = False

    result["verified"] = False

    result["message"] = (

        f"Ghost execution rejected: EA reported ticket {ticket} but it never appeared "

        f"in OpenPositions.json within {verify_timeout_seconds}s. "

        f"Original response: {result.get('message', '')}"

    )

    return result





def execute_trade(

    symbol: str,

    trade_type: int,

    volume: float,

    stop_loss: Optional[float] = None,

    take_profit: Optional[float] = None,

    timeout_seconds: int = 30,

    verify_timeout_seconds: int = VERIFY_POLL_SECONDS,

    configured_path: Optional[str] = None,

) -> dict[str, Any]:

    status = get_bridge_status(configured_path)

    if not status.get("success"):

        return status

    if not status.get("bridge_active"):

        return {

            "success": False,

            "message": "MT4 bridge EA is not active. Attach HouseVictoriaBridge to a chart with AutoTrading enabled.",

        }



    if not symbol or volume <= 0:

        return {"success": False, "message": "symbol and positive volume are required"}



    paths = bridge_paths(resolve_terminal_path(configured_path))

    symbol_map = _load_symbol_map(paths["command_root"])

    base = symbol.upper()

    broker_hint = symbol_map.get(base)



    if not broker_hint and not (paths["command_root"] / "SymbolsAvailable.json").is_file():

        return {

            "success": False,

            "message": (

                f"Cannot resolve {base}: symbol map unavailable. "

                "Call mt4_list_symbols first, or add the symbol to Market Watch in MT4."

            ),

            "symbol_map": symbol_map,

        }



    paths["responses"].mkdir(parents=True, exist_ok=True)



    command_id = f"Trade_{datetime.now().strftime('%Y%m%d%H%M%S')}_{uuid.uuid4().hex}"

    payload = {

        "Symbol": base,

        "Type": int(trade_type),

        "Volume": float(volume),

    }

    if stop_loss is not None:

        payload["StopLoss"] = float(stop_loss)

    if take_profit is not None:

        payload["TakeProfit"] = float(take_profit)



    command_file = paths["command_root"] / f"{command_id}.json"

    response_file = paths["responses"] / f"Response_{command_id}.txt"

    command_file.write_text(json.dumps(payload, indent=2), encoding="utf-8")



    deadline = time.time() + timeout_seconds

    started = time.time()

    while time.time() < deadline:

        if response_file.is_file():

            result = _parse_execution_response(command_id, response_file.read_text(encoding="utf-8"))

            if broker_hint and not result.get("broker_symbol"):

                result["broker_symbol"] = broker_hint

            result["requested_symbol"] = base

            return _apply_ticket_verification(result, paths["command_root"], verify_timeout_seconds)



        if not command_file.exists():

            for candidate in sorted(

                paths["responses"].glob("Response_*.txt"),

                key=lambda p: p.stat().st_mtime,

                reverse=True,

            ):

                if candidate.stat().st_mtime >= started - 1:

                    result = _parse_execution_response(command_id, candidate.read_text(encoding="utf-8"))

                    if broker_hint and not result.get("broker_symbol"):

                        result["broker_symbol"] = broker_hint

                    result["requested_symbol"] = base

                    return _apply_ticket_verification(result, paths["command_root"], verify_timeout_seconds)



        time.sleep(0.5)



    if not command_file.exists():

        return {

            "success": False,

            "command_id": command_id,

            "verified": False,

            "message": "Trade command consumed by MT4 but no response file appeared.",

        }



    return {

        "success": False,

        "command_id": command_id,

        "verified": False,

        "message": "Timed out waiting for MT4 EA response.",

    }





def _apply_close_verification(

    result: dict[str, Any],

    command_root: Path,

    verify_timeout_seconds: int,

) -> dict[str, Any]:

    ticket = result.get("ticket")

    if not result.get("success") or ticket is None:

        result["verified"] = False

        return result



    deadline = time.time() + verify_timeout_seconds

    while time.time() < deadline:

        positions = _read_open_positions(command_root)

        if not _position_has_ticket(positions, int(ticket)):

            result["verified"] = True

            result["closed"] = True

            return result

        time.sleep(VERIFY_POLL_INTERVAL)



    result["success"] = False

    result["verified"] = False

    result["closed"] = False

    result["message"] = (

        f"Close not verified: ticket {ticket} still in OpenPositions.json after "

        f"{verify_timeout_seconds}s. Original response: {result.get('message', '')}"

    )

    return result





def close_position(

    ticket: int,

    timeout_seconds: int = 30,

    verify_timeout_seconds: int = VERIFY_POLL_SECONDS,

    configured_path: Optional[str] = None,

) -> dict[str, Any]:

    status = get_bridge_status(configured_path)

    if not status.get("success"):

        return status

    if not status.get("bridge_active"):

        return {

            "success": False,

            "message": "MT4 bridge EA is not active. Attach HouseVictoriaBridge to a chart with AutoTrading enabled.",

        }



    if ticket <= 0:

        return {"success": False, "message": "ticket must be a positive integer"}



    paths = bridge_paths(resolve_terminal_path(configured_path))

    positions = _read_open_positions(paths["command_root"])

    if not _position_has_ticket(positions, ticket):

        return {

            "success": False,

            "message": f"Ticket {ticket} not found in OpenPositions.json (House Victoria magic).",

            "ticket": ticket,

            "open_positions": positions,

        }



    paths["responses"].mkdir(parents=True, exist_ok=True)



    command_id = f"Close_{datetime.now().strftime('%Y%m%d%H%M%S')}_{uuid.uuid4().hex}"

    payload = {"Ticket": int(ticket)}

    command_file = paths["command_root"] / f"{command_id}.json"

    response_file = paths["responses"] / f"Response_{command_id}.txt"

    command_file.write_text(json.dumps(payload, indent=2), encoding="utf-8")



    deadline = time.time() + timeout_seconds

    started = time.time()

    while time.time() < deadline:

        if response_file.is_file():

            result = _parse_execution_response(command_id, response_file.read_text(encoding="utf-8"))

            result["ticket"] = ticket

            return _apply_close_verification(result, paths["command_root"], verify_timeout_seconds)



        if not command_file.exists():

            for candidate in sorted(

                paths["responses"].glob("Response_*.txt"),

                key=lambda p: p.stat().st_mtime,

                reverse=True,

            ):

                if candidate.stat().st_mtime >= started - 1:

                    result = _parse_execution_response(command_id, candidate.read_text(encoding="utf-8"))

                    result["ticket"] = ticket

                    return _apply_close_verification(

                        result, paths["command_root"], verify_timeout_seconds

                    )



        time.sleep(0.5)



    if not command_file.exists():

        return {

            "success": False,

            "command_id": command_id,

            "verified": False,

            "closed": False,

            "ticket": ticket,

            "message": "Close command consumed by MT4 but no response file appeared.",

        }



    return {

        "success": False,

        "command_id": command_id,

        "verified": False,

        "closed": False,

        "ticket": ticket,

        "message": "Timed out waiting for MT4 EA close response. Recompile HouseVictoriaBridge.mq4 if close is unsupported.",

    }


