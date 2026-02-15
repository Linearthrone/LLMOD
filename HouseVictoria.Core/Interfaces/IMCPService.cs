using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for Model Context Protocol (MCP) server communication
    /// </summary>
    public interface IMCPService
    {
        /// <summary>
        /// Check if MCP server is available and responsive
        /// </summary>
        Task<bool> IsServerAvailableAsync(string endpoint);

        /// <summary>
        /// Get server health status
        /// </summary>
        Task<MCPServerHealth> GetServerHealthAsync(string endpoint);

        /// <summary>
        /// Get server information and capabilities
        /// </summary>
        Task<MCPServerInfo?> GetServerInfoAsync(string endpoint);

        /// <summary>
        /// Send a command/message to the MCP server
        /// </summary>
        Task<MCPResponse?> SendCommandAsync(string endpoint, MCPCommand command);

        /// <summary>
        /// Initialize a new context/session for an AI persona
        /// </summary>
        Task<bool> InitializeContextAsync(string endpoint, string personaId, string personaName, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// Update context with new information
        /// </summary>
        Task<bool> UpdateContextAsync(string endpoint, string personaId, string contextData);

        /// <summary>
        /// Get context for a persona
        /// </summary>
        Task<MCPContext?> GetContextAsync(string endpoint, string personaId);

        /// <summary>
        /// Clear context for a persona
        /// </summary>
        Task<bool> ClearContextAsync(string endpoint, string personaId);

        event EventHandler<MCPServerStatusChangedEventArgs>? ServerStatusChanged;
    }

    public class MCPServerStatusChangedEventArgs : EventArgs
    {
        public string Endpoint { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public MCPServerHealth? Health { get; set; }
    }
}
