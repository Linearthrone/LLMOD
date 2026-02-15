"""Main MCP Server implementation for House Victoria."""

import asyncio
import json
import sqlite3
from pathlib import Path
from typing import Any

from mcp.server.fastmcp import FastMCP

from .config import get_config
from .logger import get_logger
from .memory import MemoryStorage, MemoryManager
from .tt import TaskManager, WorkflowEngine, ProgressTracker

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

    config = get_config()
    
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

    # Register tool categories
    await register_memory_tools(mcp, memory_manager)
    await register_data_bank_tools(mcp, storage)
    await register_system_tools(mcp)
    await register_tt_tools(mcp, task_manager, workflow_engine, progress_tracker)
    
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
            cur.execute(
                """
                SELECT Id, Name, Description, DataEntries
                FROM DataBanks
                WHERE lower(Name) = lower(?)
                ORDER BY CreatedAt DESC
                LIMIT 1
                """,
                (bank_name,),
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


def main():
    """Main entry point for the MCP server."""
    
    async def run_server():
        # Initialize server
        server = await create_server()
        
        # Run server
        logger.info("Starting House Victoria MCP Server...")
        await server.run(transport="stdio")
    
    asyncio.run(run_server())


if __name__ == "__main__":
    main()
