# MCP Server Configuration Issue - Fix Guide

## Problem Identified

The MCP server template uses **FastMCP with stdio transport**, but the C# `MCPService` expects **HTTP REST endpoints**. These are incompatible:

- **Python MCP Server**: Uses `transport="stdio"` (JSON-RPC over stdin/stdout)
- **C# MCPService**: Expects HTTP REST endpoints (`/health`, `/info`, `/command`, `/context/init`, etc.)

## Solution

An HTTP wrapper server (`http_server.py`) has been created to bridge HTTP REST requests to the MCP server. However, **tool execution needs to be completed** by registering tool functions.

## Status

✅ **Fixed:**
- HTTP server wrapper created (`http_server.py`)
- FastAPI/uvicorn added to dependencies
- Health check endpoint (`/health`) - Working
- Server info endpoint (`/info`) - Working  
- Context management endpoints (`/context/init`, `/context/{id}`, etc.) - Working

⚠️ **Needs Completion:**
- Tool execution (`/command` endpoint) - Partially implemented
- Tool registry - Needs tool function references to be stored during registration

## How to Run the HTTP Server

1. **Install dependencies:**
```powershell
cd MCPServerTemplate
.venv\Scripts\activate
pip install -e .
```

2. **Start the HTTP server:**
```powershell
python http_server.py
```

The server will start on `http://localhost:8080` (as configured in config.py).

3. **Test the connection:**
```powershell
curl http://localhost:8080/health
curl http://localhost:8080/info
```

## Next Steps to Complete Tool Execution

The tool registry (`_tool_functions`) needs to be populated with all tool functions. Currently, only `memory_store` is registered. You have two options:

### Option 1: Manual Registration (Current Approach)

Modify each tool registration in `server.py` to also store a reference in `_tool_functions`. For example:

```python
@mcp_server.tool()
async def memory_store(...):
    # ... existing code ...

# After the function definition, add:
_tool_functions["memory_store"] = memory_store
```

Do this for ALL tools (memory_store, memory_retrieve, memory_search, etc.)

### Option 2: Extract from FastMCP (Recommended)

After server creation, extract tool functions from FastMCP's internal registry. Modify `http_server.py` startup:

```python
@app.on_event("startup")
async def startup_event():
    global _mcp_server, _tool_registry
    _mcp_server = await create_server()
    
    # Extract tools from FastMCP
    # FastMCP v1.2+ stores tools in _tools or similar
    if hasattr(_mcp_server, '_tools'):
        tools = _mcp_server._tools
        # Extract tool functions and store in _tool_registry
        # This requires understanding FastMCP's internal structure
```

### Option 3: Use MCP Client SDK (Most Robust)

Use the MCP Python SDK's client to communicate with the server via stdio, bridging HTTP to stdio. This requires:
- Running FastMCP server in a subprocess
- Using MCP client to send JSON-RPC requests
- Translating HTTP requests to JSON-RPC

## Current Workaround

For now, the HTTP server will:
- ✅ Respond to health checks (server will appear as "available")
- ✅ Return server info and capabilities
- ✅ Handle context initialization/management
- ⚠️ Tool execution will fail with "Tool not found" until registration is complete

This allows the C# application to:
- Detect the MCP server is running
- Initialize contexts for personas
- Show server status as "healthy"

Tool execution can be added later once the tool registry is complete.

## Testing Connection from C# App

1. Make sure HTTP server is running on port 8080
2. In C# app Settings, set MCPServerEndpoint to `http://localhost:8080`
3. Check System Monitor - MCP Server should show as "Available"
4. Create/Edit AI Persona with MCP Server endpoint - context initialization should work

## Files Modified

- `MCPServerTemplate/pyproject.toml` - Added fastapi and uvicorn dependencies
- `MCPServerTemplate/http_server.py` - Created HTTP wrapper (NEW)
- `MCPServerTemplate/house_victoria_mcp/server.py` - Added tool registry infrastructure

## Recommended Next Action

Complete tool registration by modifying the tool registration functions to store references. See Option 1 above.
