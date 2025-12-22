"""
Resource Bank MCP Server
Manages and provides access to various resources (files, documents, data)
"""

import asyncio
import json
from typing import Any, Dict, List, Optional
from datetime import datetime
from mcp.server import Server
from mcp.types import Tool, Resource, TextContent

class ResourceBankServer:
    def __init__(self):
        self.server = Server("resource-bank")
        self.resources = {}
        self.setup_handlers()
    
    def setup_handlers(self):
        """Setup MCP server handlers"""
        
        @self.server.list_resources()
        async def list_resources() -> List[Resource]:
            """List available resources"""
            resource_list = []
            for resource_id, resource_data in self.resources.items():
                resource_list.append(Resource(
                    uri=f"resource://{resource_id}",
                    name=resource_data.get("name", resource_id),
                    description=resource_data.get("description", ""),
                    mimeType=resource_data.get("mime_type", "text/plain")
                ))
            return resource_list
        
        @self.server.read_resource()
        async def read_resource(uri: str) -> str:
            """Read a resource"""
            resource_id = uri.replace("resource://", "")
            
            if resource_id in self.resources:
                return self.resources[resource_id].get("content", "")
            else:
                raise ValueError(f"Resource not found: {resource_id}")
        
        @self.server.list_tools()
        async def list_tools() -> List[Tool]:
            """List available tools"""
            return [
                Tool(
                    name="add_resource",
                    description="Add a new resource to the bank",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "name": {
                                "type": "string",
                                "description": "Resource name"
                            },
                            "content": {
                                "type": "string",
                                "description": "Resource content"
                            },
                            "description": {
                                "type": "string",
                                "description": "Resource description"
                            },
                            "mime_type": {
                                "type": "string",
                                "description": "MIME type of the resource",
                                "default": "text/plain"
                            },
                            "tags": {
                                "type": "array",
                                "items": {"type": "string"},
                                "description": "Tags for categorizing the resource"
                            }
                        },
                        "required": ["name", "content"]
                    }
                ),
                Tool(
                    name="get_resource",
                    description="Get a resource by ID",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "resource_id": {
                                "type": "string",
                                "description": "Resource ID"
                            }
                        },
                        "required": ["resource_id"]
                    }
                ),
                Tool(
                    name="search_resources",
                    description="Search resources by name or tags",
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
                            }
                        }
                    }
                ),
                Tool(
                    name="update_resource",
                    description="Update an existing resource",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "resource_id": {
                                "type": "string",
                                "description": "Resource ID to update"
                            },
                            "content": {
                                "type": "string",
                                "description": "New content"
                            },
                            "description": {
                                "type": "string",
                                "description": "New description"
                            }
                        },
                        "required": ["resource_id"]
                    }
                ),
                Tool(
                    name="delete_resource",
                    description="Delete a resource",
                    inputSchema={
                        "type": "object",
                        "properties": {
                            "resource_id": {
                                "type": "string",
                                "description": "Resource ID to delete"
                            }
                        },
                        "required": ["resource_id"]
                    }
                )
            ]
        
        @self.server.call_tool()
        async def call_tool(name: str, arguments: Dict[str, Any]) -> List[TextContent]:
            """Handle tool calls"""
            if name == "add_resource":
                return await self.add_resource(arguments)
            elif name == "get_resource":
                return await self.get_resource(arguments)
            elif name == "search_resources":
                return await self.search_resources(arguments)
            elif name == "update_resource":
                return await self.update_resource(arguments)
            elif name == "delete_resource":
                return await self.delete_resource(arguments)
            else:
                raise ValueError(f"Unknown tool: {name}")
    
    async def add_resource(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Add a new resource"""
        name = arguments.get("name", "")
        content = arguments.get("content", "")
        description = arguments.get("description", "")
        mime_type = arguments.get("mime_type", "text/plain")
        tags = arguments.get("tags", [])
        
        resource_id = f"res_{len(self.resources)}_{int(datetime.now().timestamp())}"
        
        self.resources[resource_id] = {
            "id": resource_id,
            "name": name,
            "content": content,
            "description": description,
            "mime_type": mime_type,
            "tags": tags,
            "created_at": datetime.now().isoformat(),
            "updated_at": datetime.now().isoformat()
        }
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "status": "success",
                "resource_id": resource_id,
                "name": name
            }, indent=2)
        )]
    
    async def get_resource(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Get a resource"""
        resource_id = arguments.get("resource_id", "")
        
        if resource_id in self.resources:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "success",
                    "resource": self.resources[resource_id]
                }, indent=2)
            )]
        else:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "error",
                    "message": "Resource not found"
                }, indent=2)
            )]
    
    async def search_resources(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Search resources"""
        query = arguments.get("query", "").lower()
        tags = arguments.get("tags", [])
        
        results = []
        for resource_id, resource_data in self.resources.items():
            if query in resource_data.get("name", "").lower() or \
               query in resource_data.get("description", "").lower():
                if not tags or any(tag in resource_data.get("tags", []) for tag in tags):
                    results.append(resource_data)
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "status": "success",
                "results": results,
                "count": len(results)
            }, indent=2)
        )]
    
    async def update_resource(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Update a resource"""
        resource_id = arguments.get("resource_id", "")
        
        if resource_id not in self.resources:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "error",
                    "message": "Resource not found"
                }, indent=2)
            )]
        
        if "content" in arguments:
            self.resources[resource_id]["content"] = arguments["content"]
        if "description" in arguments:
            self.resources[resource_id]["description"] = arguments["description"]
        
        self.resources[resource_id]["updated_at"] = datetime.now().isoformat()
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "status": "success",
                "resource": self.resources[resource_id]
            }, indent=2)
        )]
    
    async def delete_resource(self, arguments: Dict[str, Any]) -> List[TextContent]:
        """Delete a resource"""
        resource_id = arguments.get("resource_id", "")
        
        if resource_id in self.resources:
            del self.resources[resource_id]
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "success",
                    "message": "Resource deleted"
                }, indent=2)
            )]
        else:
            return [TextContent(
                type="text",
                text=json.dumps({
                    "status": "error",
                    "message": "Resource not found"
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
    server = ResourceBankServer()
    asyncio.run(server.run())

if __name__ == "__main__":
    main()