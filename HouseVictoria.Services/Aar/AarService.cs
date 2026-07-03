using System.Text.Json;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;
using HouseVictoria.Services.ProjectManagement;

namespace HouseVictoria.Services.Aar
{
    /// <summary>
    /// File-backed After Action Report store. Listens for project completion and generates a
    /// reviewable report; accepting rewards her, rejecting reopens the project with feedback.
    /// </summary>
    public sealed class AarService : IAarService, IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _storePath;
        private readonly string _autonomyRoot;
        private readonly IProjectManagementService _projects;
        private readonly IAIService? _aiService;
        private readonly IMemoryService? _memory;
        private readonly DatabasePersistenceService? _database;
        private readonly IPersonaContext? _personaContext;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly List<AfterActionReport> _reports = new();

        public event EventHandler<AarReportsChangedEventArgs>? ReportsChanged;

        public AarService(
            AppConfig appConfig,
            IProjectManagementService projects,
            IAIService? aiService = null,
            IMemoryService? memory = null,
            DatabasePersistenceService? database = null,
            IPersonaContext? personaContext = null)
        {
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
            _aiService = aiService;
            _memory = memory;
            _database = database;
            _personaContext = personaContext;

            var basePath = appConfig.AutonomyDataPath;
            if (!Path.IsPathRooted(basePath))
            {
                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                             ?? AppDomain.CurrentDomain.BaseDirectory;
                basePath = Path.Combine(appDir, basePath);
            }

            Directory.CreateDirectory(basePath);
            _storePath = Path.Combine(basePath, "aar.json");
            _autonomyRoot = basePath;
            LoadFromDisk();

            _projects.MilestoneReached += OnMilestoneReached;
        }

        private void OnMilestoneReached(object? sender, MilestoneReachedEventArgs e)
        {
            if (e.CurrentPhase != ProjectPhase.Completed)
                return;

            // Generate off the event thread; never let a failure bubble into the project service.
            _ = Task.Run(async () =>
            {
                try
                {
                    await GenerateForProjectAsync(e.ProjectId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AAR] generate on completion failed: {ex.Message}");
                }
            });
        }

        public async Task<IReadOnlyList<AfterActionReport>> GetPendingReportsAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _reports
                    .Where(r => r.Status == AarStatus.Pending)
                    .OrderByDescending(r => r.CompletedAt)
                    .Select(Clone)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<AfterActionReport?> GetReportAsync(string reportId)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var report = _reports.FirstOrDefault(r => r.Id == reportId);
                return report == null ? null : Clone(report);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<AfterActionReport?> GenerateForProjectAsync(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return null;

            // Don't duplicate a still-pending report for the same project.
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var existing = _reports.FirstOrDefault(r => r.ProjectId == projectId && r.Status == AarStatus.Pending);
                if (existing != null)
                    return Clone(existing);
            }
            finally
            {
                _lock.Release();
            }

            var project = await _projects.GetProjectAsync(projectId).ConfigureAwait(false);
            if (project == null)
                return null;

            var logs = await _projects.GetProjectLogsAsync(projectId).ConfigureAwait(false);
            var contact = await ResolveContactAsync(project.AssignedAIId).ConfigureAwait(false);
            var artifacts = (await ProjectDeliverableMaterializer.EnsureCompletionBundleAsync(
                _autonomyRoot,
                _projects,
                project,
                logs,
                contact?.Id ?? project.AssignedAIId ?? "system").ConfigureAwait(false)).ToList();

            var completedAt = project.LastModifiedAt ?? DateTime.Now;
            var timeInvested = completedAt - project.StartDate;
            if (timeInvested < TimeSpan.Zero)
                timeInvested = TimeSpan.Zero;

            var completionLevel = DetermineCompletionLevel(project.CompletionPercentage, completedAt, project.Deadline);

            var (summary, goal, outcome) = await GenerateNarrativeAsync(project, logs, completionLevel, contact).ConfigureAwait(false);

            var fileArtifacts = artifacts
                .Where(a => !string.IsNullOrWhiteSpace(a.FilePath))
                .OrderByDescending(a => a.CreatedAt)
                .ToList();
            var deliverable = fileArtifacts.FirstOrDefault(a => File.Exists(a.FilePath));

            var report = new AfterActionReport
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectType = project.Type,
                Summary = summary,
                Goal = goal,
                Outcome = outcome,
                CompletionLevel = completionLevel,
                CompletionPercentage = project.CompletionPercentage,
                StartDate = project.StartDate,
                Deadline = project.Deadline,
                CompletedAt = completedAt,
                TimeInvested = timeInvested,
                WorkSessionCount = logs.Count,
                IsDeliverable = deliverable != null && File.Exists(deliverable.FilePath),
                DeliverableName = deliverable?.Name,
                DeliverablePath = deliverable?.FilePath,
                DeliverablePaths = fileArtifacts
                    .Select(a => a.FilePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                WorkExcerpt = ProjectDeliverableMaterializer.PickWorkExcerpt(logs),
                ContactId = contact?.Id ?? project.AssignedAIId,
                Status = AarStatus.Pending
            };

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _reports.Add(report);
                await PersistAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

            RaiseChanged(report);
            return Clone(report);
        }

        public async Task RefreshPendingDeliverablesAsync()
        {
            List<AfterActionReport> pending;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                pending = _reports
                    .Where(r => r.Status == AarStatus.Pending)
                    .Select(Clone)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }

            if (pending.Count == 0)
                return;

            var changed = false;
            foreach (var report in pending)
            {
                var project = await _projects.GetProjectAsync(report.ProjectId).ConfigureAwait(false);
                if (project == null)
                    continue;

                var logs = await _projects.GetProjectLogsAsync(report.ProjectId).ConfigureAwait(false);
                var contact = await ResolveContactAsync(report.ContactId ?? project.AssignedAIId).ConfigureAwait(false);
                var artifacts = await ProjectDeliverableMaterializer.EnsureCompletionBundleAsync(
                    _autonomyRoot,
                    _projects,
                    project,
                    logs,
                    contact?.Id ?? report.ContactId ?? "system").ConfigureAwait(false);

                var fileArtifacts = artifacts
                    .Where(a => !string.IsNullOrWhiteSpace(a.FilePath) && File.Exists(a.FilePath))
                    .OrderByDescending(a => a.CreatedAt)
                    .ToList();

                var primary = fileArtifacts.FirstOrDefault();
                report.IsDeliverable = primary != null;
                report.DeliverableName = primary?.Name;
                report.DeliverablePath = primary?.FilePath;
                report.DeliverablePaths = fileArtifacts.Select(a => a.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                report.WorkExcerpt = ProjectDeliverableMaterializer.PickWorkExcerpt(logs);
                changed = true;

                await _lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var stored = _reports.FirstOrDefault(r => r.Id == report.Id);
                    if (stored == null || stored.Status != AarStatus.Pending)
                        continue;

                    stored.IsDeliverable = report.IsDeliverable;
                    stored.DeliverableName = report.DeliverableName;
                    stored.DeliverablePath = report.DeliverablePath;
                    stored.DeliverablePaths = report.DeliverablePaths;
                    stored.WorkExcerpt = report.WorkExcerpt;
                }
                finally
                {
                    _lock.Release();
                }
            }

            if (changed)
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await PersistAsync().ConfigureAwait(false);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        public async Task AcceptAsync(string reportId)
        {
            AfterActionReport? report;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                report = _reports.FirstOrDefault(r => r.Id == reportId);
                if (report == null || report.Status != AarStatus.Pending)
                    return;

                report.Status = AarStatus.Accepted;
                report.ReviewedAt = DateTime.Now;
                await PersistAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

            await RewardAsync(report).ConfigureAwait(false);
            RaiseChanged(report);
        }

        public async Task RejectAsync(string reportId, AarRejectionFeedback feedback)
        {
            feedback ??= new AarRejectionFeedback();

            AfterActionReport? report;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                report = _reports.FirstOrDefault(r => r.Id == reportId);
                if (report == null || report.Status != AarStatus.Pending)
                    return;

                report.Status = AarStatus.Rejected;
                report.ReviewedAt = DateTime.Now;
                report.RejectionReason = feedback.Reason;
                report.ImprovementSuggestions = feedback.Suggestions;
                await PersistAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

            await ReopenProjectAsync(report, feedback).ConfigureAwait(false);
            RaiseChanged(report);
        }

        private async Task RewardAsync(AfterActionReport report)
        {
            try
            {
                var contact = await ResolveContactAsync(report.ContactId).ConfigureAwait(false);
                var praise = await GeneratePraiseAsync(report, contact).ConfigureAwait(false);

                if (_memory != null && !string.IsNullOrWhiteSpace(report.ContactId))
                {
                    var rewardMemory =
                        $"[Reward] The user reviewed your completed project \"{report.ProjectName}\" and ACCEPTED it. " +
                        $"They were pleased: {praise} " +
                        "This was a genuine success — the effort and follow-through were valued and are worth repeating.";
                    await _memory.AddMemoryAsync(report.ContactId, rewardMemory).ConfigureAwait(false);
                }

                await _projects.AddLogEntryAsync(report.ProjectId, new ProjectLog
                {
                    ProjectId = report.ProjectId,
                    PerformedBy = "User",
                    Action = "AAR: accepted",
                    Details = praise
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AAR] reward failed: {ex.Message}");
            }
        }

        private async Task ReopenProjectAsync(AfterActionReport report, AarRejectionFeedback feedback)
        {
            try
            {
                var project = await _projects.GetProjectAsync(report.ProjectId).ConfigureAwait(false);
                if (project != null)
                {
                    project.Phase = ProjectPhase.Development;
                    project.CompletionPercentage = 50;
                    project.Priority = Math.Clamp(feedback.NewPriority, 1, 10);
                    project.StartDate = feedback.NewStartDate;
                    project.Deadline = feedback.NewDeadline;
                    project.LastModifiedAt = DateTime.Now;

                    if (!string.IsNullOrWhiteSpace(feedback.Reason))
                        project.Roadblocks.Add(feedback.Reason.Trim());

                    await _projects.UpdateProjectAsync(project).ConfigureAwait(false);

                    await _projects.AddLogEntryAsync(project.Id, new ProjectLog
                    {
                        ProjectId = project.Id,
                        PerformedBy = "User",
                        Action = "AAR: rejected — revisions requested",
                        Details = $"Reason: {feedback.Reason}\nSuggestions: {feedback.Suggestions}"
                    }).ConfigureAwait(false);
                }

                if (_memory != null && !string.IsNullOrWhiteSpace(report.ContactId))
                {
                    var reviseMemory =
                        $"[Project revision needed] The user reviewed your completed project \"{report.ProjectName}\" and asked for changes before accepting it. " +
                        $"What was wrong: {feedback.Reason} " +
                        $"How to improve it: {feedback.Suggestions} " +
                        "Reopen this work with these points in mind and address them directly.";
                    await _memory.AddMemoryAsync(report.ContactId, reviseMemory).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AAR] reopen project failed: {ex.Message}");
            }
        }

        private async Task<(string summary, string goal, string outcome)> GenerateNarrativeAsync(
            Project project,
            List<ProjectLog> logs,
            AarCompletionLevel level,
            AIContact? contact)
        {
            var fallbackSummary = string.IsNullOrWhiteSpace(project.Description)
                ? $"Work on {project.Name}."
                : project.Description.Trim();
            var fallbackGoal = $"Bring \"{project.Name}\" to completion.";
            var fallbackOutcome = level switch
            {
                AarCompletionLevel.Exceeded => "Completed ahead of schedule, beyond the original target.",
                AarCompletionLevel.Partial => "Reached a stopping point short of the full goal.",
                _ => "Completed as intended."
            };

            if (_aiService == null || contact == null)
                return (fallbackSummary, fallbackGoal, fallbackOutcome);

            try
            {
                var recentLogs = string.Join("\n", logs
                    .OrderByDescending(l => l.Timestamp)
                    .Take(8)
                    .Select(l => $"- {l.Action}: {Truncate(l.Details ?? string.Empty, 200)}"));

                var prompt = $$"""
                    You are {{contact.Name}}. You just finished the project "{{project.Name}}" (type: {{project.Type}}).
                    Project description: {{project.Description}}
                    Completion: {{project.CompletionPercentage:F0}}% — assessed as {{level}}.
                    Recent work log:
                    {{recentLogs}}

                    Write an After Action Report. Reply with ONLY a JSON object (no markdown):
                    {"summary":"2-3 sentences on what this project was","goal":"1 sentence on what the goal was","outcome":"2-3 sentences on what you actually achieved and any caveats"}
                    """;

                var raw = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
                var json = raw?.Trim() ?? string.Empty;
                var match = Regex.Match(json, @"\{[\s\S]*\}");
                if (match.Success)
                    json = match.Value;

                var parsed = JsonSerializer.Deserialize<NarrativePayload>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    return (
                        string.IsNullOrWhiteSpace(parsed.Summary) ? fallbackSummary : parsed.Summary.Trim(),
                        string.IsNullOrWhiteSpace(parsed.Goal) ? fallbackGoal : parsed.Goal.Trim(),
                        string.IsNullOrWhiteSpace(parsed.Outcome) ? fallbackOutcome : parsed.Outcome.Trim());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AAR] narrative generation failed: {ex.Message}");
            }

            return (fallbackSummary, fallbackGoal, fallbackOutcome);
        }

        private async Task<string> GeneratePraiseAsync(AfterActionReport report, AIContact? contact)
        {
            var fallback = $"Well done finishing \"{report.ProjectName}\".";
            if (_aiService == null || contact == null)
                return fallback;

            try
            {
                var prompt =
                    $"The user just accepted and praised your completed project \"{report.ProjectName}\". " +
                    "In ONE warm, genuine sentence, express how that recognition feels to you. No preamble.";
                var raw = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
            }
            catch
            {
                return fallback;
            }
        }

        private async Task<AIContact?> ResolveContactAsync(string? preferredId)
        {
            if (_personaContext != null)
                return await _personaContext.ResolveAsync(preferredId).ConfigureAwait(false);

            if (_database == null)
                return null;

            try
            {
                var contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
                if (contacts.Count == 0)
                    return null;

                if (!string.IsNullOrWhiteSpace(preferredId))
                {
                    var byId = contacts.Values.FirstOrDefault(c => string.Equals(c.Id, preferredId, StringComparison.Ordinal));
                    if (byId != null)
                        return byId;
                }

                return contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static AarCompletionLevel DetermineCompletionLevel(double completion, DateTime completedAt, DateTime deadline)
        {
            if (completion < 100)
                return AarCompletionLevel.Partial;
            // Finished and beat the deadline comfortably → exceeded expectations.
            if (completedAt < deadline)
                return AarCompletionLevel.Exceeded;
            return AarCompletionLevel.Full;
        }

        private void LoadFromDisk()
        {
            if (!File.Exists(_storePath))
                return;

            try
            {
                var json = File.ReadAllText(_storePath);
                var bundle = JsonSerializer.Deserialize<AarStoreBundle>(json, JsonOptions);
                if (bundle?.Reports != null)
                    _reports.AddRange(bundle.Reports);
            }
            catch
            {
                // start fresh on corruption
            }
        }

        private async Task PersistAsync()
        {
            var bundle = new AarStoreBundle { Reports = _reports };
            var json = JsonSerializer.Serialize(bundle, JsonOptions);
            await File.WriteAllTextAsync(_storePath, json).ConfigureAwait(false);
        }

        private void RaiseChanged(AfterActionReport report) =>
            ReportsChanged?.Invoke(this, new AarReportsChangedEventArgs { Report = Clone(report) });

        private static AfterActionReport Clone(AfterActionReport source) =>
            JsonSerializer.Deserialize<AfterActionReport>(
                JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? new AfterActionReport();

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

        public void Dispose()
        {
            _projects.MilestoneReached -= OnMilestoneReached;
        }

        private sealed class NarrativePayload
        {
            public string? Summary { get; set; }
            public string? Goal { get; set; }
            public string? Outcome { get; set; }
        }

        private sealed class AarStoreBundle
        {
            public List<AfterActionReport> Reports { get; set; } = new();
        }
    }
}
