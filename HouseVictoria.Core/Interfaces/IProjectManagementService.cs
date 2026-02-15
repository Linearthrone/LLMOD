using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for project and goal management
    /// </summary>
    public interface IProjectManagementService
    {
        Task<Project> CreateProjectAsync(Project project);
        Task<Project?> GetProjectAsync(string projectId);
        Task<List<Project>> GetAllProjectsAsync();
        Task<Project> UpdateProjectAsync(Project project);
        Task DeleteProjectAsync(string projectId);
        Task<ProjectLog> AddLogEntryAsync(string projectId, ProjectLog log);
        Task<List<ProjectLog>> GetProjectLogsAsync(string projectId);
        Task<ProjectArtifact> AddArtifactAsync(string projectId, ProjectArtifact artifact);
        Task<List<ProjectArtifact>> GetArtifactsAsync(string projectId);
        Task DeleteArtifactAsync(string projectId, string artifactId);
        Task<List<Project>> GetProjectsByPriorityAsync(int minPriority = 1, int maxPriority = 10);
        Task UpdateProjectPhaseAsync(string projectId, ProjectPhase phase);
        event EventHandler<ProjectUpdatedEventArgs>? ProjectUpdated;
        event EventHandler<MilestoneReachedEventArgs>? MilestoneReached;
    }

    public class ProjectUpdatedEventArgs : EventArgs
    {
        public Project Project { get; set; } = null!;
    }

    public class MilestoneReachedEventArgs : EventArgs
    {
        public string ProjectId { get; set; } = string.Empty;
        public ProjectPhase PreviousPhase { get; set; }
        public ProjectPhase CurrentPhase { get; set; }
        public string AIContactId { get; set; } = string.Empty;
    }
}
