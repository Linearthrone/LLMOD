namespace HouseVictoria.Core.Models
{
    /// <summary>Runtime autonomy intensity — cycles from the vitals drawer.</summary>
    public enum AutonomyLevel
    {
        Off,
        Low,
        Mid,
        Full
    }

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
        public DateTime? CurrentActivityStartedUtc { get; set; }
        public string? PreviousActivitySummary { get; set; }
        public DateTime? PreviousActivityStartedUtc { get; set; }
        public DateTime? PreviousActivityEndedUtc { get; set; }
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
        ExecuteTrade,
        RunBacktest,
        ScanMarkets,
        SkippedCooldown,
        GenerateGoal
    }

    /// <summary>A recent action remembered for anti-repetition reasoning.</summary>
    public class AutonomyRecentActivity
    {
        public AutonomyActivityKind Activity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Topic { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A single concrete step in a multi-tick project plan.</summary>
    public class AutonomyPlanStep
    {
        public string Description { get; set; } = string.Empty;
        public bool Done { get; set; }
        public DateTime? CompletedUtc { get; set; }
    }

    /// <summary>A persisted, step-by-step plan for advancing a project across ticks.</summary>
    public class AutonomyPlan
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public List<AutonomyPlanStep> Steps { get; set; } = new();
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public int DoneCount => Steps.Count(s => s.Done);
        public bool IsComplete => Steps.Count > 0 && Steps.All(s => s.Done);
        public AutonomyPlanStep? NextStep => Steps.FirstOrDefault(s => !s.Done);
        public double CompletionFraction => Steps.Count == 0 ? 0 : (double)DoneCount / Steps.Count;
    }

    /// <summary>A scored record of how a substantive action turned out, used as feedback.</summary>
    public class AutonomyOutcome
    {
        public AutonomyActivityKind Activity { get; set; }
        public string? Topic { get; set; }
        public string? ProjectId { get; set; }
        /// <summary>0.0 (poor) – 1.0 (excellent).</summary>
        public double Score { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Persisted autonomy runtime state.</summary>
    public class AutonomyRuntimeState
    {
        public DateTime? LastTickUtc { get; set; }
        public DateTime? LastUserActivityUtc { get; set; }
        public DateTime? LastActionUtc { get; set; }
        public AutonomyActivityKind LastActivity { get; set; } = AutonomyActivityKind.None;
        public string? LastActivitySummary { get; set; }
        /// <summary>When <see cref="LastActivity"/> / summary last changed.</summary>
        public DateTime? CurrentActivityStartedUtc { get; set; }
        public AutonomyActivityKind PreviousActivity { get; set; } = AutonomyActivityKind.None;
        public string? PreviousActivitySummary { get; set; }
        public DateTime? PreviousActivityStartedUtc { get; set; }
        public DateTime? PreviousActivityEndedUtc { get; set; }
        public string? CurrentFocusProjectId { get; set; }
        public Dictionary<string, double> Drives { get; set; } = new()
        {
            ["curiosity"] = 0.5,
            ["creativity"] = 0.5,
            ["social"] = 0.3,
            ["boredom"] = 0.2,
            ["purpose"] = 0.5
        };
        /// <summary>Resting values each drive decays toward when not stimulated or satisfied.</summary>
        public Dictionary<string, double> DriveBaselines { get; set; } = new()
        {
            ["curiosity"] = 0.5,
            ["creativity"] = 0.5,
            ["social"] = 0.3,
            ["boredom"] = 0.2,
            ["purpose"] = 0.5
        };

        /// <summary>Persisted interest weights (tag → 0–1). Decays slowly; grows when topics are explored.</summary>
        public Dictionary<string, double> InterestWeights { get; set; } = new();

        /// <summary>Top interest tags Victoria is actively deepening (max 3).</summary>
        public List<string> ActiveInterestTags { get; set; } = new();
        public int ActionsThisHour { get; set; }
        public DateTime HourWindowStartUtc { get; set; } = DateTime.UtcNow;
        public int ArtGeneratedThisHour { get; set; }
        public bool IsRunning { get; set; }
        public long TotalTicks { get; set; }
        public long TotalActions { get; set; }

        // Anti-repetition memory window (most recent first is not guaranteed; ordered by append).
        public List<AutonomyRecentActivity> RecentActivities { get; set; } = new();

        // Multi-tick plans, keyed implicitly by ProjectId.
        public List<AutonomyPlan> Plans { get; set; } = new();

        // Rolling outcome feedback for recent substantive actions.
        public List<AutonomyOutcome> RecentOutcomes { get; set; } = new();

        // Persisted anti-fixation tracking (survives restart).
        public Dictionary<string, DateTime> ProjectCooldownUntil { get; set; } = new();
        public Dictionary<string, DateTime> TopicCooldownUntil { get; set; } = new();
        public string? LastFocusProjectId { get; set; }
        public int SameFocusStreak { get; set; }

        // Self-initiated goal budget (per rolling day).
        public int SelfGoalsToday { get; set; }
        public DateTime SelfGoalDayStartUtc { get; set; } = DateTime.UtcNow;

        // Decision-failure backoff.
        public int ConsecutiveDecisionFailures { get; set; }

        /// <summary>User-provided nudge when autonomy seems stuck or fixated.</summary>
        public string? UserGuidanceSuggestion { get; set; }

        /// <summary>Substantive actions remaining before user guidance auto-clears.</summary>
        public int UserGuidanceTicksRemaining { get; set; }
    }

    /// <summary>One line from autonomy activity.log within a rolling window.</summary>
    public class AutonomyActionLogEntry
    {
        public DateTime TimestampLocal { get; set; }
        public string Text { get; set; } = string.Empty;
        public AutonomyActivityKind? ActivityKind { get; set; }
    }

    /// <summary>Live autonomy/cognition control panel snapshot for the vitals drawer.</summary>
    public class AutonomyControlSnapshot
    {
        public Dictionary<string, double> Drives { get; set; } = new();
        public List<AutonomyInterestTag> Interests { get; set; } = new();
        public int ActionsThisHour { get; set; }
        public int MaxActionsPerHour { get; set; }
        public int ArtGeneratedThisHour { get; set; }
        public int MaxArtPerHour { get; set; }
        public int SelfGoalsToday { get; set; }
        public int MaxSelfGoalsPerDay { get; set; }
        public string? UserGuidance { get; set; }
        public int UserGuidanceTicksRemaining { get; set; }
        public long TotalActions { get; set; }
        public long TotalTicks { get; set; }
        public bool IsRunning { get; set; }
        public AutonomyLevel Level { get; set; }
    }

    public class AutonomyInterestTag
    {
        public string Tag { get; set; } = string.Empty;
        public double Weight { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>Partial autonomy settings update from the vitals control drawer.</summary>
    public class AutonomySettingsUpdate
    {
        public bool? EnableAutonomy { get; set; }
        public AutonomyLevel? Level { get; set; }
        public int? TickIntervalSeconds { get; set; }
        public int? MinIdleMinutes { get; set; }
        public int? HighPriorityThreshold { get; set; }
        public bool? EnableArtGeneration { get; set; }
        public int? MaxActionsPerHour { get; set; }
        public int? MaxArtPerHour { get; set; }
        public bool? EnableSelfGoals { get; set; }
        public int? MaxSelfGoalsPerDay { get; set; }
        public double? SelfGoalDriveThreshold { get; set; }
        public int? MaxActiveSelfProjects { get; set; }
        public int? UserGuidanceMaxTicks { get; set; }
        public string? UserGuidance { get; set; }
        public bool ClearUserGuidance { get; set; }
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
