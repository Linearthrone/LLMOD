using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// The role a persona plays in the household.
    /// <para><see cref="Primary"/> is the always-active "house brain" used by autonomy,
    /// journals, remote companion, etc. There is at most one primary at a time.</para>
    /// </summary>
    public enum PersonaRole
    {
        /// <summary>A regular persona with no special standing.</summary>
        None = 0,
        /// <summary>The always-active house persona. At most one persona is Primary.</summary>
        Primary = 1,
        /// <summary>A conversational companion (no autonomy/background duties).</summary>
        Companion = 2
    }

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
        public string MCPServerEndpoint { get; set; } = "http://localhost:8080"; // House Victoria MCP (memory + MT4 bridge tools)
        public Dictionary<string, string> AdditionalServers { get; set; } = new(); // Additional server endpoints (TTS, etc.)
        public bool IsLoaded { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUsedAt { get; set; } = DateTime.Now;
        /// <summary>
        /// Legacy flag kept in sync with <see cref="HouseVictoria.Core.Interfaces.IPersonaContext"/>.
        /// The authoritative "who is primary" lives in app config + the persona context service;
        /// prefer that over reading this directly.
        /// </summary>
        public bool IsPrimaryAI { get; set; }

        /// <summary>The role this persona plays. <see cref="PersonaRole.Primary"/> mirrors <see cref="IsPrimaryAI"/>.</summary>
        public PersonaRole Role { get; set; } = PersonaRole.None;

        public string? DataPath { get; set; } // Path to store this persona's data

        /// <summary>
        /// Chatterbox Turbo reference voice id (wav stem in Media/ChatterboxVoices). Used during voice calls.
        /// </summary>
        [JsonProperty("CallVoiceId")]
        public string? CallVoiceId { get; set; }

        /// <summary>Legacy JSON field; maps old Piper/Kokoro ids to <see cref="CallVoiceId"/> on load.</summary>
        [JsonProperty("PiperVoiceId")]
        public string? PiperVoiceId
        {
            get => CallVoiceId;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                if (string.IsNullOrWhiteSpace(CallVoiceId))
                    CallVoiceId = MapLegacyVoiceId(value);
            }
        }

        private static string MapLegacyVoiceId(string legacy)
        {
            var trimmed = legacy.Trim();
            if (Regex.IsMatch(trimmed, "^[a-z]{2}_[a-z]+$"))
                return "default";
            if (trimmed.Contains('-'))
                return "default";
            return trimmed;
        }

        // Avatar settings (for virtual environment / embodied AI)
        /// <summary>Path to 3D avatar model file (e.g., .fbx, .glb) for the virtual environment.</summary>
        public string? AvatarModelPath { get; set; }
        /// <summary>Voice speed (0.1–3.0) when this persona speaks as an avatar. Default: 1.0.</summary>
        public double AvatarVoiceSpeed { get; set; } = 1.0;
        /// <summary>Voice pitch (0.1–3.0) when this persona speaks as an avatar. Default: 1.0.</summary>
        public double AvatarVoicePitch { get; set; } = 1.0;

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

        /// <summary>
        /// What this persona may see in chat beyond its own thread (user basics, house journals, etc.).
        /// </summary>
        public PersonaKnowledgeSharing KnowledgeSharing { get; set; } = new();
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
}
