"""HTTP REST API wrapper for House Victoria MCP Server.

This bridges the FastMCP server to HTTP REST endpoints
that the C# MCPService expects.
"""

import asyncio
import json
from typing import Any, Dict, Optional

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
import uvicorn

from house_victoria_mcp.server import create_server, mcp, get_tool_registry, call_tool_by_name
from house_victoria_mcp.config import get_config
from house_victoria_mcp.logger import get_logger

logger = get_logger("http_server")
app = FastAPI(title="House Victoria MCP Server HTTP API")

# Enable CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Global server instance and context storage
_mcp_server: Optional[Any] = None
_contexts: Dict[str, Dict[str, Any]] = {}
# Tool registry - will be populated by the server
_tool_registry: Dict[str, Any] = {}


@app.on_event("startup")
async def startup_event():
    """Initialize MCP server on startup."""
    global _mcp_server, _tool_registry
    try:
        logger.info("Initializing MCP server...")
        _mcp_server = await create_server()
        
        # Populate tool registry from the server
        _tool_registry.update(get_tool_registry())
        
        logger.info(f"MCP server initialized successfully. Tools registered: {list(_tool_registry.keys())}")
    except Exception as e:
        logger.error(f"Failed to initialize MCP server: {e}", exc_info=True)
        raise


@app.get("/health")
async def health_check():
    """Health check endpoint."""
    try:
        return {
            "status": "healthy",
            "server": "house-victoria-mcp",
            "version": "1.0.0"
        }
    except Exception as e:
        logger.error(f"Health check failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/info")
async def server_info():
    """Get server information."""
    try:
        # Get available tools from the MCP server
        capabilities = []
        if _mcp_server:
            # FastMCP exposes tools through its registry
            # Try to get tool names if available
            try:
                # Access tools from the mcp instance
                if hasattr(mcp, '_tools') or hasattr(_mcp_server, '_tools'):
                    tools_attr = getattr(_mcp_server, '_tools', getattr(mcp, '_tools', {}))
                    if isinstance(tools_attr, dict):
                        capabilities = list(tools_attr.keys())
            except Exception:
                pass
        
        # Fallback to known capabilities
        if not capabilities:
            capabilities = [
                "memory_store",
                "memory_retrieve",
                "memory_search",
                "memory_stats",
                "memory_conversation_log",
                "memory_conversation_get",
                "project_bank_create",
                "project_bank_get",
                "knowledge_bank_add",
                "resource_bank_index",
                "config_bank_set",
                "config_bank_get",
                "system_info",
                "list_categories",
                "task_create",
                "task_get",
                "task_update_status",
                "task_list",
                "workflow_create",
                "workflow_execute",
                "workflow_get",
                "progress_get",
                "progress_update"
            ]
        
        return {
            "name": "house-victoria",
            "version": "1.0.0",
            "description": "House Victoria MCP Server with persistent memory and data banks",
            "capabilities": capabilities
        }
    except Exception as e:
        logger.error(f"Failed to get server info: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.post("/command")
async def execute_command(command_data: Dict[str, Any]):
    """Execute an MCP command."""
    try:
        command = command_data.get("command")
        if not command:
            raise HTTPException(status_code=400, detail="Command is required")
        
        parameters = command_data.get("parameters", {})
        context_id = command_data.get("contextId")
        persona_id = command_data.get("personaId")
        
        # Execute the MCP tool
        result = await _execute_mcp_tool(command, parameters, context_id, persona_id)
        
        return {
            "success": True,
            "message": "Command executed successfully",
            "data": json.dumps(result) if isinstance(result, dict) else str(result)
        }
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Command execution failed: {e}", exc_info=True)
        return {
            "success": False,
            "message": str(e),
            "errorCode": "execution_error"
        }


async def _execute_mcp_tool(
    command: str,
    parameters: Dict[str, Any],
    context_id: Optional[str] = None,
    persona_id: Optional[str] = None
) -> Any:
    """Execute an MCP tool by name."""
    if not _mcp_server:
        raise RuntimeError("MCP server not initialized")
    
    logger.info(f"Executing command: {command} with params: {parameters}")
    
    try:
        # Use the server's tool calling function
        return await call_tool_by_name(command, **parameters)
    except ValueError as e:
        raise
    except Exception as e:
        logger.error(f"Error executing tool {command}: {e}", exc_info=True)
        raise RuntimeError(f"Failed to execute tool '{command}': {str(e)}")


@app.post("/context/init")
async def init_context(context_data: Dict[str, Any]):
    """Initialize a new context."""
    try:
        persona_id = context_data.get("personaId")
        persona_name = context_data.get("personaName")
        metadata = context_data.get("metadata", {})
        
        if not persona_id:
            raise HTTPException(status_code=400, detail="personaId is required")
        
        # Store context
        from datetime import datetime
        _contexts[persona_id] = {
            "contextId": persona_id,
            "personaId": persona_id,
            "personaName": persona_name or persona_id,
            "metadata": metadata or {},
            "data": {},
            "createdAt": datetime.now().isoformat(),
            "lastUpdated": datetime.now().isoformat()
        }
        
        logger.info(f"Initialized context for persona: {persona_id}")
        
        return {"success": True, "contextId": persona_id}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Context initialization failed: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/context/{persona_id}")
async def get_context(persona_id: str):
    """Get context for a persona."""
    try:
        if persona_id not in _contexts:
            raise HTTPException(status_code=404, detail=f"Context not found for persona: {persona_id}")
        
        return _contexts[persona_id]
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to get context: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.put("/context/{persona_id}")
async def update_context(persona_id: str, context_data: Dict[str, Any]):
    """Update context for a persona."""
    try:
        if persona_id not in _contexts:
            raise HTTPException(status_code=404, detail=f"Context not found for persona: {persona_id}")
        
        data = context_data.get("data")
        
        from datetime import datetime
        _contexts[persona_id]["data"] = data or _contexts[persona_id].get("data", {})
        _contexts[persona_id]["lastUpdated"] = datetime.now().isoformat()
        
        logger.info(f"Updated context for persona: {persona_id}")
        
        return {"success": True}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to update context: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.delete("/context/{persona_id}")
async def delete_context(persona_id: str):
    """Delete context for a persona."""
    try:
        if persona_id not in _contexts:
            raise HTTPException(status_code=404, detail=f"Context not found for persona: {persona_id}")
        
        del _contexts[persona_id]
        logger.info(f"Deleted context for persona: {persona_id}")
        
        return {"success": True}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Failed to delete context: {e}")
        raise HTTPException(status_code=500, detail=str(e))


def main():
    """Main entry point for HTTP server."""
    config = get_config()
    host = config.host
    port = config.port
    
    logger.info(f"Starting House Victoria MCP HTTP Server on {host}:{port}")
    
    uvicorn.run(
        app,
        host=host,
        port=port,
        log_level=config.log_level.lower()
    )


if __name__ == "__main__":
    main()
