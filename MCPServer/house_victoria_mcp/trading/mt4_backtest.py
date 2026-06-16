"""Historical data loading and backtesting for MT4 bridge (MCP tools)."""

from __future__ import annotations

import json
import os
import struct
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

from .mt4_bridge import (
    COMMAND_FOLDER,
    _load_symbol_map,
    bridge_paths,
    resolve_terminal_path,
)

TIMEFRAME_CODES = {
    "M1": "1",
    "M5": "5",
    "M15": "15",
    "M30": "30",
    "H1": "60",
    "H4": "240",
    "D1": "1440",
    "W1": "10080",
    "MN1": "43200",
}


def _resolve_symbol_candidates(command_root: Path, symbol: str) -> list[str]:
    base = symbol.upper()
    candidates = [base]
    symbol_map = _load_symbol_map(command_root)
    broker = symbol_map.get(base)
    if broker and broker not in candidates:
        candidates.append(broker)
    return candidates


def _read_hst_file(path: Path, symbol: str, timeframe: str, start: datetime, end: datetime) -> list[dict[str, Any]]:
    bars: list[dict[str, Any]] = []
    try:
        data = path.read_bytes()
        offset = 148
        while offset + 60 <= len(data):
            chunk = data[offset : offset + 60]
            time_raw, open_, low, high, close, volume, _, _ = struct.unpack("<qddddqqii", chunk)
            bar_time = datetime.utcfromtimestamp(time_raw)
            offset += 60
            if bar_time < start or bar_time > end:
                if bar_time > end:
                    break
                continue
            bars.append(
                {
                    "time": bar_time.isoformat(),
                    "open": open_,
                    "high": high,
                    "low": low,
                    "close": close,
                    "volume": volume,
                    "symbol": symbol,
                    "time_frame": timeframe,
                }
            )
    except (OSError, struct.error):
        return []
    return bars


def get_historical_bars(
    symbol: str,
    time_frame: str = "H1",
    start_date: Optional[str] = None,
    end_date: Optional[str] = None,
    max_bars: int = 5000,
    configured_path: Optional[str] = None,
) -> dict[str, Any]:
    """Load OHLCV bars from MT4 .hst or bridge CSV files."""
    try:
        paths = bridge_paths(resolve_terminal_path(configured_path))
    except FileNotFoundError as exc:
        return {"success": False, "message": str(exc)}

    tf = time_frame.upper()
    tf_code = TIMEFRAME_CODES.get(tf, "60")
    start = datetime.fromisoformat(start_date) if start_date else datetime.utcnow().replace(
        year=datetime.utcnow().year - 1
    )
    end = datetime.fromisoformat(end_date) if end_date else datetime.utcnow()
    candidates = _resolve_symbol_candidates(paths["command_root"], symbol)

    bars: list[dict[str, Any]] = []
    history_root = paths["terminal"] / "history"
    if history_root.is_dir():
        for broker_dir in history_root.iterdir():
            if not broker_dir.is_dir():
                continue
            for candidate in candidates:
                for hst in (
                    broker_dir / f"{candidate}{tf_code}.hst",
                    broker_dir / candidate / f"{candidate}{tf_code}.hst",
                ):
                    if hst.is_file():
                        bars = _read_hst_file(hst, symbol, tf, start, end)
                        if bars:
                            break
                if bars:
                    break
            if bars:
                break

    if not bars:
        for candidate in candidates:
            csv_path = paths["command_root"] / f"{candidate}_{tf_code}.csv"
            if not csv_path.is_file():
                csv_path = paths["command_root"] / f"{symbol.upper()}_{tf_code}.csv"
            if csv_path.is_file():
                lines = csv_path.read_text(encoding="utf-8").splitlines()
                for line in lines[1:]:
                    parts = line.split(",")
                    if len(parts) < 6:
                        continue
                    try:
                        bar_time = datetime.fromisoformat(parts[0].strip())
                    except ValueError:
                        continue
                    if bar_time < start or bar_time > end:
                        continue
                    bars.append(
                        {
                            "time": bar_time.isoformat(),
                            "open": float(parts[1]),
                            "high": float(parts[2]),
                            "low": float(parts[3]),
                            "close": float(parts[4]),
                            "volume": int(float(parts[5])),
                            "symbol": symbol.upper(),
                            "time_frame": tf,
                        }
                    )

    if not bars:
        return {
            "success": False,
            "symbol": symbol.upper(),
            "time_frame": tf,
            "message": (
                f"No historical data for {symbol.upper()} {tf}. "
                "Call mt4_export_history or download history in MT4 (Tools → History Center)."
            ),
        }

    bars = bars[-max_bars:]
    return {
        "success": True,
        "symbol": symbol.upper(),
        "time_frame": tf,
        "bar_count": len(bars),
        "start": bars[0]["time"],
        "end": bars[-1]["time"],
        "bars": bars,
    }


def _simple_ma(closes: list[float], end: int, period: int) -> float:
    window = closes[end - period + 1 : end + 1]
    return sum(window) / period


def _ema(closes: list[float], end: int, period: int) -> float:
    if end < period - 1:
        return closes[end]
    k = 2.0 / (period + 1)
    ema = _simple_ma(closes, period - 1, period)
    for i in range(period, end + 1):
        ema = closes[i] * k + ema * (1 - k)
    return ema


def _std_dev(closes: list[float], end: int, period: int) -> float:
    mean = _simple_ma(closes, end, period)
    window = closes[end - period + 1 : end + 1]
    var = sum((x - mean) ** 2 for x in window) / period
    return var**0.5


def _compute_rsi(closes: list[float], end: int, period: int) -> float:
    gains = 0.0
    losses = 0.0
    for i in range(end - period + 1, end + 1):
        delta = closes[i] - closes[i - 1]
        if delta >= 0:
            gains += delta
        else:
            losses -= delta
    if losses == 0:
        return 100.0
    rs = gains / losses
    return 100 - 100 / (1 + rs)


def _run_backtest_on_bars(
    bars: list[dict[str, Any]],
    strategy_type: str = "ma_crossover",
    fast_period: int = 10,
    slow_period: int = 30,
    rsi_period: int = 14,
    rsi_oversold: float = 30,
    rsi_overbought: float = 70,
    breakout_period: int = 20,
    macd_fast: int = 12,
    macd_slow: int = 26,
    macd_signal: int = 9,
    bollinger_period: int = 20,
    bollinger_std: float = 2.0,
    stop_loss_pips: float = 0,
    take_profit_pips: float = 0,
    direction: str = "both",
    initial_deposit: float = 10000,
    lot_size: float = 0.01,
    symbol: str = "EURUSD",
) -> dict[str, Any]:
    if len(bars) < 10:
        return {"success": False, "message": "Not enough bars to backtest"}

    closes = [float(b["close"]) for b in bars]
    pip = 0.01 if "JPY" in symbol.upper() else 0.0001
    allows_long = direction.lower() != "short"
    allows_short = direction.lower() != "long"

    balance = initial_deposit
    max_equity = balance
    max_drawdown = 0.0
    trades: list[dict[str, Any]] = []
    open_trade: Optional[dict[str, Any]] = None
    st = strategy_type.lower()

    for i, bar in enumerate(bars):
        signal = "none"
        if st in ("rsi", "rsi_reversal", "rsi_mean_reversion") and i >= rsi_period:
            rsi_now = _compute_rsi(closes, i, rsi_period)
            rsi_prev = _compute_rsi(closes, i - 1, rsi_period)
            if rsi_prev <= rsi_oversold < rsi_now:
                signal = "long"
            elif rsi_prev >= rsi_overbought > rsi_now:
                signal = "short"
        elif st in ("breakout", "donchian") and i >= breakout_period:
            window = bars[i - breakout_period : i]
            if float(bar["close"]) > max(float(b["high"]) for b in window):
                signal = "long"
            elif float(bar["close"]) < min(float(b["low"]) for b in window):
                signal = "short"
        elif st in ("macd", "macd_crossover") and i >= macd_slow + macd_signal:
            macd_line = [
                _ema(closes, j, macd_fast) - _ema(closes, j, macd_slow) if j >= macd_slow else 0.0
                for j in range(i + 1)
            ]
            macd_now = macd_line[i]
            macd_prev = macd_line[i - 1]
            sig_now = _ema(macd_line, i, macd_signal)
            sig_prev = _ema(macd_line, i - 1, macd_signal)
            if macd_prev <= sig_prev < macd_now:
                signal = "long"
            elif macd_prev >= sig_prev > macd_now:
                signal = "short"
        elif st in ("bollinger", "bollinger_mean_reversion", "bb_mean_reversion") and i >= bollinger_period:
            mid = _simple_ma(closes, i, bollinger_period)
            std = _std_dev(closes, i, bollinger_period)
            upper = mid + bollinger_std * std
            lower = mid - bollinger_std * std
            price = closes[i]
            prev = closes[i - 1]
            if prev >= lower > price:
                signal = "long"
            elif prev <= upper < price:
                signal = "short"
        elif st in ("ema_crossover", "ema") and i >= slow_period:
            fast_now = _ema(closes, i, fast_period)
            slow_now = _ema(closes, i, slow_period)
            fast_prev = _ema(closes, i - 1, fast_period)
            slow_prev = _ema(closes, i - 1, slow_period)
            if fast_prev <= slow_prev < fast_now:
                signal = "long"
            elif fast_prev >= slow_prev > fast_now:
                signal = "short"
        elif i >= slow_period:
            fast_now = _simple_ma(closes, i, fast_period)
            slow_now = _simple_ma(closes, i, slow_period)
            fast_prev = _simple_ma(closes, i - 1, fast_period)
            slow_prev = _simple_ma(closes, i - 1, slow_period)
            if fast_prev <= slow_prev < fast_now:
                signal = "long"
            elif fast_prev >= slow_prev > fast_now:
                signal = "short"

        if open_trade:
            close_it = False
            price = float(bar["close"])
            if open_trade["type"] == "buy" and signal == "short":
                close_it = True
            if open_trade["type"] == "sell" and signal == "long":
                close_it = True
            if stop_loss_pips > 0:
                if open_trade["type"] == "buy" and float(bar["low"]) <= open_trade["open"] - stop_loss_pips * pip:
                    close_it = True
                    price = open_trade["open"] - stop_loss_pips * pip
                if open_trade["type"] == "sell" and float(bar["high"]) >= open_trade["open"] + stop_loss_pips * pip:
                    close_it = True
                    price = open_trade["open"] + stop_loss_pips * pip
            if take_profit_pips > 0:
                if open_trade["type"] == "buy" and float(bar["high"]) >= open_trade["open"] + take_profit_pips * pip:
                    close_it = True
                    price = open_trade["open"] + take_profit_pips * pip
                if open_trade["type"] == "sell" and float(bar["low"]) <= open_trade["open"] - take_profit_pips * pip:
                    close_it = True
                    price = open_trade["open"] - take_profit_pips * pip

            if close_it:
                if open_trade["type"] == "buy":
                    profit = (price - open_trade["open"]) * lot_size * 100000
                else:
                    profit = (open_trade["open"] - price) * lot_size * 100000
                open_trade["close"] = price
                open_trade["profit"] = profit
                balance += profit
                trades.append(open_trade)
                open_trade = None

        if open_trade is None:
            if allows_long and signal == "long":
                open_trade = {"type": "buy", "open": float(bar["close"]), "time": bar["time"]}
            elif allows_short and signal == "short":
                open_trade = {"type": "sell", "open": float(bar["close"]), "time": bar["time"]}

        equity = balance
        if open_trade:
            if open_trade["type"] == "buy":
                equity += (float(bar["close"]) - open_trade["open"]) * lot_size * 100000
            else:
                equity += (open_trade["open"] - float(bar["close"])) * lot_size * 100000
        max_equity = max(max_equity, equity)
        max_drawdown = max(max_drawdown, max_equity - equity)

    if open_trade:
        last = bars[-1]
        price = float(last["close"])
        profit = (
            (price - open_trade["open"]) * lot_size * 100000
            if open_trade["type"] == "buy"
            else (open_trade["open"] - price) * lot_size * 100000
        )
        open_trade["close"] = price
        open_trade["profit"] = profit
        balance += profit
        trades.append(open_trade)

    wins = sum(1 for t in trades if t.get("profit", 0) > 0)
    total = len(trades)
    net = balance - initial_deposit

    return {
        "success": True,
        "strategy_type_used": strategy_type,
        "bars_processed": len(bars),
        "total_trades": total,
        "winning_trades": wins,
        "losing_trades": total - wins,
        "win_rate": (wins / total * 100) if total else 0,
        "initial_deposit": initial_deposit,
        "final_balance": balance,
        "net_profit": net,
        "profit_percent": (net / initial_deposit * 100) if initial_deposit else 0,
        "max_drawdown": max_drawdown,
        "max_drawdown_percent": (max_drawdown / max_equity * 100) if max_equity else 0,
        "trades": trades[-50:],
    }


def run_backtest(
    symbol: str,
    time_frame: str = "H1",
    start_date: Optional[str] = None,
    end_date: Optional[str] = None,
    strategy_name: str = "MCPBacktest",
    strategy_type: str = "ma_crossover",
    fast_period: int = 10,
    slow_period: int = 30,
    rsi_period: int = 14,
    rsi_oversold: float = 30,
    rsi_overbought: float = 70,
    breakout_period: int = 20,
    stop_loss_pips: float = 0,
    take_profit_pips: float = 0,
    direction: str = "both",
    initial_deposit: float = 10000,
    lot_size: float = 0.01,
    configured_path: Optional[str] = None,
) -> dict[str, Any]:
    """Run a backtest on MT4 historical data."""
    hist = get_historical_bars(
        symbol=symbol,
        time_frame=time_frame,
        start_date=start_date,
        end_date=end_date,
        configured_path=configured_path,
    )
    if not hist.get("success"):
        return hist

    result = _run_backtest_on_bars(
        hist["bars"],
        strategy_type=strategy_type,
        fast_period=fast_period,
        slow_period=slow_period,
        rsi_period=rsi_period,
        rsi_oversold=rsi_oversold,
        rsi_overbought=rsi_overbought,
        breakout_period=breakout_period,
        stop_loss_pips=stop_loss_pips,
        take_profit_pips=take_profit_pips,
        direction=direction,
        initial_deposit=initial_deposit,
        lot_size=lot_size,
        symbol=symbol,
    )
    result["strategy_name"] = strategy_name
    result["symbol"] = symbol.upper()
    result["time_frame"] = time_frame.upper()

    if result.get("success"):
        try:
            paths = bridge_paths(resolve_terminal_path(configured_path))
            out = paths["command_root"] / f"Backtest_{strategy_name}_{datetime.utcnow().strftime('%Y%m%d%H%M%S')}.json"
            out.write_text(json.dumps(result, indent=2), encoding="utf-8")
            result["result_file"] = str(out)
        except OSError:
            pass

    return result


def export_history(
    symbol: str,
    time_frame: str = "H1",
    start_date: Optional[str] = None,
    end_date: Optional[str] = None,
    timeout_seconds: int = 60,
    configured_path: Optional[str] = None,
) -> dict[str, Any]:
    """Ask the MT4 EA to export OHLCV bars to a bridge CSV file."""
    from .mt4_bridge import get_bridge_status

    status = get_bridge_status(configured_path)
    if not status.get("success"):
        return status
    if not status.get("bridge_active"):
        return {
            "success": False,
            "message": "MT4 bridge EA is not active. Attach HouseVictoriaBridge with AutoTrading enabled.",
        }

    try:
        paths = bridge_paths(resolve_terminal_path(configured_path))
    except FileNotFoundError as exc:
        return {"success": False, "message": str(exc)}

    tf = time_frame.upper()
    tf_code = TIMEFRAME_CODES.get(tf, "60")
    start = datetime.fromisoformat(start_date) if start_date else datetime.utcnow().replace(
        year=datetime.utcnow().year - 1
    )
    end = datetime.fromisoformat(end_date) if end_date else datetime.utcnow()
    base = symbol.upper()

    paths["responses"].mkdir(parents=True, exist_ok=True)
    command_id = f"History_{datetime.now().strftime('%Y%m%d%H%M%S')}_{uuid.uuid4().hex}"
    payload = {
        "Symbol": base,
        "TimeFrame": int(tf_code),
        "StartDate": start.strftime("%Y-%m-%d %H:%M:%S"),
        "EndDate": end.strftime("%Y-%m-%d %H:%M:%S"),
    }
    command_file = paths["command_root"] / f"{command_id}.json"
    response_file = paths["responses"] / f"Response_{command_id}.txt"
    command_file.write_text(json.dumps(payload, indent=2), encoding="utf-8")

    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        if response_file.is_file():
            try:
                data = json.loads(response_file.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                data = {"success": False, "message": response_file.read_text(encoding="utf-8")}
            return {
                "success": bool(data.get("success")),
                "symbol": base,
                "time_frame": tf,
                "bars_exported": int(data.get("bars_exported", 0)),
                "csv_file": data.get("csv_file"),
                "message": data.get("message", ""),
                "command_id": command_id,
            }
        if not command_file.exists():
            break
        time.sleep(0.5)

    return {
        "success": False,
        "symbol": base,
        "message": "Timed out waiting for MT4 history export response.",
        "command_id": command_id,
    }


def _resolve_autonomy_dir() -> Path:
    env = os.getenv("HOUSEVICTORIA_AUTONOMY_PATH")
    if env:
        return Path(env).expanduser().resolve()

    repo_root = Path(__file__).resolve().parents[3]
    app_config = repo_root / "HouseVictoria.App" / "App.config"
    if app_config.is_file():
        text = app_config.read_text(encoding="utf-8")
        import re

        match = re.search(r'AutonomyDataPath["\s]+value="([^"]+)"', text, re.I)
        if match:
            rel = match.group(1).replace("\\", "/")
            for base in (
                repo_root / "HouseVictoria.App" / "bin" / "Release" / "net8.0-windows",
                repo_root / "HouseVictoria.App" / "bin" / "Debug" / "net8.0-windows",
                repo_root,
            ):
                candidate = (base / rel).resolve()
                if candidate.is_dir():
                    return candidate

    for candidate in (
        repo_root / "HouseVictoria.App" / "bin" / "Release" / "net8.0-windows" / "Data" / "Autonomy",
        repo_root / "HouseVictoria.App" / "bin" / "Debug" / "net8.0-windows" / "Data" / "Autonomy",
        repo_root / "Data" / "Autonomy",
    ):
        if candidate.is_dir():
            return candidate

    return repo_root / "Data" / "Autonomy"


def get_market_watch_status() -> dict[str, Any]:
    """Read multi-pair market watch status written by House Victoria (scanner + technical signals)."""
    autonomy_dir = _resolve_autonomy_dir()
    status_file = autonomy_dir / "market-watch-status.json"

    if not status_file.is_file():
        return {
            "success": False,
            "message": (
                "market-watch-status.json not found. Start House Victoria with TradingWatchEnabled "
                "and an attached MT4 bridge EA."
            ),
            "autonomy_path": str(autonomy_dir),
        }

    try:
        data = json.loads(status_file.read_text(encoding="utf-8"))
        data["success"] = True
        data["autonomy_path"] = str(autonomy_dir)
        data["status_file"] = str(status_file)
        return data
    except (OSError, json.JSONDecodeError) as exc:
        return {"success": False, "message": str(exc), "autonomy_path": str(autonomy_dir)}
