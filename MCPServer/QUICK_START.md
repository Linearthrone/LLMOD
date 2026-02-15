# Quick Start Guide

Get started with House Victoria MCP Server in minutes.

## Prerequisites

- Python 3.10 or higher
- A WPF desktop application (or any MCP-compatible client)
- Virtual environment capability

## Installation

### Option 1: Automated Setup (Windows)

1. Clone or navigate to the project directory
2. Run `setup.bat`

```powershell
setup.bat
```

This script will:
- Create a virtual environment
- Install all dependencies
- Create `.env` configuration file
- Create necessary data directories

### Option 2: Manual Setup

1. Create virtual environment:
```powershell
python -m venv .venv
```

2. Activate virtual environment:
```powershell
.venv\Scripts\activate.bat
```

3. Install dependencies:
```powershell
pip install -e .
```

4. Create `.env` file (copy from `.env.example`):
```powershell
copy .env.example .env
```

## Starting the Server

### As Standalone Server

```powershell
python -m house_victoria_mcp
```

The server will start in STDIO mode, ready to accept JSON-RPC requests.

### With VS Code

Already configured! The server is available in VS Code through `.vscode/mcp.json`.

### With Claude Desktop

Add this to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "house-victoria": {
      "command": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\.venv\\Scripts\\python.exe",
      "args": ["-m", "house_victoria_mcp"],
      "env": {
        "DATABASE_PATH": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\data\\memory.db",
        "DATA_BANKS_PATH": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\data\\banks"
      }
    }
  }
}
```

## Basic Usage

### Store Information

```
Call: memory_store
Parameters:
  value: "User prefers dark mode and uses 1920x1080 resolution"
  category: "user_preferences"
  importance: 0.9
```

### Retrieve Information

```
Call: memory_search
Parameters:
  query: "user preferences"
  category: "user_preferences"
```

### Log Conversation

```
Call: memory_conversation_log
Parameters:
  session_id: "unique-session-id"
  role: "user"
  content: "What are my display settings?"
```

### Create a Project Data Bank

```
Call: project_bank_create
Parameters:
  project_name: "Website Redesign"
  metadata:
    - status: "planning"
    - client: "Acme Corp"
```

### Add Knowledge

```
Call: knowledge_bank_add
Parameters:
  topic: "Responsive Design Best Practices"
  content: "Detailed article about responsive web design..."
  category: "design"
  tags: ["web", "responsive", "css", "mobile"]
```

### Index a Resource

```
Call: resource_bank_index
Parameters:
  resource_path: "C:\\Projects\\redesign\\assets\\logo.png"
  resource_type: "file"
  description: "Company logo - PNG format"
```

### Store Configuration

```
Call: config_bank_set
Parameters:
  config_key: "default_theme"
  config_value: "dark"
```

## Available Tools

### Memory Management
- `memory_store` - Store information in persistent memory
- `memory_retrieve` - Retrieve by key
- `memory_search` - Search memory
- `memory_stats` - Get memory statistics
- `memory_conversation_log` - Log conversation message
- `memory_conversation_get` - Get conversation history

### Data Banks
- `project_bank_create` - Create project data bank
- `project_bank_get` - Get project information
- `knowledge_bank_add` - Add to knowledge repository
- `resource_bank_index` - Index resources
- `config_bank_set` - Set configuration
- `config_bank_get` - Get configuration

### System
- `system_info` - Get system information
- `list_categories` - List memory categories

## Directory Structure

```
MCPServerTemplate/
├── data/
│   ├── memory.db              # SQLite database for memory
│   ├── banks/                 # Data banks storage
│   └── projects/              # Project data
├── logs/
│   └── server.log             # Server logs
├── house_victoria_mcp/
│   ├── memory/                # Memory system
│   │   ├── storage.py         # Database operations
│   │   ├── memory_manager.py  # Memory management
│   │   └── vector_search.py   # Semantic search
│   ├── tools/                 # Tool implementations
│   └── server.py              # Main MCP server
├── .env                       # Configuration
├── .vscode/
│   └── mcp.json              # VS Code configuration
├── pyproject.toml            # Python project config
└── README.md                  # Documentation
```

## Configuration

Edit `.env` to customize:

```env
# Server
SERVER_HOST=localhost
SERVER_PORT=8080

# Database
DATABASE_PATH=.\data\memory.db

# Data banks
DATA_BANKS_PATH=.\data\banks
PROJECTS_PATH=.\data\projects

# Logging
LOG_LEVEL=INFO
LOG_FILE=.\logs\server.log

# Memory
MEMORY_MAX_ENTRIES=10000
MEMORY_RETENTION_DAYS=365
```

## Testing

### Test with MCP Inspector

```powershell
npx @modelcontextprotocol/inspector python -m house_victoria_mcp
```

### Test with Python Script

```python
import asyncio
import json
from pathlib import Path

async def test_server():
    import sys
    sys.path.insert(0, str(Path(__file__).parent))
    
    from house_victoria_mcp import create_server
    
    server = await create_server()
    # Server will run in STDIO mode
    
if __name__ == "__main__":
    asyncio.run(test_server())
```

## WPF Integration

For detailed WPF integration instructions, see `INTEGRATION_GUIDE.md`.

Key points:
1. Use MCP Client SDK or direct stdio communication
2. Implement JSON-RPC request/response handling
3. Use tools to store/retrieve information
4. Log conversations for context retention

## Troubleshooting

### Server Won't Start

1. Check Python version: `python --version` (needs 3.10+)
2. Verify virtual environment: `dir .venv`
3. Check dependencies: `pip list`
4. View logs: `type logs\server.log`

### Memory Not Saving

1. Check database path in `.env`
2. Verify write permissions to `data/` directory
3. Check for existing SQLite lock files

### Tools Not Available

1. Ensure server started successfully
2. Check stderr for errors
3. Verify tools are registered in `server.py`

## Next Steps

1. **Custom Tools**: Add your own tools in `house_victoria_mcp/tools/`
2. **Advanced Features**: Implement semantic search, embeddings
3. **Integration**: Integrate with your WPF application
4. **Monitoring**: Add metrics and logging
5. **Scale**: Consider HTTP transport for multi-client support

## Documentation

- `README.md` - Full project documentation
- `INTEGRATION_GUIDE.md` - WPF integration guide
- `house_victoria_mcp/memory/` - Memory system documentation

## Support

- Check logs in `logs/server.log`
- Review MCP spec at https://modelcontextprotocol.io/
- Open issues for bugs or feature requests

## License

MIT License - See LICENSE file
