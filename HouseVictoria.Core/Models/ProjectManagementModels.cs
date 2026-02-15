namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Represents a project or goal
    /// </summary>
    public class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ProjectType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 5; // 1-10
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime Deadline { get; set; } = DateTime.Now.AddDays(30);
        public ProjectPhase Phase { get; set; } = ProjectPhase.Planning;
        public double CompletionPercentage { get; set; } = 0;
        public List<string> Roadblocks { get; set; } = new();
        public string? AssignedAIId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastModifiedAt { get; set; }
    }

    /// <summary>
    /// Log entry for project activities
    /// </summary>
    public class ProjectLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty; // User or AI Contact ID
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Artifact (file, media, transcript) generated for the project
    /// </summary>
    public class ProjectArtifact
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public ArtifactType Type { get; set; }
        public string? Description { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = string.Empty; // User or AI Contact ID
    }

    public enum ProjectType
    {
        Coding,
        Finance,
        Design,
        Research,
        Writing,
        Business,
        Personal,
        Other
    }

    public enum ProjectPhase
    {
        Planning,
        Research,
        Development,
        Testing,
        Review,
        Deployment,
        Maintenance,
        Completed
    }

    public enum ArtifactType
    {
        File,
        Image,
        Video,
        Audio,
        Document,
        Transcript,
        Code,
        Diagram,
        Other
    }
}
