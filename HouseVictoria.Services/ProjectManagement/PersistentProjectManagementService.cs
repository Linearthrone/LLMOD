using System.Text.Json;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.ProjectManagement
{
    /// <summary>
    /// File-backed project store so goals survive app restarts and autonomy can build on them.
    /// </summary>
    public sealed class PersistentProjectManagementService : IProjectManagementService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _storePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly List<Project> _projects = new();
        private readonly Dictionary<string, List<ProjectLog>> _projectLogs = new();
        private readonly Dictionary<string, List<ProjectArtifact>> _projectArtifacts = new();

        public event EventHandler<ProjectUpdatedEventArgs>? ProjectUpdated;
        public event EventHandler<MilestoneReachedEventArgs>? MilestoneReached;

        public PersistentProjectManagementService(AppConfig appConfig)
        {
            var basePath = appConfig.AutonomyDataPath;
            if (!Path.IsPathRooted(basePath))
            {
                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                             ?? AppDomain.CurrentDomain.BaseDirectory;
                basePath = Path.Combine(appDir, basePath);
            }

            Directory.CreateDirectory(basePath);
            _storePath = Path.Combine(basePath, "projects.json");
            LoadFromDisk();
        }

        private void LoadFromDisk()
        {
            if (!File.Exists(_storePath))
            {
                SeedDefaultProjects();
                SaveToDisk();
                return;
            }

            try
            {
                var json = File.ReadAllText(_storePath);
                var bundle = JsonSerializer.Deserialize<ProjectStoreBundle>(json, JsonOptions);
                if (bundle == null)
                {
                    SeedDefaultProjects();
                    return;
                }

                _projects.Clear();
                _projects.AddRange(bundle.Projects ?? new List<Project>());
                _projectLogs.Clear();
                if (bundle.Logs != null)
                {
                    foreach (var kv in bundle.Logs)
                        _projectLogs[kv.Key] = kv.Value;
                }

                _projectArtifacts.Clear();
                if (bundle.Artifacts != null)
                {
                    foreach (var kv in bundle.Artifacts)
                        _projectArtifacts[kv.Key] = kv.Value;
                }
            }
            catch
            {
                SeedDefaultProjects();
            }
        }

        private void SeedDefaultProjects()
        {
            _projects.Clear();
            _projects.Add(new Project
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Victoria's creative studio",
                Type = ProjectType.Design,
                Description = "Personal art experiments, visual studies, and aesthetic exploration.",
                Priority = 4,
                Phase = ProjectPhase.Development,
                CompletionPercentage = 5
            });
            _projects.Add(new Project
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Research & curiosity backlog",
                Type = ProjectType.Research,
                Description = "Topics I want to investigate, notes, and half-formed ideas.",
                Priority = 5,
                Phase = ProjectPhase.Research,
                CompletionPercentage = 10
            });
        }

        private void SaveToDisk()
        {
            var bundle = new ProjectStoreBundle
            {
                Projects = _projects,
                Logs = _projectLogs,
                Artifacts = _projectArtifacts
            };
            var json = JsonSerializer.Serialize(bundle, JsonOptions);
            File.WriteAllText(_storePath, json);
        }

        private async Task PersistAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                SaveToDisk();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<Project> CreateProjectAsync(Project project)
        {
            project.Id = string.IsNullOrWhiteSpace(project.Id) ? Guid.NewGuid().ToString() : project.Id;
            project.CreatedAt = DateTime.Now;
            project.LastModifiedAt = DateTime.Now;
            _projects.Add(project);
            ProjectUpdated?.Invoke(this, new ProjectUpdatedEventArgs { Project = project });
            await PersistAsync().ConfigureAwait(false);
            return project;
        }

        public Task<Project?> GetProjectAsync(string projectId) =>
            Task.FromResult(_projects.FirstOrDefault(p => p.Id == projectId));

        public Task<List<Project>> GetAllProjectsAsync() =>
            Task.FromResult(_projects.ToList());

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

            await PersistAsync().ConfigureAwait(false);
            return project;
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

            await PersistAsync().ConfigureAwait(false);
        }

        public async Task<ProjectLog> AddLogEntryAsync(string projectId, ProjectLog log)
        {
            log.Id = Guid.NewGuid().ToString();
            log.Timestamp = DateTime.Now;
            if (!_projectLogs.ContainsKey(projectId))
                _projectLogs[projectId] = new List<ProjectLog>();
            _projectLogs[projectId].Add(log);

            if (log.Action.Contains("phase", StringComparison.OrdinalIgnoreCase))
            {
                var project = await GetProjectAsync(projectId).ConfigureAwait(false);
                if (project != null)
                {
                    var newPhase = DetectPhaseFromAction(log.Action, project.Phase);
                    if (newPhase != project.Phase && newPhase != ProjectPhase.Completed)
                        await UpdateProjectPhaseAsync(projectId, newPhase).ConfigureAwait(false);
                }
            }

            await PersistAsync().ConfigureAwait(false);
            return log;
        }

        public Task<List<ProjectLog>> GetProjectLogsAsync(string projectId)
        {
            _projectLogs.TryGetValue(projectId, out var logs);
            return Task.FromResult(logs ?? new List<ProjectLog>());
        }

        public async Task<ProjectArtifact> AddArtifactAsync(string projectId, ProjectArtifact artifact)
        {
            artifact.Id = Guid.NewGuid().ToString();
            artifact.CreatedAt = DateTime.Now;
            if (!_projectArtifacts.ContainsKey(projectId))
                _projectArtifacts[projectId] = new List<ProjectArtifact>();
            _projectArtifacts[projectId].Add(artifact);
            await PersistAsync().ConfigureAwait(false);
            return artifact;
        }

        public Task<List<ProjectArtifact>> GetArtifactsAsync(string projectId)
        {
            _projectArtifacts.TryGetValue(projectId, out var artifacts);
            return Task.FromResult(artifacts ?? new List<ProjectArtifact>());
        }

        public async Task DeleteArtifactAsync(string projectId, string artifactId)
        {
            if (_projectArtifacts.TryGetValue(projectId, out var artifacts))
            {
                var artifact = artifacts.FirstOrDefault(a => a.Id == artifactId);
                if (artifact != null)
                {
                    artifacts.Remove(artifact);
                    if (artifacts.Count == 0)
                        _projectArtifacts.Remove(projectId);
                }
            }

            await PersistAsync().ConfigureAwait(false);
        }

        public Task<List<Project>> GetProjectsByPriorityAsync(int minPriority = 1, int maxPriority = 10)
        {
            var filtered = _projects
                .Where(p => p.Priority >= minPriority && p.Priority <= maxPriority && p.Phase != ProjectPhase.Completed)
                .OrderByDescending(p => p.Priority)
                .ThenByDescending(p => p.CompletionPercentage)
                .ToList();
            return Task.FromResult(filtered);
        }

        public async Task UpdateProjectPhaseAsync(string projectId, ProjectPhase phase)
        {
            var project = await GetProjectAsync(projectId).ConfigureAwait(false);
            if (project == null)
                return;

            var previousPhase = project.Phase;
            project.Phase = phase;
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
                AIContactId = project.AssignedAIId ?? "Autonomy"
            });
            ProjectUpdated?.Invoke(this, new ProjectUpdatedEventArgs { Project = project });
            await PersistAsync().ConfigureAwait(false);
        }

        private static ProjectPhase DetectPhaseFromAction(string action, ProjectPhase currentPhase)
        {
            var lowerAction = action.ToLowerInvariant();
            if (lowerAction.Contains("plan")) return ProjectPhase.Planning;
            if (lowerAction.Contains("research")) return ProjectPhase.Research;
            if (lowerAction.Contains("develop") || lowerAction.Contains("code")) return ProjectPhase.Development;
            if (lowerAction.Contains("test")) return ProjectPhase.Testing;
            if (lowerAction.Contains("review")) return ProjectPhase.Review;
            if (lowerAction.Contains("deploy") || lowerAction.Contains("launch")) return ProjectPhase.Deployment;
            if (lowerAction.Contains("complete") || lowerAction.Contains("finish")) return ProjectPhase.Completed;
            return currentPhase;
        }

        private sealed class ProjectStoreBundle
        {
            public List<Project>? Projects { get; set; }
            public Dictionary<string, List<ProjectLog>>? Logs { get; set; }
            public Dictionary<string, List<ProjectArtifact>>? Artifacts { get; set; }
        }
    }
}
