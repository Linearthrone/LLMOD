namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Represents an AI Contact (persona) that can be used for conversations
    /// </summary>
    public class AIContact
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string? SystemPrompt { get; set; }
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public Dictionary<string, string> PersonalityTraits { get; set; } = new();
        public string ServerEndpoint { get; set; } = "http://localhost:11434";
        public string MCPServerEndpoint { get; set; } = string.Empty; // Individual MCP server for this persona
        public Dictionary<string, string> AdditionalServers { get; set; } = new(); // Additional server endpoints (TTS, etc.)
        public bool IsLoaded { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUsedAt { get; set; } = DateTime.Now;
        public bool IsPrimaryAI { get; set; }
        public string? DataPath { get; set; } // Path to store this persona's data
        
        /// <summary>
        /// Piper TTS voice/model ID (e.g., en_US-lessac-medium). Used when this AI contact speaks during calls.
        /// </summary>
        public string? PiperVoiceId { get; set; }
        
        // LLM Parameters
        /// <summary>
        /// Temperature (0.0-2.0): Controls randomness. Lower = more focused, Higher = more creative. Default: 0.7
        /// </summary>
        public double Temperature { get; set; } = 0.7;
        
        /// <summary>
        /// Top P (0.0-1.0): Nucleus sampling. Controls diversity via nucleus probability. Default: 0.9
        /// </summary>
        public double TopP { get; set; } = 0.9;
        
        /// <summary>
        /// Top K (1-100): Limits sampling to top K most likely tokens. Default: 40
        /// </summary>
        public int TopK { get; set; } = 40;
        
        /// <summary>
        /// Repeat Penalty (0.0-2.0): Penalizes repetition. Higher = less repetition. Default: 1.1
        /// </summary>
        public double RepeatPenalty { get; set; } = 1.1;
        
        /// <summary>
        /// Max Tokens / Num Predict: Maximum tokens to generate. -1 = unlimited. Default: -1
        /// </summary>
        public int MaxTokens { get; set; } = -1;
        
        /// <summary>
        /// Context Length / Num Ctx: Size of the context window. Default: 4096
        /// </summary>
        public int ContextLength { get; set; } = 4096;
    }

    /// <summary>
    /// Represents a chat message in a conversation
    /// </summary>
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Role { get; set; } = "user"; // user, assistant, system
        public string Content { get; set; } = string.Empty;
        public byte[]? ImageData { get; set; }
        public byte[]? AudioData { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? ModelUsed { get; set; }
    }

    /// <summary>
    /// Model configuration
    /// </summary>
    public class AIModelConfig
    {
        public string Name { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public int ContextLength { get; set; } = 4096;
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 2048;
        public bool SupportsVision { get; set; } = false;
        public bool SupportsAudio { get; set; } = false;
    }
}
