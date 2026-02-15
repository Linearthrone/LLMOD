"""Entry point for running the MCP server as a module.

This allows the server to be run with: python -m house_victoria_mcp
"""

from .server import main

if __name__ == "__main__":
    main()
