# Integration Guide for WPF Applications

This guide explains how to integrate the House Victoria MCP Server with your WPF desktop application.

## Overview

The House Victoria MCP Server provides:
- **Persistent Memory**: SQLite-based storage for conversation history and knowledge
- **Data Banks**: Structured storage for projects, knowledge, resources, and configurations
- **Complex Tools**: Ready-to-use tools for data processing, web operations, and system tasks
- **Task Tracking**: Workflow management and progress monitoring

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    WPF Application                          │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              AI/LLM Integration Layer               │  │
│  └──────────────────────────────────────────────────────┘  │
│                          │                                  │
│                          │ MCP Protocol (STDIO)             │
│                          ▼                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         House Victoria MCP Server                     │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌────────────┐  │  │
│  │  │ Memory System│  │ Data Banks   │  │ Tools      │  │  │
│  │  └──────────────┘  └──────────────┘  └────────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Integration Methods

### Method 1: MCP Client SDK (Recommended)

Use the MCP Client SDK to connect your WPF application programmatically.

```csharp
// Example C# code for WPF integration
using System.Diagnostics;

public class McpClient
{
    private Process _serverProcess;

    public async Task StartServerAsync()
    {
        var serverPath = @"C:\Users\kurtw\Victoria\HouseVictoria\MCPServerTemplate";
        var pythonPath = @"C:\Users\kurtw\Victoria\HouseVictoria\MCPServerTemplate\.venv\Scripts\python.exe";

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = "-m house_victoria_mcp",
            WorkingDirectory = serverPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _serverProcess = Process.Start(startInfo);
        await Task.Delay(1000); // Give server time to start
    }

    public async Task<string> SendRequestAsync(string request)
    {
        // MCP protocol implementation
        // Send JSON-RPC requests to the server
        await _serverProcess.StandardInput.WriteLineAsync(request);
        await _serverProcess.StandardInput.FlushAsync();

        string response = await _serverProcess.StandardOutput.ReadLineAsync();
        return response;
    }

    public void StopServer()
    {
        _serverProcess?.Kill();
    }
}
```

### Method 2: Named Pipes (Windows-Specific)

For local IPC without network overhead:

```python
# Server-side modification to support named pipes
import asyncio
import sys

from mcp.server.fastmcp import FastMCP

mcp = FastMCP("house-victoria")


# Add named pipe support
async def run_named_pipe(pipe_name="HouseVictoriaMCP"):
    import win32pipe
    import win32file
    
    pipe = win32pipe.CreateNamedPipe(
        f"\\\\.\\pipe\\{pipe_name}",
        win32pipe.PIPE_ACCESS_DUPLEX,
        win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE,
        1, 65536, 65536, 0, None
    )
    
    win32pipe.ConnectNamedPipe(pipe, None)
    
    while True:
        # Read request
        data = win32file.ReadFile(pipe, 4096)
        # Process request
        # Send response
        win32file.WriteFile(pipe, response)
```

```csharp
// Client-side for named pipes
public class NamedPipeMcpClient
{
    public async Task ConnectAsync()
    {
        using var pipeClient = new NamedPipeClientStream(
            ".",
            "HouseVictoriaMCP",
            PipeDirection.InOut
        );

        await pipeClient.ConnectAsync();
        // Use StreamReader and StreamWriter for communication
    }
}
```

### Method 3: HTTP Transport

For web client support or remote access:

```python
# Add this to server.py
async def run_http_server(host="localhost", port=8080):
    from mcp.server.sse import SseServerTransport
    
    starlette_app = mcp.create_sse_server()
    uvicorn.run(starlette_app, host=host, port=port)
```

```csharp
// Client-side for HTTP
using System.Net.Http;
using System.Text;

public class HttpMcpClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> SendRequestAsync(string jsonRpcRequest)
    {
        var content = new StringContent(
            jsonRpcRequest,
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "http://localhost:8080/mcp",
            content
        );

        return await response.Content.ReadAsStringAsync();
    }
}
```

## Available Tools Reference

### Memory Tools

#### `memory_store`
Store information in persistent memory.

**Parameters:**
- `value` (str): The value to store
- `key` (str, optional): Custom key (auto-generated if not provided)
- `category` (str, optional): Category for organization
- `importance` (float, optional): Importance score (0.0 - 1.0)
- `metadata` (dict, optional): Additional metadata

**Returns:**
```json
{
    "success": true,
    "key": "generated-key",
    "category": "user",
    "importance": 1.0
}
```

#### `memory_retrieve`
Retrieve information by key.

**Parameters:**
- `key` (str): Memory key to retrieve

#### `memory_search`
Search memory for information.

**Parameters:**
- `query` (str): Search query
- `category` (str, optional): Filter by category
- `limit` (int, optional): Maximum results (default: 10)

#### `memory_conversation_log`
Log conversation messages.

**Parameters:**
- `session_id` (str): Session identifier
- `role` (str): Message role (user, assistant, system)
- `content` (str): Message content
- `metadata` (dict, optional): Additional metadata

#### `memory_conversation_get`
Get conversation history.

**Parameters:**
- `session_id` (str): Session identifier
- `limit` (int, optional): Maximum messages (default: 50)

### Data Bank Tools

#### `project_bank_create`
Create a new project data bank.

**Parameters:**
- `project_name` (str): Name of the project
- `metadata` (dict, optional): Project metadata

#### `project_bank_get`
Get project information.

**Parameters:**
- `project_name` (str): Name of the project

#### `knowledge_bank_add`
Add knowledge to repository.

**Parameters:**
- `topic` (str): Knowledge topic/title
- `content` (str): Knowledge content
- `category` (str, optional): Knowledge category
- `tags` (list, optional): Tags for organization

#### `resource_bank_index`
Index a resource.

**Parameters:**
- `resource_path` (str): Path to the resource
- `resource_type` (str): Type of resource (file, url, database)
- `description` (str, optional): Resource description
- `metadata` (dict, optional): Additional metadata

#### `config_bank_set`
Set configuration value.

**Parameters:**
- `config_key` (str): Configuration key
- `config_value` (str): Configuration value

#### `config_bank_get`
Get configuration value.

**Parameters:**
- `config_key` (str): Configuration key

### System Tools

#### `system_info`
Get system information.

#### `list_categories`
List all memory categories.

## Usage Example in WPF

Create a view model for AI interactions:

```csharp
public class AiAssistantViewModel : INotifyPropertyChanged
{
    private readonly McpClient _mcpClient;
    private string _userInput;
    private string _assistantResponse;
    private string _currentConversationId;

    public AiAssistantViewModel()
    {
        _mcpClient = new McpClient();
        _currentConversationId = Guid.NewGuid().ToString();
        
        // Start server when initialized
        Task.Run(async () => await _mcpClient.StartServerAsync());
    }

    public async Task SendUserMessageAsync()
    {
        // Log user message
        var logRequest = CreateMcpRequest("memory_conversation_log", new
        {
            session_id = _currentConversationId,
            role = "user",
            content = UserInput
        });
        await _mcpClient.SendRequestAsync(logRequest);

        // Process with LLM (your LLM integration here)
        var llmResponse = await GetLlmResponseAsync(UserInput);

        // Search memory for relevant context
        var searchRequest = CreateMcpRequest("memory_search", new
        {
            query = UserInput,
            limit = 5
        });
        var context = await _mcpClient.SendRequestAsync(searchRequest);

        // Combine context with LLM response
        AssistantResponse = EnrichResponse(llmResponse, context);

        // Log assistant response
        var logAssistant = CreateMcpRequest("memory_conversation_log", new
        {
            session_id = _currentConversationId,
            role = "assistant",
            content = AssistantResponse
        });
        await _mcpClient.SendRequestAsync(logAssistant);

        // Store important information
        var storeRequest = CreateMcpRequest("memory_store", new
        {
            value = $"User asked: {UserInput}\nResponse: {AssistantResponse}",
            category = "conversation",
            importance = 0.8
        });
        await _mcpClient.SendRequestAsync(storeRequest);
    }

    private string CreateMcpRequest(string method, object parameters)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid(),
            method = method,
            @params = parameters
        });
    }

    public void Cleanup()
    {
        _mcpClient?.StopServer();
    }
}
```

## Connection in Visual Studio Code

Already configured! Check `.vscode/mcp.json`:

```json
{
  "servers": {
    "house-victoria": {
      "type": "stdio",
      "command": "C:/Users/kurtw/Victoria/HouseVictoria/MCPServerTemplate/.venv/Scripts/python.exe",
      "args": ["-m", "house_victoria_mcp"],
      "env": {
        "DATABASE_PATH": "C:/Users/kurtw/Victoria/HouseVictoria/MCPServerTemplate/data/memory.db",
        "DATA_BANKS_PATH": "C:/Users/kurtw/Victoria/HouseVictoria/MCPServerTemplate/data/banks",
        "LOG_LEVEL": "INFO"
      }
    }
  }
}
```

## Connection in Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "house-victoria": {
      "command": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\.venv\\Scripts\\python.exe",
      "args": ["-m", "house_victoria_mcp"],
      "env": {
        "DATABASE_PATH": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\data\\memory.db",
        "DATA_BANKS_PATH": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\data\\banks",
        "PROJECTS_PATH": "C:\\Users\\kurtw\\Victoria\\HouseVictoria\\MCPServerTemplate\\data\\projects",
        "LOG_LEVEL": "INFO"
      }
    }
  }
}
```

## Testing the Server

### Manual Testing

Run the server and test with JSON-RPC messages:

```powershell
# Start the server
python -m house_victoria_mcp

# In another terminal, send a test request
echo '{"jsonrpc":"2.0","id":1,"method":"memory_store","params":{"value":"test","category":"test"}}' | python -m house_victoria_mcp
```

### Using MCP Inspector

```bash
npx @modelcontextprotocol/inspector python -m house_victoria_mcp
```

## Troubleshooting

### Server Not Starting

1. Check virtual environment is activated
2. Verify all dependencies are installed: `pip list`
3. Check logs in `data/logs/server.log`
4. Ensure Python 3.10 or higher is installed

### Memory Not Persisting

1. Check database path in `.env` is correct
2. Verify write permissions to data directory
3. Check for SQLite lock files

### Tools Not Available

1. Verify server is initialized successfully
2. Check for error messages in stderr
3. Ensure tools are properly registered

## Performance Optimization

1. **Database Indexing**: Already configured in `storage.py`
2. **Connection Pooling**: For HTTP transport mode
3. **Async Operations**: All I/O operations are async
4. **Memory Caching**: Consider implementing in-memory cache for frequent accesses

## Security Considerations

1. **Input Validation**: All inputs are validated using Pydantic
2. **SQL Injection Protection**: Using parameterized queries
3. **File Access**: File operations are restricted to configured paths
4. **Authentication**: Consider adding for HTTP transport mode

## Next Steps

1. **Extend Tools**: Add custom tools in `house_victoria_mcp/tools/`
2. **Custom Data Banks**: Create specialized data banks for your use case
3. **Advanced Memory**: Implement semantic search with actual embeddings
4. **TT Integration**: Add task and workflow management features
5. **Monitoring**: Add metrics and monitoring capabilities

## Support

For issues or questions:
1. Check logs in `logs/server.log`
2. Review the MCP documentation at https://modelcontextprotocol.io/
3. Open an issue in the repository
