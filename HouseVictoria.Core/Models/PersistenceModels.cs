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
    /// Persisted primary/secondary persona selections. Stored under the key
    /// <c>PersonaContextState</c> and managed by IPersonaContext.
    /// </summary>
    public class PersonaContextState
    {
        public string PrimaryId { get; set; } = string.Empty;
        public string SecondaryId { get; set; } = string.Empty;
    }

    public class AppConfig
    {
        public string OllamaEndpoint { get; set; } = "http://localhost:11434";
        /// <summary>LM Studio OpenAI-compatible API base URL (e.g. http://localhost:1234/v1).</summary>
        public string LmStudioEndpoint { get; set; } = "http://localhost:1234/v1";
        /// <summary>Anything LLM OpenAI-compatible API base URL (e.g. http://localhost:3001).</summary>
        public string AnythingLLMEndpoint { get; set; } = "http://localhost:3001";
        /// <summary>Primary LLM server: "ollama", "lmstudio", "anythingllm", or "hermes". Only one can be primary. Non-primary servers can be started manually from System Monitor.</summary>
        public string PrimaryLLM { get; set; } = "ollama";
        /// <summary>Hermes Agent OpenAI-compatible API base URL (e.g. http://127.0.0.1:8642/v1).</summary>
        public string HermesEndpoint { get; set; } = "http://127.0.0.1:8642/v1";
        /// <summary>Bearer token for Hermes API server (API_SERVER_KEY). Required when API auth is enabled.</summary>
        public string HermesApiKey { get; set; } = string.Empty;
        /// <summary>Model id sent to Hermes /v1/chat/completions (cosmetic; Hermes uses its configured provider).</summary>
        public string HermesModelName { get; set; } = "hermes-agent";
        /// <summary>Auto-start <c>hermes gateway</c> on app launch when Hermes is primary or this flag is true.</summary>
        public bool HermesAutoStart { get; set; } = true;
        /// <summary>
        /// When true, Victoria is allowed to act on the desktop via the Hermes <c>computer_use</c> tool
        /// (mouse/keyboard/screen). Independent of screen sharing: sharing lets her see, this lets her act.
        /// Default false for safety.
        /// </summary>
        public bool AllowComputerControl { get; set; }
        /// <summary>
        /// When true, Victoria may mutate the open Unreal Editor via Remote Control
        /// (set property, spawn, console). Read tools work whenever RC is up.
        /// Default false for safety. Synced to ~/.house_victoria/unreal_editor.env for MCP.
        /// </summary>
        public bool AllowUnrealEditorControl { get; set; }
        /// <summary>
        /// Epic Web Remote Control HTTP base URL (default http://127.0.0.1:30010).
        /// Separate from <see cref="UnrealEngineEndpoint"/> (world WebSocket :8888).
        /// </summary>
        public string UnrealRemoteControlUrl { get; set; } = "http://127.0.0.1:30010";
        /// <summary>
        /// Id of the always-active "primary" persona (the house brain). Managed by IPersonaContext.
        /// At most one persona is primary at a time.
        /// </summary>
        public string PrimaryAiContactId { get; set; } = string.Empty;

        /// <summary>
        /// Id of the persona last activated in a chat conversation ("secondary"/current focus).
        /// Managed by IPersonaContext.
        /// </summary>
        public string SecondaryAiContactId { get; set; } = string.Empty;

        public string MCPServerEndpoint { get; set; } = "http://localhost:8080";
        public string UnrealEngineEndpoint { get; set; } = "ws://localhost:8888";
        public string TTSEndpoint { get; set; } = "http://localhost:8881";
        public string? STTEndpoint { get; set; }
        /// <summary>Folder of Chatterbox reference clips (Media/ChatterboxVoices/*.wav).</summary>
        public string ChatterboxVoicesDir { get; set; } = "Media/ChatterboxVoices";
        /// <summary>When Chatterbox HTTP synthesis fails, use System.Speech (Microsoft voices). Set false to avoid wrong-voice fallback; requires app restart.</summary>
        public bool UseWindowsTTSFallback { get; set; } = true;
        /// <summary>Image provider: <c>a2e</c> (cloud, default when token set) or <c>comfyui</c> (local Stability Matrix / ComfyUI only).</summary>
        public string ImageGenerationProvider { get; set; } = "a2e";
        /// <summary>Bearer token from https://video.a2e.ai (API Token). Falls back to env <c>A2E_API_TOKEN</c>.</summary>
        public string A2eApiToken { get; set; } = string.Empty;
        /// <summary>A2E API base URL (global: https://video.a2e.ai, China: https://video.a2e.com.cn).</summary>
        public string A2eApiBaseUrl { get; set; } = "https://video.a2e.ai";
        public string StableDiffusionEndpoint { get; set; } = "http://localhost:8188"; // Legacy name: image endpoint (ComfyUI default)
        /// <summary>Full path to Stability Matrix executable (e.g. C:\...\Stability Matrix.exe). Used to launch the manager and ComfyUI.</summary>
        public string StabilityMatrixPath { get; set; } = string.Empty;
        /// <summary>Full path to ComfyUI portable folder (contains run_nvidia_gpu.bat). Can be the ComfyUI install managed by Stability Matrix.</summary>
        public string ComfyUIPortablePath { get; set; } = string.Empty;
        /// <summary>Full path to a custom ComfyUI workflow JSON file (API format). Use placeholders {{positive}}, {{negative}}, {{width}}, {{height}}, {{seed}}, {{filename_prefix}}.</summary>
        public string ComfyUICustomWorkflowPath { get; set; } = string.Empty;
        /// <summary>Preferred ComfyUI checkpoint for image generation. Leave empty for automatic selection.</summary>
        public string ComfyUIPreferredCheckpoint { get; set; } = "sd_xl_base_1.0.safetensors";
        /// <summary>Color scheme/theme ID (e.g. CyanBlueDark, EmeraldLight). See ThemeManager.Themes for available values.</summary>
        public string ColorScheme { get; set; } = "ObsidianFieldDark";
        public string MT4DataPath { get; set; } = "C:\\Program Files\\MetaTrader 4";
        public string DataBankPath { get; set; } = "Data/Databanks";
        public string LogsPath { get; set; } = "Logs";
        public string MediaPath { get; set; } = "Media";
        /// <summary>
        /// When true, personas keep their voice but must not claim actions (files, tools, trades)
        /// they did not actually perform.
        /// </summary>
        public bool OperationalMode { get; set; } = true;
        public int RefreshIntervalMs { get; set; } = 1000;
        public bool EnableOverlay { get; set; } = true;
        public double OverlayOpacity { get; set; } = 0.85;
        public bool AutoHideTrays { get; set; } = true;
        public int AutoHideDelayMs { get; set; } = 3000;

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
        /// <summary>Ollama embedding model (e.g. nomic-embed-text). Must match <see cref="EmbeddingVectorDimensions"/>.</summary>
        public string OllamaEmbeddingModel { get; set; } = "nomic-embed-text";
        /// <summary>Vector size for pgvector column and Ollama embeddings (e.g. 768 for nomic-embed-text).</summary>
        public int EmbeddingVectorDimensions { get; set; } = 768;
        public double HybridLexicalWeight { get; set; } = 0.5;

        // COVAS: Next (Elite Dangerous) bridge - OpenAI-compatible API for ship computer AI
        public bool CovasBridgeEnabled { get; set; } = false;
        public string CovasBridgeEndpoint { get; set; } = "http://localhost:11435";
        /// <summary>Optional AI contact ID to use as ship computer. If empty, first available contact is used.</summary>
        public string CovasContactId { get; set; } = string.Empty;

        /// <summary>When true, Kestrel exposes the remote companion HTTP API (text + optional audio).</summary>
        public bool RemoteCompanionEnabled { get; set; }

        /// <summary>TCP port for the remote companion API (default 17890).</summary>
        public int RemoteCompanionListenPort { get; set; } = 17890;

        /// <summary>Bearer / X-Api-Key secret. Required when remote companion is enabled; use at least 16 characters.</summary>
        public string RemoteCompanionApiToken { get; set; } = string.Empty;

        /// <summary>Optional AI contact id for remote chat; if empty, primary (or first) AI contact is used.</summary>
        public string RemoteCompanionAiContactId { get; set; } = string.Empty;

        /// <summary>Listen on all interfaces (0.0.0.0) for LAN access. If false, only loopback (recommended with a tunnel).</summary>
        public bool RemoteCompanionListenOnLan { get; set; }

        /// <summary>After each remote reply, send a JSON command to Unreal (see Docs/Unreal_Protocol.md).</summary>
        public bool RemoteCompanionNotifyUnreal { get; set; }

        /// <summary>When true, connect to Unreal on startup and route Victoria chat to the embodied avatar.</summary>
        public bool EnableVictoriaEmbodiment { get; set; } = true;

        /// <summary>Unreal avatar id for the in-scene MetaHuman (e.g. victoria for BP_MHC_Victoria).</summary>
        public string VictoriaUnrealAvatarId { get; set; } = "victoria";

        /// <summary>After desktop SMS/chat replies, notify Unreal (talk + inferred walk/see/touch).</summary>
        public bool NotifyUnrealAfterDesktopChat { get; set; } = true;

        // Autonomy (background agent loop)
        public bool EnableAutonomy { get; set; } = true;
        /// <summary>Runtime autonomy intensity (Off / Low / Mid / Full).</summary>
        public AutonomyLevel AutonomyLevel { get; set; } = AutonomyLevel.Mid;
        /// <summary>Seconds between autonomy ticks (perceive / decide / act).</summary>
        public int AutonomyTickIntervalSeconds { get; set; } = 90;
        /// <summary>Minutes without user chat before idle activities run.</summary>
        public int AutonomyMinIdleMinutes { get; set; } = 2;
        /// <summary>Project priority at or above this is treated as high-priority work.</summary>
        public int AutonomyHighPriorityThreshold { get; set; } = 7;
        /// <summary>Optional AI contact for autonomy; falls back to primary AI.</summary>
        public string AutonomyAiContactId { get; set; } = string.Empty;
        public bool AutonomyEnableArtGeneration { get; set; } = true;
        /// <summary>Cap substantive autonomy actions per rolling hour.</summary>
        public int AutonomyMaxActionsPerHour { get; set; } = 6;
        public int AutonomyMaxArtPerHour { get; set; } = 2;
        /// <summary>Folder for autonomy state, logs, and generated artifacts.</summary>
        public string AutonomyDataPath { get; set; } = "Data/Autonomy";
        /// <summary>Allow autonomy to start its own self-initiated projects from internal drives.</summary>
        public bool AutonomyEnableSelfGoals { get; set; } = true;
        /// <summary>Cap on self-initiated projects created per rolling day.</summary>
        public int AutonomyMaxSelfGoalsPerDay { get; set; } = 3;

        /// <summary>Minimum dominant drive (0–1) before self-goal generation runs.</summary>
        public double AutonomySelfGoalDriveThreshold { get; set; } = 0.65;

        /// <summary>Max concurrent self-initiated open projects before goal generation is blocked.</summary>
        public int AutonomyMaxActiveSelfProjects { get; set; } = 3;

        /// <summary>Substantive actions while user guidance is active before it auto-clears.</summary>
        public int AutonomyUserGuidanceMaxTicks { get; set; } = 3;

        /// <summary>Max persisted interest tags Victoria actively deepens.</summary>
        public int AutonomyMaxInterestTags { get; set; } = 3;

        /// <summary>When true, background service polls MT4 quotes across <see cref="TradingWatchSymbols"/>.</summary>
        public bool TradingWatchEnabled { get; set; } = true;

        /// <summary>Comma-separated base symbols (must be in MT4 Market Watch). EA reads Watchlist.json.</summary>
        public string TradingWatchSymbols { get; set; } =
            "EURUSD,GBPUSD,USDJPY,AUDUSD,USDCAD,USDCHF,NZDUSD,EURGBP,EURJPY,GBPJPY,XAUUSD,XAGUSD,US30,US500,NAS100";

        /// <summary>Seconds between multi-pair quote scans.</summary>
        public int TradingWatchIntervalSeconds { get; set; } = 30;

        /// <summary>Alert when mid price moves at least this many pips vs last scan.</summary>
        public double TradingWatchPipMoveThreshold { get; set; } = 8;

        /// <summary>Alert when spread exceeds this many pips.</summary>
        public double TradingWatchMaxSpreadPips { get; set; } = 25;

        /// <summary>Run RSI/MACD/MA technical scan on H1 bars (no LLM).</summary>
        public bool TradingWatchTechnicalEnabled { get; set; } = true;

        /// <summary>Seconds between technical scans across the watchlist.</summary>
        public int TradingWatchTechnicalIntervalSeconds { get; set; } = 300;

        /// <summary>H1 bars to load per symbol for technical signals.</summary>
        public int TradingWatchTechnicalBarCount { get; set; } = 120;

        /// <summary>Priority for the dedicated MT4 Market Watch project (should be ≥ AutonomyHighPriorityThreshold).</summary>
        public int TradingWatchProjectPriority { get; set; } = 9;

        // Streaming voice-call engine (On-Device-Speech-to-Speech-Conversational-AI)
        /// <summary>When true, voice calls launch the external streaming speech-to-speech engine instead of the legacy push-to-talk STT/TTS path.</summary>
        public bool VoiceEngineEnabled { get; set; } = true;
        /// <summary>Path to the engine repo folder. Empty = auto-detect by walking up from the app directory looking for "On-Device-Speech-to-Speech-Conversational-AI".</summary>
        public string VoiceEngineDirectory { get; set; } = string.Empty;
        /// <summary>Path to the engine's Python interpreter. Empty = {VoiceEngineDirectory}\.venv\Scripts\python.exe.</summary>
        public string VoiceEnginePython { get; set; } = string.Empty;
        /// <summary>Engine entry script (relative to the engine directory).</summary>
        public string VoiceEngineScript { get; set; } = "speech_to_speech.py";
        /// <summary>Default Chatterbox reference voice (wav stem) when a persona has no voice set.</summary>
        public string VoiceEngineVoice { get; set; } = "default";
        /// <summary>Show the engine's console window during calls (useful for live transcripts/diagnostics).</summary>
        public bool VoiceEngineShowConsole { get; set; } = true;
        /// <summary>Microphone boost for the streaming voice engine (1 = none, 4–6 typical for quiet mics).</summary>
        public float VoiceEngineInputGain { get; set; } = 4f;
        /// <summary>Speech detection threshold for the streaming voice engine (lower = more sensitive).</summary>
        public float VoiceEngineSilenceThreshold { get; set; } = 0.003f;
        /// <summary>Amplification applied to chat push-to-talk recordings before STT (1 = none).</summary>
        public float ChatMicRecordingGain { get; set; } = 4f;
    }
}
