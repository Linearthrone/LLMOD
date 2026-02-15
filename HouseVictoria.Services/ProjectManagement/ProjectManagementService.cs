using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.ProjectManagement
{
    /// <summary>
    /// Service for managing projects and goals
    /// </summary>
    public class ProjectManagementService : IProjectManagementService
    {
        private readonly List<Project> _projects = new();
        private readonly Dictionary<string, List<ProjectLog>> _projectLogs = new();
        private readonly Dictionary<string, List<ProjectArtifact>> _projectArtifacts = new();

        public event EventHandler<ProjectUpdatedEventArgs>? ProjectUpdated;
        public event EventHandler<MilestoneReachedEventArgs>? MilestoneReached;

        public ProjectManagementService()
        {
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            // Initialize with sample project
            var sampleProject = new Project
            {
                Id = "1",
                Name = "Sample Project",
                Type = ProjectType.Coding,
                Description = "A sample project to demonstrate functionality",
                Priority = 5,
                StartDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(30),
                Phase = ProjectPhase.Planning,
                CompletionPercentage = 0
            };
            _projects.Add(sampleProject);
        }

        public async Task<Project> CreateProjectAsync(Project project)
        {
            project.Id = Guid.NewGuid().ToString();
            project.CreatedAt = DateTime.Now;
            project.LastModifiedAt = DateTime.Now;
            _projects.Add(project);
            ProjectUpdated?.Invoke(this, new ProjectUpdatedEventArgs { Project = project });
            return await Task.FromResult(project);
        }

        public async Task<Project?> GetProjectAsync(string projectId)
        {
            return await Task.FromResult(_projects.FirstOrDefault(p => p.Id == projectId));
        }

        public async Task<List<Project>> GetAllProjectsAsync()
        {
            return await Task.FromResult(_projects);
        }

        public async Task<Project> UpdateProjectAsync(Project project)
        {
            var existing = _projects.FirstOrDefault(p => p.Id == project.Id);
            if (existing != null)
            {
                var index = _projects.IndexOf(existing);
                project.LastModifiedAt = DateTime.Now;
                _projects[index] = project;
                ProjectUpdated?.Invoke(this, new ProjectUpdatedEventArgs { Project = project });
            }
            return await Task.FromResult(project);
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId);
            if (project != null)
            {
                _projects.Remove(project);
                _projectLogs.Remove(projectId);
                _projectArtifacts.Remove(projectId);
            }
            await Task.CompletedTask;
        }

        public async Task<ProjectLog> AddLogEntryAsync(string projectId, ProjectLog log)
        {
            log.Id = Guid.NewGuid().ToString();
            log.Timestamp = DateTime.Now;

            if (!_projectLogs.ContainsKey(projectId))
            {
                _projectLogs[projectId] = new List<ProjectLog>();
            }
            _projectLogs[projectId].Add(log);

            // Check if this is an AI updating the project phase
            if (log.Action.Contains("phase", StringComparison.OrdinalIgnoreCase))
            {
                var project = await GetProjectAsync(projectId);
                if (project != null)
                {
                    var newPhase = DetectPhaseFromAction(log.Action, project.Phase);
                    if (newPhase != project.Phase && newPhase != ProjectPhase.Completed)
                    {
                        await UpdateProjectPhaseAsync(projectId, newPhase);
                    }
                }
            }

            return await Task.FromResult(log);
        }

        public async Task<List<ProjectLog>> GetProjectLogsAsync(string projectId)
        {
            _projectLogs.TryGetValue(projectId, out var logs);
            return await Task.FromResult(logs ?? new List<ProjectLog>());
        }

        public async Task<ProjectArtifact> AddArtifactAsync(string projectId, ProjectArtifact artifact)
        {
            artifact.Id = Guid.NewGuid().ToString();
            artifact.CreatedAt = DateTime.Now;

            if (!_projectArtifacts.ContainsKey(projectId))
            {
                _projectArtifacts[projectId] = new List<ProjectArtifact>();
            }
            _projectArtifacts[projectId].Add(artifact);

            return await Task.FromResult(artifact);
        }

        public async Task<List<ProjectArtifact>> GetArtifactsAsync(string projectId)
        {
            _projectArtifacts.TryGetValue(projectId, out var artifacts);
            return await Task.FromResult(artifacts ?? new List<ProjectArtifact>());
        }

        public async Task DeleteArtifactAsync(string projectId, string artifactId)
        {
            if (_projectArtifacts.TryGetValue(projectId, out var artifacts))
            {
                var artifact = artifacts.FirstOrDefault(a => a.Id == artifactId);
                if (artifact != null)
                {
                    artifacts.Remove(artifact);
                    
                    // If no artifacts remain, remove the project entry
                    if (artifacts.Count == 0)
                    {
                        _projectArtifacts.Remove(projectId);
                    }
                }
            }
            await Task.CompletedTask;
        }

        public async Task<List<Project>> GetProjectsByPriorityAsync(int minPriority = 1, int maxPriority = 10)
        {
            var filtered = _projects
                .Where(p => p.Priority >= minPriority && p.Priority <= maxPriority && p.Phase != ProjectPhase.Completed)
                .OrderByDescending(p => p.Priority)
                .ThenByDescending(p => p.CompletionPercentage)
                .ToList();

            return await Task.FromResult(filtered);
        }

        public async Task UpdateProjectPhaseAsync(string projectId, ProjectPhase phase)
        {
            var project = await GetProjectAsync(projectId);
            if (project != null)
            {
                var previousPhase = project.Phase;
                project.Phase = phase;

                // Update completion percentage based on phase
                project.CompletionPercentage = phase switch
                {
                    ProjectPhase.Planning => 10,
                    ProjectPhase.Research => 25,
                    ProjectPhase.Development => 50,
                    ProjectPhase.Testing => 75,
                    ProjectPhase.Review => 85,
                    ProjectPhase.Deployment => 95,
                    ProjectPhase.Completed => 100,
                    _ => project.CompletionPercentage
                };

                project.LastModifiedAt = DateTime.Now;
                MilestoneReached?.Invoke(this, new MilestoneReachedEventArgs
                {
                    ProjectId = projectId,
                    PreviousPhase = previousPhase,
                    CurrentPhase = phase,
                    AIContactId = project.AssignedAIId ?? "System"
                });

                ProjectUpdated?.Invoke(this, new ProjectUpdatedEventArgs { Project = project });
            }
            await Task.CompletedTask;
        }

        private ProjectPhase DetectPhaseFromAction(string action, ProjectPhase currentPhase)
        {
            var lowerAction = action.ToLower();

            if (lowerAction.Contains("plan")) return ProjectPhase.Planning;
            if (lowerAction.Contains("research")) return ProjectPhase.Research;
            if (lowerAction.Contains("develop") || lowerAction.Contains("code")) return ProjectPhase.Development;
            if (lowerAction.Contains("test")) return ProjectPhase.Testing;
            if (lowerAction.Contains("review")) return ProjectPhase.Review;
            if (lowerAction.Contains("deploy") || lowerAction.Contains("launch")) return ProjectPhase.Deployment;
            if (lowerAction.Contains("complete") || lowerAction.Contains("finish")) return ProjectPhase.Completed;

            return currentPhase;
        }
    }
}
