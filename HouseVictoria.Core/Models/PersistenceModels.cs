using Newtonsoft.Json;

namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Individual entry stored in a data bank
    /// </summary>
    public class DataBankEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Category { get; set; }
        public List<string> Tags { get; set; } = new();
        public double Importance { get; set; } = 0.5;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Optional attachment metadata for full-file uploads
        /// </summary>
        public string? AttachmentPath { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }
        public long? AttachmentSizeBytes { get; set; }

        /// <summary>
        /// Temp path used during upload before being copied into the databank folder.
        /// Ignored in persistence.
        /// </summary>
        [JsonIgnore]
        public string? AttachmentTempPath { get; set; }

        /// <summary>
        /// When true, any existing attachment should be removed during update.
        /// </summary>
        [JsonIgnore]
        public bool AttachmentMarkedForRemoval { get; set; }
    }

    /// <summary>
    /// Data bank for storing context information
    /// </summary>
    public class DataBank
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<DataBankEntry> DataEntries { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Memory entry for AI contacts
    /// </summary>
    public class MemoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ContactId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public double Importance { get; set; } = 1.0;
        public int AccessCount { get; set; } = 0;
    }

    /// <summary>
    /// Global knowledge entry
    /// </summary>
    public class GlobalKnowledgeEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public string? Category { get; set; }
        public List<string> Tags { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastAccessed { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Application settings
    /// </summary>
    public class AppConfig
    {
        public string OllamaEndpoint { get; set; } = "http://localhost:11434";
        public string MCPServerEndpoint { get; set; } = "http://localhost:8080";
        public string UnrealEngineEndpoint { get; set; } = "ws://localhost:8888";
        public string TTSEndpoint { get; set; } = "http://localhost:5000";
        public string PiperDataDir { get; set; } = "Media/PiperVoices";
        public string PiperDefaultModel { get; set; } = "en_US-amy-medium";
        public string StableDiffusionEndpoint { get; set; } = "http://localhost:8188"; // Default to ComfyUI port
        public string MT4DataPath { get; set; } = "C:\\Program Files\\MetaTrader 4";
        public string DataBankPath { get; set; } = "Data/Databanks";
        public string LogsPath { get; set; } = "Logs";
        public string MediaPath { get; set; } = "Media";
        public int RefreshIntervalMs { get; set; } = 1000;
        public bool EnableOverlay { get; set; } = true;
        public double OverlayOpacity { get; set; } = 0.85;
        public bool AutoHideTrays { get; set; } = true;
        public int AutoHideDelayMs { get; set; } = 3000;

        // Avatar Settings
        public string AvatarModelPath { get; set; } = string.Empty;
        public string AvatarVoiceModel { get; set; } = string.Empty;
        public double AvatarVoiceSpeed { get; set; } = 1.0;
        public double AvatarVoicePitch { get; set; } = 1.0;

        // Locomotion Settings
        public double WalkSpeed { get; set; } = 1.0;
        public double RunSpeed { get; set; } = 2.0;
        public double JumpHeight { get; set; } = 1.0;
        public bool EnablePhysicsInteraction { get; set; } = true;

        // Tools Configuration
        public bool EnableFileSystemAccess { get; set; } = true;
        public bool EnableNetworkAccess { get; set; } = true;
        public bool EnableSystemCommands { get; set; } = false;
        public List<string> AllowedTools { get; set; } = new();

        // Persistent Memory Configuration
        public bool EnablePersistentMemory { get; set; } = true;
        public string PersistentMemoryPath { get; set; } = "Data/Memory";
        public int MemoryMaxEntries { get; set; } = 10000;
        public double MemoryImportanceThreshold { get; set; } = 0.5;
        public int MemoryRetentionDays { get; set; } = 90;

        // Memory backends
        public bool EnablePgVector { get; set; } = false;
        public string? PgVectorConnectionString { get; set; }
        public double HybridLexicalWeight { get; set; } = 0.5;

        // COVAS: Next (Elite Dangerous) bridge - OpenAI-compatible API for ship computer AI
        public bool CovasBridgeEnabled { get; set; } = false;
        public string CovasBridgeEndpoint { get; set; } = "http://localhost:11435";
        /// <summary>Optional AI contact ID to use as ship computer. If empty, first available contact is used.</summary>
        public string CovasContactId { get; set; } = string.Empty;
    }
}
