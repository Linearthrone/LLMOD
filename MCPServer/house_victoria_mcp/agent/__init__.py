"""
Agent reference implementation package for House Victoria MCP.

This package contains a cognitive agent skeleton that can be wired into the
MCP server by providing it with the existing memory, task/workflow, and
tool execution primitives.
"""

from .agent_reference_implementation import CognitiveAgent

__all__ = ["CognitiveAgent"]

