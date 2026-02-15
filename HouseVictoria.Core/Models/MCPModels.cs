using System;
using System.Collections.Generic;

namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// MCP Server health information
    /// </summary>
    public class MCPServerHealth
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? LastChecked { get; set; }
        public string? Version { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// MCP Server information and capabilities
    /// </summary>
    public class MCPServerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string> Capabilities { get; set; } = new();
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// MCP Command request
    /// </summary>
    public class MCPCommand
    {
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public string? ContextId { get; set; }
        public string? PersonaId { get; set; }
    }

    /// <summary>
    /// MCP Response
    /// </summary>
    public class MCPResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// MCP Context information
    /// </summary>
    public class MCPContext
    {
        public string ContextId { get; set; } = string.Empty;
        public string PersonaId { get; set; } = string.Empty;
        public string? Data { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
