"""
Long-Term Memory (LTM) MCP Server
Provides persistent memory storage and retrieval for AI conversations
"""

import asyncio
import json
from typing import Any, Dict, List, Optional
from datetime import datetime
from mcp.server import Server
from mcp.types import Tool, Resource, TextContent

class LtmMcpServer:
    def __init__(self):
        self.server = Server("ltm")
        self.memories = []
        self.setup_handlers()
    
    def setup_handlers(self):
        """Setup MCP server handlers"""
        
        @self.server.list_resources()
        async def list_resources() -> List[Resource]:
            """List available memory resources"""
            return [
                Resource(
                    uri="memory://all",
                    name="All Memories",
                    description="Access to all stored memories",
                    mimeType="application/json"
                ),
                Resource(
                    uri="memory://recent",
                    name="Recent Memories",
                    description="Access to recent memories",
                    mimeType="application/json"
                )
            ]
        
        @self.server.read_resource()
        async def read_resource(uri: str) -> str:
            """Read memory resources"""
            if uri == "memory://all":
                return json.dumps(self.memories, indent=2)
            elif uri == "memory://recent":
                recent = self.memories[-10:] if len(self.memories) > 10 else self.memories
                return json.dumps(recent, indent=2)
            else:
                raise ValueError(f"Unknown resource: {uri}")
        
        @self.server.list_tools()
        async def list_tools() -> List[Tool]:
            """List available tools"""
            return [
                Tool(
                    name="store_memory",
                    description="Store a new memory",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "content": {
                                "type": "string",
                                "description": "Memory content to store"
                            },
                            "tags": {
                                "type": "array",
                                "items": {"type": "string"},
                                "description": "Tags for categorizing the memory"
                            },
                            "importance": {
                                "type": "number",
                                "description": "Importance level (0-10)",
                                "default": 5
                            }
                        },
                        "required": ["content"]
                    }
                ),
                Tool(
                    name="search_memories",
                    description="Search stored memories",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "query": {
                                "type": "string",
                                "description": "Search query"
                            },
                            "tags": {
                                "type": "array",
                                "items": {"type": "string"},
                                "description": "Filter by tags"
                            },
                            "limit": {
                                "type": "number",
                                "description": "Maximum number of results",
                                "default": 10
                            }
                        },
                        "required": ["query"]
                    }
                ),
                Tool(
                    name="get_memory",
                    description="Get a specific memory by ID",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "memory_id": {
                                "type": "string",
                                "description": "Memory ID"
                            }
                        },
                        "required": ["memory_id"]
                    }
                ),
                Tool(
                    name="delete_memory",
                    description="Delete a memory by ID",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "memory_id": {
                                "type": "string",
                                "description": "Memory ID to delete"
                            }
                        },
                        "required": ["memory_id"]
                    }
                )
            ]
        
        @self.server.call_tool()
        async def call_tool(name: str, arguments: Dict[str, Any]) -> List[TextContent]:
            """Handle tool calls"""
            if name == "store_memory":
                return await self.store_memory(arguments)
            elif name == "search_memories":
                return await self.search_memories(arguments)
            elif name == "get_memory":
                return await self.get_memory(arguments)
            elif name == "delete_memory":
                return await self.delete_memory(arguments)
            else:
                raise ValueError(f"Unknown tool: {name}")
    
    async def store_memory(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Store a new memory"""
        content = arguments.get("content", "")
        tags = arguments.get("tags", [])
        importance = arguments.get("importance", 5)
        
        memory = {
            "id": f"mem_{len(self.memories)}_{int(datetime.now().timestamp())}",
            "content": content,
            "tags": tags,
            "importance": importance,
            "created_at": datetime.now().isoformat(),
            "accessed_count": 0
        }
        
        self.memories.append(memory)
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "status": "success",
                "memory": memory
            }, indent=2)
        )]
    
    async def search_memories(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Search memories"""
        query = arguments.get("query", "").lower()
        tags = arguments.get("tags", [])
        limit = arguments.get("limit", 10)
        
        results = []
        for memory in self.memories:
            # Simple search implementation
            if query in memory["content"].lower():
                if not tags or any(tag in memory["tags"] for tag in tags):
                    memory["accessed_count"] += 1
                    results.append(memory)
        
        results = results[:limit]
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "status": "success",
                "results": results,
                "count": len(results)
            }, indent=2)
        )]
    
    async def get_memory(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Get a specific memory"""
        memory_id = arguments.get("memory_id", "")
        
        memory = next((m for m in self.memories if m["id"] == memory_id), None)
        
        if memory:
            memory["accessed_count"] += 1
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "success",
                    "memory": memory
                }, indent=2)
            )]
        else:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "error",
                    "message": "Memory not found"
                }, indent=2)
            )]
    
    async def delete_memory(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Delete a memory"""
        memory_id = arguments.get("memory_id", "")
        
        initial_count = len(self.memories)
        self.memories = [m for m in self.memories if m["id"] != memory_id]
        
        if len(self.memories) < initial_count:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "success",
                    "message": "Memory deleted"
                }, indent=2)
            )]
        else:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "error",
                    "message": "Memory not found"
                }, indent=2)
            )]
    
    async def run(self):
        """Run the MCP server"""
        from mcp.server.stdio import stdio_server
        
        async with stdio_server() as (read_stream, write_stream):
            await self.server.run(
                read_stream,
                write_stream,
                self.server.create_initialization_options()
            )

def main():
    """Main entry point"""
    server = LtmMcpServer()
    asyncio.run(server.run())

if __name__ == "__main__":
    main()