"""Main MCP Server implementation for House Victoria."""

import anyio
import asyncio
import json
import sqlite3
from pathlib import Path
from typing import Any, Optional, Dict

from mcp.server.fastmcp import FastMCP

from .config import get_config
from .logger import get_logger
from .memory import MemoryStorage, MemoryManager
from .tt import TaskManager, WorkflowEngine, ProgressTracker
from .agent import CognitiveAgent
from .trading.mt4_backtest import export_history, get_historical_bars, get_market_watch_status, run_backtest
from .trading.mt4_bridge import (
    close_position,
    execute_trade,
    get_bridge_status,
    get_market_data,
    get_open_positions,
    list_symbols,
    verify_ticket,
)

logger = get_logger("main")


# Create MCP server instance
mcp = FastMCP(
    name="house-victoria",
    instructions="""
    House Victoria is an advanced MCP server with persistent memory, 
    complex tools, and specialized data banks for WPF desktop applications.
    
    Features:
    - Persistent Memory: Store and retrieve information across sessions
    - Data Banks: Organized storage for projects, knowledge, resources
    - Complex Tools: Data processing, web operations, system tasks
    - Task Tracking: Workflow management and progress monitoring
    - TT Support: Complete task and workflow management system
    """,
)

# Tool registry for HTTP wrapper access
_tool_functions: dict = {}


def get_tool_registry():
    """Get the tool function registry for HTTP wrapper."""
    return _tool_functions.copy()


async def call_tool_by_name(tool_name: str, **kwargs):
    """Call a tool by name with parameters."""
    if tool_name in _tool_functions:
        tool_func = _tool_functions[tool_name]
        if asyncio.iscoroutinefunction(tool_func):
            return await tool_func(**kwargs)
        else:
            return tool_func(**kwargs)
    else:
        raise ValueError(f"Tool '{tool_name}' not found. Available tools: {list(_tool_functions.keys())}")


async def create_server():
    """Create and configure the MCP server."""

    # Initialize memory system
    logger.info("Initializing memory system...")
    storage = MemoryStorage()
    await storage.initialize()
    memory_manager = MemoryManager(storage)

    # Initialize TT (Task & Workflow) system
    logger.info("Initializing TT system...")
    task_manager = TaskManager()
    workflow_engine = WorkflowEngine(task_manager)
    progress_tracker = ProgressTracker()

    # Initialize cognitive agent (Python-side reference implementation)
    logger.info("Initializing cognitive agent...")

    async def _tool_executor(tool_name: str, **kwargs: Any) -> Any:
        """Adapter that lets the agent call other MCP tools by name."""
        return await call_tool_by_name(tool_name, **kwargs)

    agent = CognitiveAgent(
        memory_manager=memory_manager,
        workflow_engine=workflow_engine,
        tool_executor=_tool_executor,
    )

    # Register tool categories
    await register_memory_tools(mcp, memory_manager)
    await register_data_bank_tools(mcp, storage)
    await register_system_tools(mcp)
    await register_trading_tools(mcp)
    await register_tt_tools(mcp, task_manager, workflow_engine, progress_tracker)
    await register_agent_tools(mcp, agent)
    
    logger.info("Server creation complete")
    return mcp


async def register_memory_tools(mcp_server: FastMCP, memory_mgr: MemoryManager):
    """Register memory-related tools."""

    @mcp_server.tool()
    async def memory_store(
        value: str,
        key: str | None = None,
        category: str | None = None,
        importance: float = 1.0,
        metadata: dict | None = None,
    ) -> dict:
        """Store information in persistent memory.
        
        Args:
            value: The value to store (will be converted to string)
            key: Optional custom key. Auto-generated if not provided
            category: Category for organization (e.g., 'project', 'user', 'system')
            importance: Importance score from 0.0 to 1.0 (default: 1.0)
            metadata: Additional metadata dictionary
        
        Returns:
            Dictionary with the key and storage information
        """
        stored_key = await memory_mgr.remember(
            value=value,
            key=key,
            metadata=metadata,
            category=category,
            importance=importance,
        )
        
        return {
            "success": True,
            "key": stored_key,
            "category": category,
            "importance": importance,
        }
    
    # Register tool function for HTTP wrapper
    _tool_functions["memory_store"] = memory_store

    @mcp_server.tool()
    async def memory_retrieve(key: str) -> dict:
        """Retrieve information from persistent memory by key.
        
        Args:
            key: The memory key to retrieve
        
        Returns:
            Dictionary with the retrieved value or error
        """
        value = await memory_mgr.recall(key)
        
        if value is None:
            return {
                "success": False,
                "error": "Memory not found",
                "key": key,
            }
        
        return {
            "success": True,
            "key": key,
            "value": value,
        }

    @mcp_server.tool()
    async def memory_search(
        query: str,
        category: str | None = None,
        limit: int = 10,
    ) -> list:
        """Search persistent memory for information.
        
        Args:
            query: Search query string
            category: Optional category filter
            limit: Maximum number of results (default: 10)
        
        Returns:
            List of matching memory entries
        """
        results = await memory_mgr.search_memory(
            query=query,
            category=category,
            limit=limit,
        )
        
        return [
            {
                "key": r["key"],
                "value": r["value"],
                "category": r["category"],
                "importance": r["importance"],
            }
            for r in results
        ]

    @mcp_server.tool()
    async def memory_stats() -> dict:
        """Get statistics about the persistent memory system.
        
        Returns:
            Dictionary with memory statistics
        """
        stats = await memory_mgr.get_memory_stats()
        return stats

    @mcp_server.tool()
    async def memory_conversation_log(
        session_id: str,
        role: str,
        content: str,
        metadata: dict | None = None,
    ) -> dict:
        """Log a conversation message to history.
        
        Args:
            session_id: Unique session identifier
            role: Message role (user, assistant, system)
            content: Message content
            metadata: Additional metadata
        
        Returns:
            Dictionary with log information
        """
        await memory_mgr.remember_conversation(
            session_id=session_id,
            role=role,
            content=content,
            metadata=metadata,
        )
        
        return {
            "success": True,
            "session_id": session_id,
            "role": role,
        }

    @mcp_server.tool()
    async def memory_conversation_get(
        session_id: str,
        limit: int = 50,
    ) -> list:
        """Get conversation history for a session.
        
        Args:
            session_id: Unique session identifier
            limit: Maximum number of messages (default: 50)
        
        Returns:
            List of conversation messages in chronological order
        """
        messages = await memory_mgr.recall_conversation(
            session_id=session_id,
            limit=limit,
        )
        
        return [
            {
                "session_id": m["session_id"],
                "role": m["role"],
                "content": m["content"],
                "timestamp": str(m["timestamp"]),
            }
            for m in messages
        ]

    _tool_functions["memory_retrieve"] = memory_retrieve
    _tool_functions["memory_search"] = memory_search
    _tool_functions["memory_stats"] = memory_stats
    _tool_functions["memory_conversation_log"] = memory_conversation_log
    _tool_functions["memory_conversation_get"] = memory_conversation_get


async def register_data_bank_tools(mcp_server: FastMCP, storage: MemoryStorage):
    """Register data bank tools."""

    @mcp_server.tool()
    async def external_data_bank_get(
        bank_name: str,
        limit: int = 50,
    ) -> dict:
        """Read a data bank directly from the WPF app SQLite file.

        Args:
            bank_name: Name of the data bank to fetch (case-insensitive).
            limit: Maximum number of entries to return (default: 50).
        """
        config = get_config()
        db_path = Path(config.app_database_path)
        if not db_path.exists():
            return {
                "success": False,
                "error": f"App database not found at {db_path}",
            }

        try:
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()
            row = None
            lookup_names = [
                bank_name,
                f"{bank_name} - Personal Data",
            ]
            for candidate in lookup_names:
                cur.execute(
                    """
                    SELECT Id, Name, Description, DataEntries
                    FROM DataBanks
                    WHERE lower(Name) = lower(?)
                    ORDER BY CreatedAt DESC
                    LIMIT 1
                    """,
                    (candidate,),
                )
                row = cur.fetchone()
                if row:
                    break
            if not row:
                cur.execute(
                    """
                    SELECT Id, Name, Description, DataEntries
                    FROM DataBanks
                    WHERE lower(Name) LIKE lower(?)
                    ORDER BY CreatedAt DESC
                    LIMIT 1
                    """,
                    (f"%{bank_name}%",),
                )
                row = cur.fetchone()
        except Exception as exc:
            return {"success": False, "error": f"DB query failed: {exc}"}
        finally:
            try:
                conn.close()
            except Exception:
                pass

        if not row:
            return {"success": False, "error": f"Data bank '{bank_name}' not found"}

        bank_id, name, description, raw_entries = row
        entries = []
        try:
            entries = json.loads(raw_entries) if raw_entries else []
        except Exception as exc:
            return {"success": False, "error": f"Failed to parse entries: {exc}"}

        truncated = False
        if limit and len(entries) > limit:
            entries = entries[:limit]
            truncated = True

        return {
            "success": True,
            "bank": {
                "id": bank_id,
                "name": name,
                "description": description,
                "entries_returned": len(entries),
                "truncated": truncated,
            },
            "entries": entries,
        }

    @mcp_server.tool()
    async def app_memory_search(
        query: str,
        contact_id: str | None = None,
        limit: int = 10,
    ) -> list:
        """Search the House Victoria app memory database (persona memories, journals, etc.).

        Args:
            query: Text to search for
            contact_id: Optional persona/contact id filter
            limit: Maximum results (default 10)
        """
        config = get_config()
        db_path = Path(config.app_database_path)
        if not db_path.exists():
            return [{"error": f"App database not found at {db_path}"}]

        safe_limit = max(1, min(limit, 50))
        results: list[dict] = []
        escaped = query.replace('"', '""')
        fts_query = f'"{escaped}"'
        conn = None
        try:
            conn = sqlite3.connect(db_path)
            conn.row_factory = sqlite3.Row
            cur = conn.cursor()
            rows = []
            try:
                if contact_id:
                    cur.execute(
                        """
                        SELECT m.Id, m.ContactId, m.Type, m.Content, m.UpdatedAt
                        FROM Memory_fts f
                        JOIN Memory m ON m.Id = f.Id
                        WHERE f MATCH ? AND m.ContactId = ?
                        ORDER BY m.UpdatedAt DESC
                        LIMIT ?
                        """,
                        (fts_query, contact_id, safe_limit),
                    )
                else:
                    cur.execute(
                        """
                        SELECT m.Id, m.ContactId, m.Type, m.Content, m.UpdatedAt
                        FROM Memory_fts f
                        JOIN Memory m ON m.Id = f.Id
                        WHERE f MATCH ?
                        ORDER BY m.UpdatedAt DESC
                        LIMIT ?
                        """,
                        (fts_query, safe_limit),
                    )
                rows = cur.fetchall()
            except sqlite3.OperationalError:
                rows = []

            if not rows:
                like = f"%{query}%"
                if contact_id:
                    cur.execute(
                        """
                        SELECT Id, ContactId, Type, Content, UpdatedAt
                        FROM Memory
                        WHERE ContactId = ? AND Content LIKE ?
                        ORDER BY UpdatedAt DESC
                        LIMIT ?
                        """,
                        (contact_id, like, safe_limit),
                    )
                else:
                    cur.execute(
                        """
                        SELECT Id, ContactId, Type, Content, UpdatedAt
                        FROM Memory
                        WHERE Content LIKE ?
                        ORDER BY UpdatedAt DESC
                        LIMIT ?
                        """,
                        (like, safe_limit),
                    )
                rows = cur.fetchall()
            for row in rows:
                results.append(
                    {
                        "id": row["Id"],
                        "contact_id": row["ContactId"],
                        "type": row["Type"],
                        "content": row["Content"],
                        "updated_at": row["UpdatedAt"],
                    }
                )
        except Exception as exc:
            return [{"error": f"App memory search failed: {exc}"}]
        finally:
            if conn is not None:
                try:
                    conn.close()
                except Exception:
                    pass

        return results

    @mcp_server.tool()
    async def project_bank_create(
        project_name: str,
        metadata: dict | None = None,
    ) -> dict:
        """Create a new project data bank.
        
        Args:
            project_name: Name of the project
            metadata: Project metadata dictionary
        
        Returns:
            Dictionary with project creation information
        """
        project_key = f"project:{project_name}"
        
        project_data = {
            "name": project_name,
            "created_at": str(asyncio.get_event_loop().time()),
            "metadata": metadata or {},
            "status": "active",
        }
        
        memory_id = await storage.store(
            key=project_key,
            value=project_data,
            category="project",
            importance=1.0,
        )
        
        return {
            "success": True,
            "project_id": memory_id,
            "key": project_key,
            "project_name": project_name,
        }

    @mcp_server.tool()
    async def project_bank_get(project_name: str) -> dict:
        """Get project information from data bank.
        
        Args:
            project_name: Name of the project
        
        Returns:
            Dictionary with project information
        """
        project_key = f"project:{project_name}"
        entry = await storage.retrieve(project_key)
        
        if not entry:
            return {
                "success": False,
                "error": "Project not found",
            }
        
        return {
            "success": True,
            "project": entry["value"],
        }

    @mcp_server.tool()
    async def knowledge_bank_add(
        topic: str,
        content: str,
        category: str = "general",
        tags: list | None = None,
    ) -> dict:
        """Add knowledge to the knowledge bank.
        
        Args:
            topic: Knowledge topic/title
            content: Knowledge content
            category: Knowledge category
            tags: Optional tags for organization
        
        Returns:
            Dictionary with knowledge addition information
        """
        knowledge_key = f"knowledge:{category}:{topic}"
        
        knowledge_data = {
            "topic": topic,
            "content": content,
            "category": category,
            "tags": tags or [],
            "created_at": str(asyncio.get_event_loop().time()),
        }
        
        memory_id = await storage.store(
            key=knowledge_key,
            value=knowledge_data,
            category="knowledge",
            importance=0.8,
        )
        
        return {
            "success": True,
            "knowledge_id": memory_id,
            "key": knowledge_key,
        }

    @mcp_server.tool()
    async def resource_bank_index(
        resource_path: str,
        resource_type: str,
        description: str = "",
        metadata: dict | None = None,
    ) -> dict:
        """Index a resource in the resource catalog.
        
        Args:
            resource_path: Path to the resource
            resource_type: Type of resource (file, url, database, etc.)
            description: Resource description
            metadata: Additional resource metadata
        
        Returns:
            Dictionary with resource indexing information
        """
        resource_key = f"resource:{resource_type}:{resource_path}"
        
        resource_data = {
            "path": resource_path,
            "type": resource_type,
            "description": description,
            "metadata": metadata or {},
            "indexed_at": str(asyncio.get_event_loop().time()),
        }
        
        memory_id = await storage.store(
            key=resource_key,
            value=resource_data,
            category="resource",
            importance=0.7,
        )
        
        return {
            "success": True,
            "resource_id": memory_id,
            "key": resource_key,
        }

    @mcp_server.tool()
    async def config_bank_set(
        config_key: str,
        config_value: str,
    ) -> dict:
        """Set a configuration value.
        
        Args:
            config_key: Configuration key
            config_value: Configuration value
        
        Returns:
            Dictionary with configuration set information
        """
        config_key_full = f"config:{config_key}"
        
        memory_id = await storage.store(
            key=config_key_full,
            value={"key": config_key, "value": config_value},
            category="config",
            importance=0.9,
        )
        
        return {
            "success": True,
            "config_id": memory_id,
            "key": config_key_full,
        }

    @mcp_server.tool()
    async def config_bank_get(config_key: str) -> dict:
        """Get a configuration value.
        
        Args:
            config_key: Configuration key
        
        Returns:
            Dictionary with configuration value
        """
        config_key_full = f"config:{config_key}"
        entry = await storage.retrieve(config_key_full)
        
        if not entry:
            return {
                "success": False,
                "error": "Configuration not found",
            }
        
        return {
            "success": True,
            "config": entry["value"],
        }

    _tool_functions["external_data_bank_get"] = external_data_bank_get
    _tool_functions["app_memory_search"] = app_memory_search
    _tool_functions["project_bank_create"] = project_bank_create
    _tool_functions["project_bank_get"] = project_bank_get
    _tool_functions["knowledge_bank_add"] = knowledge_bank_add
    _tool_functions["resource_bank_index"] = resource_bank_index
    _tool_functions["config_bank_set"] = config_bank_set
    _tool_functions["config_bank_get"] = config_bank_get


async def register_system_tools(mcp_server: FastMCP):
    """Register system tools."""

    @mcp_server.tool()
    async def system_info() -> dict:
        """Get system information.
        
        Returns:
            Dictionary with system information
        """
        import sys
        import platform
        
        return {
            "python_version": sys.version,
            "platform": platform.platform(),
            "architecture": platform.machine(),
            "processor": platform.processor(),
        }

    @mcp_server.tool()
    async def list_categories() -> list:
        """List all memory categories.
        
        Returns:
            List of category names
        """
        return ["project", "knowledge", "resource", "config", "conversation"]

    @mcp_server.tool()
    async def save_to_file_retrieval(filename: str, content: str) -> dict:
        """Save a user-facing file to the House Victoria File Retrieval folder (📥 top tray).

        Use this when the user asks for a document, research paper, report, or any file
        they should be able to open from File Retrieval in the desktop app.

        Args:
            filename: Target filename including extension (e.g. research_paper.md)
            content: Full file body (markdown, text, json, etc.)
        """
        from pathlib import Path
        from .config import resolve_file_retrieval_path

        safe_name = Path(filename).name.strip()
        if not safe_name:
            return {"success": False, "error": "filename is required"}

        target_dir = Path(resolve_file_retrieval_path())
        target_dir.mkdir(parents=True, exist_ok=True)
        target_path = target_dir / safe_name
        if target_path.exists():
            stem = target_path.stem
            suffix = target_path.suffix
            from datetime import datetime, timezone
            stamp = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
            target_path = target_dir / f"{stem}_{stamp}{suffix}"

        target_path.write_text(content, encoding="utf-8")
        return {
            "success": True,
            "filename": target_path.name,
            "path": str(target_path),
            "location": "File Retrieval",
        }

    @mcp_server.tool()
    async def list_house_victoria_tools() -> dict:
        """List House Victoria MCP tools and when to use them.

        Call this when you need to deliver a file, run MT4/trading actions, or store memory —
        instead of guessing or narrating actions you have not taken.
        """
        from .config import resolve_file_retrieval_path

        return {
            "file_retrieval_path": resolve_file_retrieval_path(),
            "deliver_files": {
                "save_to_file_retrieval": "Save a document/paper/report to the user's File Retrieval folder (📥 top tray).",
                "chat_file_markers": "Or put content in [FILE]filename.md[/FILE] in the chat reply (app saves automatically).",
            },
            "trading": [
                "mt4_status", "mt4_list_symbols", "mt4_get_market_data",
                "mt4_get_open_positions", "mt4_execute_trade", "mt4_close_position",
                "mt4_run_backtest", "mt4_export_history", "mt4_get_historical_bars",
            ],
            "memory_and_banks": [
                "memory_store", "memory_search", "memory_retrieve",
                "project_bank_create", "knowledge_bank_add", "config_bank_set",
            ],
        }

    _tool_functions["system_info"] = system_info
    _tool_functions["list_categories"] = list_categories
    _tool_functions["save_to_file_retrieval"] = save_to_file_retrieval
    _tool_functions["list_house_victoria_tools"] = list_house_victoria_tools


async def register_trading_tools(mcp_server: FastMCP):
    """Register MetaTrader 4 bridge tools."""

    @mcp_server.tool()
    async def mt4_status() -> dict:
        """Get MT4 bridge connection status, terminal path, and account snapshot."""
        return get_bridge_status()

    @mcp_server.tool()
    async def mt4_list_symbols() -> dict:
        """List broker symbol names and the base->broker map (e.g. EURUSD -> EURUSD.pro)."""
        return list_symbols()

    @mcp_server.tool()
    async def mt4_get_market_data(symbol: str) -> dict:
        """Get current bid/ask for a symbol from the MT4 bridge."""
        return get_market_data(symbol)

    @mcp_server.tool()
    async def mt4_get_open_positions() -> dict:
        """Get open MT4 positions written by the HouseVictoriaBridge EA."""
        return get_open_positions()

    @mcp_server.tool()
    async def mt4_execute_trade(
        symbol: str,
        trade_type: int,
        volume: float,
        stop_loss: float | None = None,
        take_profit: float | None = None,
    ) -> dict:
        """Execute a live MT4 trade through the file bridge.

        Returns success only when the EA response includes a ticket that appears
        in OpenPositions.json (verified atomic execution). Call mt4_list_symbols
        first if symbol suffixes are unknown.

        Args:
            symbol: Currency pair, e.g. EURUSD
            trade_type: 0 = buy, 1 = sell
            volume: Lot size, e.g. 0.01
            stop_loss: Required stop loss price (bridge RequireStopLoss=true). Auto-filled ~20 pips from quote when omitted.
            take_profit: Optional take profit price
        """
        return execute_trade(
            symbol=symbol,
            trade_type=trade_type,
            volume=volume,
            stop_loss=stop_loss,
            take_profit=take_profit,
        )

    @mcp_server.tool()
    async def mt4_close_position(ticket: int) -> dict:
        """Close an open MT4 position by ticket (HouseVictoria magic only).

        Use mt4_get_open_positions first to list tickets. Verified when the ticket
        disappears from OpenPositions.json after the EA confirms the close.

        Args:
            ticket: Position ticket from mt4_get_open_positions or mt4_execute_trade
        """
        return close_position(ticket=ticket)

    @mcp_server.tool()
    async def mt4_verify_ticket(ticket: int) -> dict:
        """Verify an MT4 ticket exists in OpenPositions.json (HouseVictoria magic)."""
        return verify_ticket(ticket)

    @mcp_server.tool()
    async def mt4_market_watch_status() -> dict:
        """Poll multi-pair market watch: pending alerts, technical signals, watchlist, last scan times.

        Requires House Victoria running with TradingWatchEnabled. Reads market-watch-status.json.
        """
        return get_market_watch_status()

    @mcp_server.tool()
    async def mt4_export_history(
        symbol: str,
        time_frame: str = "H1",
        start_date: str | None = None,
        end_date: str | None = None,
    ) -> dict:
        """Export OHLCV from MT4 to a bridge CSV via the EA (live chart data).

        Use when .hst files are missing. After export, call mt4_get_historical_bars or mt4_run_backtest.
        """
        return export_history(
            symbol=symbol,
            time_frame=time_frame,
            start_date=start_date,
            end_date=end_date,
        )

    @mcp_server.tool()
    async def mt4_get_historical_bars(
        symbol: str,
        time_frame: str = "H1",
        start_date: str | None = None,
        end_date: str | None = None,
        max_bars: int = 5000,
    ) -> dict:
        """Load OHLCV bars from MT4 .hst history (requires data downloaded in History Center)."""
        return get_historical_bars(
            symbol=symbol,
            time_frame=time_frame,
            start_date=start_date,
            end_date=end_date,
            max_bars=max_bars,
        )

    @mcp_server.tool()
    async def mt4_run_backtest(
        symbol: str,
        time_frame: str = "H1",
        start_date: str | None = None,
        end_date: str | None = None,
        strategy_name: str = "HermesBacktest",
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
    ) -> dict:
        """Backtest a strategy on MT4 historical data.

        strategy_type: ma_crossover | ema_crossover | macd | bollinger | rsi | breakout
        Use mt4_export_history if .hst data is missing, then mt4_get_historical_bars.
        """
        return run_backtest(
            symbol=symbol,
            time_frame=time_frame,
            start_date=start_date,
            end_date=end_date,
            strategy_name=strategy_name,
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
        )

    _tool_functions["mt4_status"] = mt4_status
    _tool_functions["mt4_list_symbols"] = mt4_list_symbols
    _tool_functions["mt4_get_market_data"] = mt4_get_market_data
    _tool_functions["mt4_get_open_positions"] = mt4_get_open_positions
    _tool_functions["mt4_execute_trade"] = mt4_execute_trade
    _tool_functions["mt4_close_position"] = mt4_close_position
    _tool_functions["mt4_verify_ticket"] = mt4_verify_ticket
    _tool_functions["mt4_market_watch_status"] = mt4_market_watch_status
    _tool_functions["mt4_export_history"] = mt4_export_history
    _tool_functions["mt4_get_historical_bars"] = mt4_get_historical_bars
    _tool_functions["mt4_run_backtest"] = mt4_run_backtest


async def register_agent_tools(mcp_server: FastMCP, agent: CognitiveAgent):
    """Register cognitive agent tools."""

    @mcp_server.tool()
    async def agent_step(external_input: Optional[Dict[str, Any]] = None) -> dict:
        """Run a single cognitive agent step.

        Args:
            external_input: Optional structured perception input
                (e.g., Unreal state, sensors, speech transcript).

        Returns:
            Structured result with goal, plan, result, drives, world_state, reflection.
        """
        result = await agent.step(external_input)
        return {
            "success": True,
            "agent": result,
        }

    @mcp_server.tool()
    async def agent_state() -> dict:
        """Get a lightweight snapshot of the agent's current state.

        Returns:
            Dictionary with basic state information.
        """
        # The current CognitiveAgent does not expose a dedicated state object,
        # but we can provide a minimal snapshot by running a no-op step in future.
        # For now, this returns a simple static description.
        return {
            "success": True,
            "name": agent.name,
            "personality": agent.personality,
        }

    _tool_functions["agent_step"] = agent_step
    _tool_functions["agent_state"] = agent_state


async def register_tt_tools(
    mcp_server: FastMCP,
    task_mgr: TaskManager,
    workflow_engine: WorkflowEngine,
    progress_tracker: ProgressTracker,
):
    """Register TT (Task & Workflow) tools."""

    @mcp_server.tool()
    async def task_create(
        name: str,
        description: str,
        priority: str = "medium",
        assigned_to: str | None = None,
        tags: list | None = None,
        dependencies: list | None = None,
    ) -> dict:
        """Create a new task.
        
        Args:
            name: Task name
            description: Task description
            priority: Task priority (low, medium, high, urgent)
            assigned_to: User or system assigned to
            tags: Tags for organization
            dependencies: IDs of tasks this depends on
        
        Returns:
            Dictionary with task creation information
        """
        from .tt.task_manager import TaskPriority
        
        task = await task_mgr.create_task(
            name=name,
            description=description,
            priority=TaskPriority(priority),
            assigned_to=assigned_to,
            tags=tags,
            dependencies=dependencies,
        )
        
        return {
            "success": True,
            "task_id": task.id,
            "name": task.name,
            "priority": priority,
        }

    @mcp_server.tool()
    async def task_get(task_id: str) -> dict:
        """Get a task by ID.
        
        Args:
            task_id: Task ID
        
        Returns:
            Dictionary with task information
        """
        task = await task_mgr.get_task(task_id)
        
        if not task:
            return {
                "success": False,
                "error": "Task not found",
            }
        
        return {
            "success": True,
            "task": {
                "id": task.id,
                "name": task.name,
                "description": task.description,
                "status": task.status.value,
                "priority": task.priority.value,
                "progress": task.progress,
            },
        }

    @mcp_server.tool()
    async def task_update_status(
        task_id: str,
        status: str,
        progress: float | None = None,
    ) -> dict:
        """Update task status.
        
        Args:
            task_id: Task ID
            status: New status (pending, in_progress, completed, failed, cancelled)
            progress: Progress percentage (0-100)
        
        Returns:
            Dictionary with update result
        """
        from .tt.task_manager import TaskStatus
        
        task = await task_mgr.update_task_status(
            task_id=task_id,
            status=TaskStatus(status),
            progress=progress,
        )
        
        if not task:
            return {
                "success": False,
                "error": "Task not found",
            }
        
        return {
            "success": True,
            "task_id": task.id,
            "status": task.status.value,
            "progress": task.progress,
        }

    @mcp_server.tool()
    async def task_list(
        status: str | None = None,
        assigned_to: str | None = None,
    ) -> list:
        """List tasks with optional filters.
        
        Args:
            status: Filter by status
            assigned_to: Filter by assignment
        
        Returns:
            List of tasks
        """
        from .tt.task_manager import TaskStatus
        
        tasks = await task_mgr.list_tasks(
            status=TaskStatus(status) if status else None,
            assigned_to=assigned_to,
        )
        
        return [
            {
                "id": t.id,
                "name": t.name,
                "status": t.status.value,
                "priority": t.priority.value,
                "progress": t.progress,
            }
            for t in tasks
        ]

    @mcp_server.tool()
    async def workflow_create(
        name: str,
        description: str,
        steps_config: list,
    ) -> dict:
        """Create a new workflow.
        
        Args:
            name: Workflow name
            description: Workflow description
            steps_config: List of step configurations
        
        Returns:
            Dictionary with workflow creation information
        """
        from .tt.workflow_engine import WorkflowStep, WorkflowStepType
        
        steps = []
        for step_cfg in steps_config:
            step = WorkflowStep(
                id=step_cfg.get("id", ""),
                name=step_cfg.get("name", ""),
                step_type=WorkflowStepType(step_cfg.get("type", "task")),
                step_config=step_cfg.get("config", {}),
                depends_on=step_cfg.get("depends_on", []),
            )
            steps.append(step)
        
        workflow = await workflow_engine.create_workflow(
            name=name,
            description=description,
            steps=steps,
        )
        
        return {
            "success": True,
            "workflow_id": workflow.id,
            "name": workflow.name,
            "steps_count": len(workflow.steps),
        }

    @mcp_server.tool()
    async def workflow_execute(workflow_id: str) -> dict:
        """Execute a workflow.
        
        Args:
            workflow_id: Workflow ID
        
        Returns:
            Dictionary with execution result
        """
        workflow = await workflow_engine.execute_workflow(workflow_id)
        
        return {
            "success": True,
            "workflow_id": workflow.id,
            "status": workflow.status.value,
            "progress": workflow.progress,
        }

    @mcp_server.tool()
    async def workflow_get(workflow_id: str) -> dict:
        """Get a workflow by ID.
        
        Args:
            workflow_id: Workflow ID
        
        Returns:
            Dictionary with workflow information
        """
        workflow = await workflow_engine.get_workflow(workflow_id)
        
        if not workflow:
            return {
                "success": False,
                "error": "Workflow not found",
            }
        
        return {
            "success": True,
            "workflow": {
                "id": workflow.id,
                "name": workflow.name,
                "description": workflow.description,
                "status": workflow.status.value,
                "progress": workflow.progress,
                "steps_count": len(workflow.steps),
            },
        }

    @mcp_server.tool()
    async def progress_get(meter_id: str) -> dict:
        """Get progress information for a meter.
        
        Args:
            meter_id: Progress meter ID
        
        Returns:
            Dictionary with progress information
        """
        summary = await progress_tracker.get_meter_summary(meter_id)
        
        if not summary:
            return {
                "success": False,
                "error": "Progress meter not found",
            }
        
        return {
            "success": True,
            "progress": summary,
        }

    @mcp_server.tool()
    async def progress_update(
        meter_id: str,
        progress: float,
        message: str | None = None,
    ) -> dict:
        """Update progress for a meter.
        
        Args:
            meter_id: Progress meter ID
            progress: Progress percentage (0-100)
            message: Status message
        
        Returns:
            Dictionary with update result
        """
        from .tt.progress_tracker import ProgressState
        
        meter = await progress_tracker.update_progress(
            meter_id=meter_id,
            progress=progress,
            state=ProgressState.IN_PROGRESS,
            message=message,
        )
        
        if not meter:
            return {
                "success": False,
                "error": "Progress meter not found",
            }
        
        return {
            "success": True,
            "meter_id": meter.id,
            "progress": meter.progress,
            "state": meter.state.value,
        }

    _tool_functions["task_create"] = task_create
    _tool_functions["task_get"] = task_get
    _tool_functions["task_update_status"] = task_update_status
    _tool_functions["task_list"] = task_list
    _tool_functions["workflow_create"] = workflow_create
    _tool_functions["workflow_execute"] = workflow_execute
    _tool_functions["workflow_get"] = workflow_get
    _tool_functions["progress_get"] = progress_get
    _tool_functions["progress_update"] = progress_update


def main():
    """Main entry point for the MCP server."""

    async def run_server():
        # Initialize server
        server = await create_server()
        # Run server (use run_stdio_async directly to avoid nested anyio.run)
        logger.info("Starting House Victoria MCP Server...")
        await server.run_stdio_async()

    anyio.run(run_server)


if __name__ == "__main__":
    main()
