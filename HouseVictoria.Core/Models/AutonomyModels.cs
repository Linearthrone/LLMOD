namespace HouseVictoria.Core.Models
{
    /// <summary>ECG-style cognition rhythm — drives pulse rate and waveform shape in the UI.</summary>
    public enum CognitionVitalRhythm
    {
        Resting,
        Waiting,
        Reflecting,
        Research,
        CreativeCalm,
        ProjectWork,
        PriorityUrgent,
        TradingActive,
        Environment
    }

    /// <summary>Live vitals snapshot for telemetry / heart-monitor UI.</summary>
    public class CognitionVitalsSnapshot
    {
        public CognitionVitalRhythm Rhythm { get; set; } = CognitionVitalRhythm.Resting;
        public string Label { get; set; } = "At rest";
        public double BeatsPerMinute { get; set; } = 52;
        /// <summary>0–1 waveform amplitude.</summary>
        public double Intensity { get; set; } = 0.25;
        public string WaveColorHex { get; set; } = "#4FC3F7";
        public AutonomyActivityKind LastActivity { get; set; } = AutonomyActivityKind.None;
        public string? LastActivitySummary { get; set; }
        public bool AutonomyRunning { get; set; }
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class CognitionVitalsChangedEventArgs : EventArgs
    {
        public CognitionVitalsSnapshot Vitals { get; set; } = new();
    }

    /// <summary>What the agent chose to do on an autonomy tick.</summary>
    public enum AutonomyActivityKind
    {
        None,
        WaitingForUserQuiet,
        WorkOnPriorityProject,
        CreateArt,
        WriteResearch,
        AdvancePersonalProject,
        Reflect,
        ExploreEnvironment,
        SkippedCooldown
    }

    /// <summary>Persisted autonomy runtime state.</summary>
    public class AutonomyRuntimeState
    {
        public DateTime? LastTickUtc { get; set; }
        public DateTime? LastUserActivityUtc { get; set; }
        public DateTime? LastActionUtc { get; set; }
        public AutonomyActivityKind LastActivity { get; set; } = AutonomyActivityKind.None;
        public string? LastActivitySummary { get; set; }
        public string? CurrentFocusProjectId { get; set; }
        public Dictionary<string, double> Drives { get; set; } = new()
        {
            ["curiosity"] = 0.5,
            ["creativity"] = 0.5,
            ["social"] = 0.3,
            ["boredom"] = 0.2
        };
        public int ActionsThisHour { get; set; }
        public DateTime HourWindowStartUtc { get; set; } = DateTime.UtcNow;
        public int ArtGeneratedThisHour { get; set; }
        public bool IsRunning { get; set; }
        public long TotalTicks { get; set; }
        public long TotalActions { get; set; }
    }

    /// <summary>LLM decision payload for one autonomy tick.</summary>
    public class AutonomyDecision
    {
        public string Mode { get; set; } = "idle"; // "priority" | "idle" | "wait"
        public string Activity { get; set; } = "reflect";
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class AutonomyActivityEventArgs : EventArgs
    {
        public AutonomyActivityKind Activity { get; set; }
        public string Summary { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Rich autonomy journal record written to disk for GLD and audit.</summary>
    public class AutonomyJournalEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public AutonomyActivityKind Activity { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? Body { get; set; }
        public List<string> LinkedFilePaths { get; set; } = new();
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
    }
}
