# House Victoria MCP Server

An advanced Model Context Protocol (MCP) server designed for WPF desktop applications, featuring persistent memory, complex tool ecosystems, and specialized data banks.

## Features

### 🧠 Persistent Memory System
- SQLite-based memory storage with vector-like search capabilities
- Conversation history with context retention
- Knowledge graph for semantic associations
- Automatic memory consolidation and pruning

### 🛠️ Complex Tool Ecosystem
- **Data Processing Tools**: File I/O, transformation, and analysis
- **Web Tools**: HTTP requests, web scraping, API interactions
- **System Tools**: Process management, file system operations
- **Analytics Tools**: Data aggregation, statistical analysis
- **Task Tools**: Scheduling, reminders, workflow automation

### 🗄️ Specialized Data Banks
- **Project Databases**: Structured storage for project metadata
- **Knowledge Repository**: Documentation, FAQs, procedures
- **Resource Catalog**: Index of files, assets, configurations
- **Configuration Store**: Settings, preferences, environment data

### 🎯 TT (Task & Workflow) Support
- Task definition and tracking
- Workflow orchestration
- Progress monitoring
- Result caching

## Installation

```bash
# Install dependencies
pip install -e .

# Or using uv
uv pip install -e .
```

## Configuration

Create a `.env` file in the root directory:

```env
# Server Configuration
SERVER_PORT=8080
SERVER_HOST=localhost

# Database Configuration
DATABASE_PATH=./data/memory.db

# Data Banks
DATA_BANKS_PATH=./data/banks
PROJECTS_PATH=./data/projects

# Logging
LOG_LEVEL=INFO
LOG_FILE=./logs/server.log
```

## Usage

### As STDIO Server (Recommended for MCP Clients)

```bash
python -m house_victoria_mcp
```

### Configure Claude Desktop

Add to your Claude Desktop configuration (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "house-victoria": {
      "command": "C:/Users/kurtw/Victoria/HouseVictoria/MCPServerTemplate/.venv/Scripts/python.exe",
      "args": ["-m", "house_victoria_mcp"],
      "env": {
        "DATABASE_PATH": "C:/Users/kurtw/Victoria/HouseVictoria/MCPServerTemplate/data/memory.db",
        "DATA_BANKS_PATH": "C:/Users/kurtw/Victoria/HouseVictoria/MCPServerTemplate/data/banks"
      }
    }
  }
}
```

### Integration with WPF Applications

The server is designed to work seamlessly with WPF desktop applications through:

1. **Named Pipes Communication**: For local IPC
2. **REST API**: For network communication
3. **Direct Python Import**: For embedded integration

## Architecture

```
house_victoria_mcp/
├── __init__.py              # Main entry point
├── server.py                # MCP Server implementation
├── config.py                # Configuration management
├── logger.py                # Logging utilities
├── memory/                  # Persistent memory system
│   ├── __init__.py
│   ├── storage.py          # Database operations
│   ├── memory_manager.py   # Memory management
│   └── vector_search.py    # Semantic search
├── tools/                   # Tool implementations
│   ├── __init__.py
│   ├── data_tools.py       # Data processing
│   ├── web_tools.py        # Web operations
│   ├── system_tools.py     # System operations
│   ├── analytics_tools.py  # Analytics
│   └── task_tools.py       # Task management
├── databanks/              # Data bank implementations
│   ├── __init__.py
│   ├── project_db.py       # Project database
│   ├── knowledge_repo.py   # Knowledge repository
│   ├── resource_catalog.py # Resource catalog
│   └── config_store.py     # Configuration store
└── tt/                     # Task & Workflow
    ├── __init__.py
    ├── task_manager.py     # Task management
    ├── workflow_engine.py  # Workflow execution
    └── progress_tracker.py # Progress tracking
```

## API Reference

### Memory Operations
- `memory_store`: Store information in persistent memory
- `memory_retrieve`: Retrieve information from memory
- `memory_search`: Search memory using keywords/semantics
- `memory_clear`: Clear memory entries
- `memory_stats`: Get memory statistics

### Data Bank Operations
- `project_bank_create`: Create a new project data bank
- `project_bank_query`: Query project data
- `knowledge_bank_add`: Add to knowledge repository
- `resource_bank_index`: Index resources
- `config_bank_set`: Set configuration values

### Tool Operations
- Various tools organized by category (data, web, system, analytics, task)

### TT Operations
- `task_create`: Create a new task
- `task_update`: Update task status
- `workflow_execute`: Execute a workflow
- `progress_get`: Get progress information

## Development

```bash
# Run tests
pytest

# Format code
black house_victoria_mcp/

# Lint code
ruff check house_victoria_mcp/
```

## License

MIT License - See LICENSE file for details

## Contributing

Contributions are welcome! Please read CONTRIBUTING.md for guidelines.
