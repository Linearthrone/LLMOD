namespace HouseVictoria.Core.Models
{
    public enum JournalStatus
    {
        Active,
        Concluded
    }

    public enum JournalEntryKind
    {
        Preface,
        Research,
        ProjectWork,
        Reflection,
        Art,
        Thought,
        Environment,
        GldActivity,
        Conclusion
    }

    /// <summary>External publications/URLs vs our own generated artifacts.</summary>
    public enum ReferenceKind
    {
        External,
        Technology,
        InternalArtifact
    }

    /// <summary>A cited source or reference material attached to research or journal entries.</summary>
    public class ReferencedMaterial
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ReferenceKind Kind { get; set; } = ReferenceKind.External;
        public string Title { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? Url { get; set; }
        public string? FilePath { get; set; }
        public string? Notes { get; set; }
        public DateTime CitedAt { get; set; } = DateTime.Now;
    }

    /// <summary>Generated research output file tracked in the journal appendix.</summary>
    public class GeneratedResearchFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? EntryTitle { get; set; }
    }

    /// <summary>One page-worth of content within a research journal.</summary>
    public class JournalPageEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public JournalEntryKind Kind { get; set; } = JournalEntryKind.Thought;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<ReferencedMaterial> References { get; set; } = new();
        public List<string> LinkedFilePaths { get; set; } = new();
        public string? AutonomyJournalEntryId { get; set; }
        public AutonomyActivityKind? SourceActivity { get; set; }
    }

    /// <summary>
    /// A topic- or goal-scoped journal that accumulates everything Victoria does
    /// on a project, research thread, or line of thought.
    /// </summary>
    public class ResearchJournal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        /// <summary>Why she chose to pursue this topic — no wrong answers.</summary>
        public string Preface { get; set; } = string.Empty;
        public JournalStatus Status { get; set; } = JournalStatus.Active;
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? ConcludedAt { get; set; }
        public string? ConclusionSummary { get; set; }
        public string? ConclusionImplications { get; set; }
        public List<ReferencedMaterial> ConclusionCitations { get; set; } = new();
        public List<JournalPageEntry> Entries { get; set; } = new();
    }

    public class JournalUpdatedEventArgs : EventArgs
    {
        public ResearchJournal Journal { get; set; } = new();
    }
}
