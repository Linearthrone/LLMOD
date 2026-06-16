namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Manages the external on-device streaming speech-to-speech engine
    /// (the Python "On-Device-Speech-to-Speech-Conversational-AI" process).
    /// The engine owns the microphone and speakers directly and runs the full
    /// VAD -> Whisper -> LLM -> TTS streaming loop, so House Victoria only needs
    /// to start it (with the chosen persona) when a call connects and stop it
    /// when the call ends.
    /// </summary>
    public interface IVoiceCallEngineService
    {
        /// <summary>True when the engine process is currently running.</summary>
        bool IsRunning { get; }

        /// <summary>Conversation id the running engine is bound to, if any.</summary>
        string? ActiveConversationId { get; }

        /// <summary>
        /// Starts the engine for the given session. Any already-running engine is
        /// stopped first. Returns false if the engine is disabled or cannot start.
        /// </summary>
        Task<bool> StartAsync(VoiceCallEngineSession session);

        /// <summary>Stops the engine process (kills the process tree) if running.</summary>
        Task StopAsync();
    }

    /// <summary>Per-call configuration handed to the voice engine at launch.</summary>
    public class VoiceCallEngineSession
    {
        /// <summary>Conversation this engine instance is serving.</summary>
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>Model id the engine should request.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// LLM protocol: "ollama" (native /api/chat) or "openai"
        /// (OpenAI-compatible /v1/chat/completions, e.g. Hermes or LM Studio).
        /// </summary>
        public string Backend { get; set; } = "ollama";

        /// <summary>
        /// Full chat endpoint. For Ollama: http://localhost:11434/api/chat.
        /// For OpenAI-compatible: http://127.0.0.1:8642/v1/chat/completions.
        /// </summary>
        public string OllamaChatUrl { get; set; } = "http://localhost:11434/api/chat";

        /// <summary>Bearer token for OpenAI-compatible backends (e.g. Hermes API key).</summary>
        public string? ApiKey { get; set; }

        /// <summary>System prompt (persona identity + voice-call style guidance).</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>Kokoro voice id (e.g. af_nicole). Null/empty uses engine default.</summary>
        public string? Voice { get; set; }

        /// <summary>Speech playback speed.</summary>
        public double Speed { get; set; } = 1.2;

        /// <summary>LLM sampling temperature.</summary>
        public double Temperature { get; set; } = 0.9;
    }
}
