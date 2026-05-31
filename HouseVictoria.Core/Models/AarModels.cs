namespace HouseVictoria.Core.Models
{
    /// <summary>How fully a project met its stated goal.</summary>
    public enum AarCompletionLevel
    {
        Partial,
        Full,
        Exceeded
    }

    /// <summary>Review state of an After Action Report in the AAR tray.</summary>
    public enum AarStatus
    {
        Pending,
        Accepted,
        Rejected
    }

    /// <summary>
    /// An After Action Report generated when a project is completed. It is reviewed by the user
    /// (accept / reject) from the AAR tray before being cleared.
    /// </summary>
    public class AfterActionReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public ProjectType ProjectType { get; set; }

        /// <summary>What the project was about.</summary>
        public string Summary { get; set; } = string.Empty;
        /// <summary>What the goal was.</summary>
        public string Goal { get; set; } = string.Empty;
        /// <summary>Narrative of how it turned out.</summary>
        public string Outcome { get; set; } = string.Empty;

        public AarCompletionLevel CompletionLevel { get; set; } = AarCompletionLevel.Full;
        public double CompletionPercentage { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime Deadline { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.Now;
        /// <summary>Total wall-clock time from start to completion.</summary>
        public TimeSpan TimeInvested { get; set; }
        /// <summary>How many logged work sessions/actions went into it.</summary>
        public int WorkSessionCount { get; set; }

        public bool IsDeliverable { get; set; }
        public string? DeliverableName { get; set; }
        public string? DeliverablePath { get; set; }
        public List<string> DeliverablePaths { get; set; } = new();

        public AarStatus Status { get; set; } = AarStatus.Pending;
        public string? ContactId { get; set; }

        // Populated when the user rejects the report.
        public string? RejectionReason { get; set; }
        public string? ImprovementSuggestions { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public bool WasOnTime => CompletedAt <= Deadline;
        public string TimeInvestedLabel => FormatTimeInvested(TimeInvested);

        private static string FormatTimeInvested(TimeSpan span)
        {
            if (span < TimeSpan.Zero)
                span = TimeSpan.Zero;
            if (span.TotalDays >= 1)
                return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            return $"{span.Minutes}m";
        }
    }

    /// <summary>User feedback supplied when an After Action Report is rejected.</summary>
    public class AarRejectionFeedback
    {
        public string Reason { get; set; } = string.Empty;
        public string Suggestions { get; set; } = string.Empty;
        public int NewPriority { get; set; } = 5;
        public DateTime NewStartDate { get; set; } = DateTime.Now;
        public DateTime NewDeadline { get; set; } = DateTime.Now.AddDays(14);
    }

    public class AarReportsChangedEventArgs : EventArgs
    {
        public AfterActionReport? Report { get; set; }
    }
}
