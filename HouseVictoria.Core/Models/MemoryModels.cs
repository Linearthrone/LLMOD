using System.Collections.Generic;

namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Represents a unit of memory stored by the system.
    /// </summary>
    public class MemoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? TenantId { get; set; }
        public string? PersonaId { get; set; }
        public string? ProjectId { get; set; }
        public string? ContactId { get; set; }
        public string Type { get; set; } = "memory";
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, string>? Metadata { get; set; }
        public double Importance { get; set; } = 1.0;
        public bool Pinned { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
        public long? TtlSeconds { get; set; }
        /// <summary>Lineage: who/what triggered this memory (e.g. taskId, toolName, agent, persona, project, runId).</summary>
        public Dictionary<string, string>? Lineage { get; set; }
    }

    /// <summary>
    /// Request for searching memory.
    /// </summary>
    public class MemorySearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public string? PersonaId { get; set; }
        public string? ProjectId { get; set; }
        public string? ContactId { get; set; }
        public string? Type { get; set; }
        public int Limit { get; set; } = 20;
    }

    /// <summary>
    /// Result item returned from a memory search.
    /// </summary>
    public class MemorySearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
