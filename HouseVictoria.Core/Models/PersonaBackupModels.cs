namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Manifest stored at the root of a persona backup archive.
    /// </summary>
    public class PersonaBackupManifest
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
        public string PersonaId { get; set; } = string.Empty;
        public string PersonaName { get; set; } = string.Empty;
        public string? SourceMachine { get; set; }
        public int MemoryCount { get; set; }
        public int DataBankCount { get; set; }
        public int ConversationCount { get; set; }
        public int MessageCount { get; set; }
        public int FileCount { get; set; }
        public List<PersonaBackupFileEntry> Files { get; set; } = new();
    }

    public class PersonaBackupFileEntry
    {
        /// <summary>Path inside the zip archive.</summary>
        public string ArchivePath { get; set; } = string.Empty;

        /// <summary>Original absolute path on the source machine (informational).</summary>
        public string? OriginalPath { get; set; }

        /// <summary>Logical role: persona-folder, media, avatar, databank-attachment.</summary>
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full memory row for lossless restore.
    /// </summary>
    public class PersonaMemoryRecord
    {
        public string Id { get; set; } = string.Empty;
        public string ContactId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public double Importance { get; set; } = 1.0;
        public int AccessCount { get; set; }
        public string? Type { get; set; }
        public string? Metadata { get; set; }
        public string? TenantId { get; set; }
        public string? PersonaId { get; set; }
        public string? ProjectId { get; set; }
        public int Pinned { get; set; }
        public long? TtlSeconds { get; set; }
        public string? UpdatedAt { get; set; }
        public string? LastAccessed { get; set; }
        public string? Lineage { get; set; }
    }

    public class PersonaBackupConversation
    {
        public string Id { get; set; } = string.Empty;
        public string ContactId { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    }

    public class PersonaBackupPayload
    {
        public PersonaBackupManifest Manifest { get; set; } = new();
        public AIContact Persona { get; set; } = new();
        public List<PersonaMemoryRecord> Memories { get; set; } = new();
        public List<DataBank> DataBanks { get; set; } = new();
        public List<PersonaBackupConversation> Conversations { get; set; } = new();
        public Dictionary<string, List<ConversationMessage>> MessagesByConversation { get; set; } = new();
    }

    public enum PersonaImportMode
    {
        /// <summary>Keep the original persona ID; overwrite existing data if present.</summary>
        PreserveId,
        /// <summary>Assign a new ID and import as a separate persona.</summary>
        NewCopy
    }

    public class PersonaBackupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? OutputPath { get; set; }
        public string? PersonaId { get; set; }
        public string? PersonaName { get; set; }
    }
}
