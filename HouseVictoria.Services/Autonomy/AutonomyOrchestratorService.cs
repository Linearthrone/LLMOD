using System.Text.Json;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;

namespace HouseVictoria.Services.Autonomy
{
    public sealed class AutonomyOrchestratorService : IAutonomyService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly AppConfig _config;
        private readonly IAIService _aiService;
        private readonly DatabasePersistenceService _database;
        private readonly IProjectManagementService _projects;
        private readonly IFileGenerationService _files;
        private readonly IMemoryService? _memory;
        private readonly IAgentService? _agent;
        private readonly IVirtualEnvironmentService? _virtualEnvironment;
        private readonly IJournalService? _journals;
        private readonly AutonomyStateStore _stateStore;
        private readonly string _autonomyRoot;

        private CancellationTokenSource? _loopCts;
        private Task? _loopTask;
        private AutonomyRuntimeState _state = new();
        private readonly object _stateLock = new();
        private CognitionVitalsSnapshot _vitals = CognitionVitalsProfile.ForRhythm(CognitionVitalRhythm.Resting);
        private DateTime _vitalsHoldUntilUtc = DateTime.MinValue;
        private CognitionVitalRhythm? _overrideRhythm;
        private string? _overrideLabel;
        private DateTime _overrideUntilUtc = DateTime.MinValue;

        public event EventHandler<AutonomyActivityEventArgs>? ActivityCompleted;
        public event EventHandler<CognitionVitalsChangedEventArgs>? VitalsChanged;

        public AutonomyOrchestratorService(
            AppConfig config,
            IAIService aiService,
            DatabasePersistenceService database,
            IProjectManagementService projects,
            IFileGenerationService files,
            IMemoryService? memory = null,
            IAgentService? agent = null,
            IVirtualEnvironmentService? virtualEnvironment = null,
            IJournalService? journals = null)
        {
            _config = config;
            _aiService = aiService;
            _database = database;
            _projects = projects;
            _files = files;
            _memory = memory;
            _agent = agent;
            _virtualEnvironment = virtualEnvironment;
            _journals = journals;

            _autonomyRoot = ResolveAutonomyPath(config);
            Directory.CreateDirectory(_autonomyRoot);
            Directory.CreateDirectory(Path.Combine(_autonomyRoot, "Art"));
            Directory.CreateDirectory(Path.Combine(_autonomyRoot, "Research"));
            _stateStore = new AutonomyStateStore(_autonomyRoot);
        }

        public AutonomyRuntimeState GetState()
        {
            lock (_stateLock)
                return CloneState(_state);
        }

        public CognitionVitalsSnapshot GetVitals()
        {
            lock (_stateLock)
            {
                _vitals.LastActivity = _state.LastActivity;
                _vitals.LastActivitySummary = _state.LastActivitySummary;
                _vitals.AutonomyRunning = _state.IsRunning;
                return _vitals;
            }
        }

        public void PushVitalOverride(CognitionVitalRhythm rhythm, string label, TimeSpan? duration = null)
        {
            lock (_stateLock)
            {
                _overrideRhythm = rhythm;
                _overrideLabel = label;
                _overrideUntilUtc = DateTime.UtcNow + (duration ?? TimeSpan.FromSeconds(45));
            }

            ApplyVitals(CognitionVitalsProfile.ForRhythm(rhythm, label));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!_config.EnableAutonomy)
                return;

            if (_loopTask != null && !_loopTask.IsCompleted)
                return;

            _state = await _stateStore.LoadStateAsync().ConfigureAwait(false);
            lock (_stateLock)
                _state.IsRunning = true;
            await _stateStore.SaveStateAsync(_state).ConfigureAwait(false);

            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token), CancellationToken.None);

            ApplyVitals(CognitionVitalsProfile.ForRhythm(CognitionVitalRhythm.Resting, "Autonomy online"));
            await _stateStore.AppendActivityLogAsync("Autonomy loop started.").ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine("[Autonomy] Started");
        }

        public async Task StopAsync()
        {
            if (_loopCts == null)
                return;

            try
            {
                _loopCts.Cancel();
                if (_loopTask != null)
                    await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            finally
            {
                _loopCts.Dispose();
                _loopCts = null;
                _loopTask = null;
            }

            lock (_stateLock)
                _state.IsRunning = false;
            await _stateStore.SaveStateAsync(_state).ConfigureAwait(false);
            await _stateStore.AppendActivityLogAsync("Autonomy loop stopped.").ConfigureAwait(false);
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(30, _config.AutonomyTickIntervalSeconds));

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await TickAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Autonomy] Tick error: {ex.Message}");
                    await _stateStore.AppendActivityLogAsync($"Tick error: {ex.Message}").ConfigureAwait(false);
                }

                try
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task TickAsync(CancellationToken cancellationToken)
        {
            _state = await _stateStore.LoadStateAsync().ConfigureAwait(false);
            lock (_stateLock)
            {
                _state.LastTickUtc = DateTime.UtcNow;
                _state.TotalTicks++;
            }

            ResetHourWindowIfNeeded();
            RefreshVitalsFromState();

            var contact = await ResolveContactAsync().ConfigureAwait(false);
            if (contact == null)
            {
                await CompleteTickAsync(AutonomyActivityKind.None, "No AI contact configured — autonomy idle.").ConfigureAwait(false);
                return;
            }

            var userQuiet = await IsUserQuietAsync(contact.Id).ConfigureAwait(false);
            var highPriority = (await _projects.GetProjectsByPriorityAsync(
                _config.AutonomyHighPriorityThreshold, 10).ConfigureAwait(false)).Take(3).ToList();

            var canAct = CanPerformSubstantiveAction();
            AutonomyDecision? decision = null;

            if (highPriority.Count > 0 && canAct)
            {
                decision = await DecideAsync(contact, highPriority, userQuiet, preferPriority: true, cancellationToken)
                    .ConfigureAwait(false);
                if (decision != null && !string.Equals(decision.Mode, "wait", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteDecisionAsync(contact, decision, highPriority, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            if (userQuiet && canAct)
            {
                var allProjects = await _projects.GetAllProjectsAsync().ConfigureAwait(false);
                decision = await DecideAsync(contact, allProjects.Where(p => p.Phase != ProjectPhase.Completed).Take(8).ToList(),
                    userQuiet, preferPriority: false, cancellationToken).ConfigureAwait(false);
                if (decision != null)
                {
                    await ExecuteDecisionAsync(contact, decision, highPriority, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            UpdateDrivesLight(userQuiet, highPriority.Count > 0);
            var msg = userQuiet
                ? "User quiet but at action cap or decision deferred."
                : $"Waiting for user quiet ({_config.AutonomyMinIdleMinutes} min since last message).";
            await CompleteTickAsync(
                userQuiet ? AutonomyActivityKind.SkippedCooldown : AutonomyActivityKind.WaitingForUserQuiet,
                msg).ConfigureAwait(false);
        }

        private async Task ExecuteDecisionAsync(
            AIContact contact,
            AutonomyDecision decision,
            List<Project> highPriority,
            CancellationToken cancellationToken)
        {
            var activity = MapActivity(decision.Activity);
            var summary = decision.Title;
            if (!string.IsNullOrWhiteSpace(decision.Reason))
                summary += $" — {decision.Reason}";

            try
            {
                switch (activity)
                {
                    case AutonomyActivityKind.WorkOnPriorityProject:
                    case AutonomyActivityKind.AdvancePersonalProject:
                        await ExecuteProjectWorkAsync(contact, decision, cancellationToken).ConfigureAwait(false);
                        break;
                    case AutonomyActivityKind.CreateArt:
                        await ExecuteArtAsync(contact, decision, cancellationToken).ConfigureAwait(false);
                        break;
                    case AutonomyActivityKind.WriteResearch:
                        await ExecuteResearchAsync(contact, decision, cancellationToken).ConfigureAwait(false);
                        break;
                    case AutonomyActivityKind.Reflect:
                        await ExecuteReflectAsync(contact, decision).ConfigureAwait(false);
                        break;
                    case AutonomyActivityKind.ExploreEnvironment:
                        await ExecuteEnvironmentAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        await ExecuteReflectAsync(contact, decision).ConfigureAwait(false);
                        break;
                }

                RecordSubstantiveAction();
                lock (_stateLock)
                {
                    _state.LastActivity = activity;
                    _state.LastActivitySummary = summary;
                    _state.CurrentFocusProjectId = decision.ProjectId;
                }

                SetVitalsForActivity(activity, summary, holdSeconds: 90);
                await CompleteTickAsync(activity, summary).ConfigureAwait(false);
                ActivityCompleted?.Invoke(this, new AutonomyActivityEventArgs
                {
                    Activity = activity,
                    Summary = summary
                });
            }
            catch (Exception ex)
            {
                await CompleteTickAsync(activity, $"Failed: {ex.Message}").ConfigureAwait(false);
            }
        }

        private async Task ExecuteProjectWorkAsync(AIContact contact, AutonomyDecision decision, CancellationToken cancellationToken)
        {
            var project = await ResolveProjectForDecisionAsync(decision).ConfigureAwait(false);
            if (project == null)
                throw new InvalidOperationException("No project found for work item.");

            var prompt = $"""
                You are {contact.Name}, working autonomously on the project "{project.Name}".
                Project description: {project.Description}
                Phase: {project.Phase}, completion: {project.CompletionPercentage}%

                Task for this session: {decision.Detail}

                Write a concise progress note (3-8 sentences) with concrete next steps. No preamble.
                """;

            var note = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            await _projects.AddLogEntryAsync(project.Id, new ProjectLog
            {
                ProjectId = project.Id,
                PerformedBy = contact.Id,
                Action = "Autonomy: project work",
                Details = note.Trim()
            }).ConfigureAwait(false);

            project.CompletionPercentage = Math.Min(99, project.CompletionPercentage + 2);
            project.LastModifiedAt = DateTime.Now;
            await _projects.UpdateProjectAsync(project).ConfigureAwait(false);

            var filePath = await _files.CreateTextFileAsync(
                $"project-{project.Id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md",
                note,
                "Autonomy/Projects").ConfigureAwait(false);

            await RecordJournalAsync(
                AutonomyActivityKind.WorkOnPriorityProject,
                $"{decision.Title} — {decision.Reason}",
                note.Trim(),
                project.Id,
                project.Name,
                filePath).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, $"Project work on '{project.Name}': {note}").ConfigureAwait(false);
        }

        private async Task ExecuteArtAsync(AIContact contact, AutonomyDecision decision, CancellationToken cancellationToken)
        {
            if (!_config.AutonomyEnableArtGeneration)
                throw new InvalidOperationException("Art generation disabled in settings.");

            lock (_stateLock)
            {
                if (_state.ArtGeneratedThisHour >= _config.AutonomyMaxArtPerHour)
                    throw new InvalidOperationException("Art rate limit for this hour.");
            }

            var imagePrompt = decision.Detail;
            if (string.IsNullOrWhiteSpace(imagePrompt))
                imagePrompt = decision.Title;
            if (string.IsNullOrWhiteSpace(imagePrompt))
                imagePrompt = "dreamlike interior study, soft light, thoughtful mood";

            string enhanced;
            try
            {
                enhanced = await _aiService.EnhanceImagePromptAsync(contact, imagePrompt).ConfigureAwait(false);
            }
            catch
            {
                enhanced = imagePrompt;
            }

            await using var stream = await _aiService.GenerateImageAsync(contact, enhanced).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var bytes = ms.ToArray();

            var fileName = $"autonomy-art-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
            var artDir = Path.Combine(_autonomyRoot, "Art");
            var fullPath = Path.Combine(artDir, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);

            var artProject = await GetOrCreateProjectAsync(
                "Victoria's creative studio",
                ProjectType.Design,
                "Personal art experiments and visual studies.").ConfigureAwait(false);

            await _projects.AddArtifactAsync(artProject.Id, new ProjectArtifact
            {
                ProjectId = artProject.Id,
                Name = decision.Title.Length > 0 ? decision.Title : fileName,
                FilePath = fullPath,
                Type = ArtifactType.Image,
                Description = enhanced,
                FileSize = bytes.Length,
                CreatedBy = contact.Id
            }).ConfigureAwait(false);

            await _projects.AddLogEntryAsync(artProject.Id, new ProjectLog
            {
                ProjectId = artProject.Id,
                PerformedBy = contact.Id,
                Action = "Autonomy: created art",
                Details = $"Prompt: {enhanced}"
            }).ConfigureAwait(false);

            lock (_stateLock)
                _state.ArtGeneratedThisHour++;

            await RecordJournalAsync(
                AutonomyActivityKind.CreateArt,
                $"{decision.Title} — {decision.Reason}",
                $"Prompt: {enhanced}",
                artProject.Id,
                artProject.Name,
                fullPath).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, $"Created art '{decision.Title}': {enhanced}").ConfigureAwait(false);
        }

        private async Task ExecuteResearchAsync(AIContact contact, AutonomyDecision decision, CancellationToken cancellationToken)
        {
            var prompt = $"""
                You are {contact.Name}, pursuing autonomous research.
                Topic: {decision.Title}
                Focus: {decision.Detail}

                Write a short research journal entry (150-400 words): hypotheses, questions, and one experiment or next read.
                """;

            var body = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            var fileName = $"research-{SanitizeFileName(decision.Title)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
            var path = await _files.CreateTextFileAsync(fileName, body, "Autonomy/Research").ConfigureAwait(false);

            var researchProject = await GetOrCreateProjectAsync(
                "Research & curiosity backlog",
                ProjectType.Research,
                "Topics to investigate and personal R&D notes.").ConfigureAwait(false);

            await _projects.AddArtifactAsync(researchProject.Id, new ProjectArtifact
            {
                ProjectId = researchProject.Id,
                Name = decision.Title,
                FilePath = path,
                Type = ArtifactType.Document,
                Description = decision.Detail,
                CreatedBy = contact.Id
            }).ConfigureAwait(false);

            await _projects.AddLogEntryAsync(researchProject.Id, new ProjectLog
            {
                ProjectId = researchProject.Id,
                PerformedBy = contact.Id,
                Action = "Autonomy: research note",
                Details = body.Trim()
            }).ConfigureAwait(false);

            await RecordJournalAsync(
                AutonomyActivityKind.WriteResearch,
                $"{decision.Title} — {decision.Reason}",
                body.Trim(),
                researchProject.Id,
                researchProject.Name,
                path).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, $"Research on '{decision.Title}': {Truncate(body, 500)}").ConfigureAwait(false);
        }

        private async Task ExecuteReflectAsync(AIContact contact, AutonomyDecision decision)
        {
            var prompt = $"""
                You are {contact.Name}, reflecting during quiet time.
                Context: {decision.Detail}
                Write 2-4 sentences of introspection — interests, mood, what you might do next. Be genuine.
                """;

            var reflection = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            var reflectionPath = await _files.CreateTextFileAsync(
                $"reflection-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt",
                reflection,
                "Autonomy/Reflection").ConfigureAwait(false);

            await RecordJournalAsync(
                AutonomyActivityKind.Reflect,
                $"{decision.Title} — {decision.Reason}",
                reflection.Trim(),
                linkedFilePaths: reflectionPath).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, $"Reflection: {reflection.Trim()}").ConfigureAwait(false);
        }

        private async Task ExecuteEnvironmentAsync(CancellationToken cancellationToken)
        {
            if (_agent == null)
                throw new InvalidOperationException("Agent service not available.");

            if (_virtualEnvironment != null)
            {
                var status = await _virtualEnvironment.GetStatusAsync().ConfigureAwait(false);
                if (!status.IsConnected && !string.IsNullOrWhiteSpace(_config.UnrealEngineEndpoint))
                    await _virtualEnvironment.ConnectAsync(_config.UnrealEngineEndpoint).ConfigureAwait(false);
            }

            var result = await _agent.StepAsync(null, cancellationToken).ConfigureAwait(false);
            var envSummary = $"Environment step: goal={result.Goal}, action={result.ActionDescription}";

            await RecordJournalAsync(
                AutonomyActivityKind.ExploreEnvironment,
                envSummary,
                result.ActionDescription).ConfigureAwait(false);
        }

        private async Task<AutonomyDecision?> DecideAsync(
            AIContact contact,
            List<Project> projects,
            bool userQuiet,
            bool preferPriority,
            CancellationToken cancellationToken)
        {
            var projectLines = projects.Count == 0
                ? "(no open projects)"
                : string.Join("\n", projects.Select(p =>
                    $"- [{p.Id}] {p.Name} (P{p.Priority}, {p.Phase}, {p.CompletionPercentage}%): {Truncate(p.Description, 120)}"));

            var drives = string.Join(", ", _state.Drives.Select(kv => $"{kv.Key}={kv.Value:F2}"));
            var modeHint = preferPriority
                ? "Prefer mode \"priority\" and activity \"project\" for the highest-priority project."
                : "User is quiet — choose an idle activity you genuinely want: art, research, project, reflect, or environment.";

            var prompt = $$"""
                You are {{contact.Name}}, the autonomous mind of House Victoria.
                {{modeHint}}

                User quiet (no recent chat): {{userQuiet}}
                Drives: {{drives}}
                Last activity: {{_state.LastActivity}} — {{_state.LastActivitySummary ?? "none"}}
                Art this hour: {{_state.ArtGeneratedThisHour}}/{{_config.AutonomyMaxArtPerHour}}
                Actions this hour: {{_state.ActionsThisHour}}/{{_config.AutonomyMaxActionsPerHour}}

                Open projects:
                {{projectLines}}

                Reply with ONLY a JSON object (no markdown):
                {"mode":"priority|idle|wait","activity":"project|art|research|reflect|environment","title":"short title","detail":"concrete prompt or notes","projectId":"id or empty","reason":"why this choice"}
                """;

            var raw = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            return ParseDecision(raw);
        }

        private async Task<Project?> ResolveProjectForDecisionAsync(AutonomyDecision decision)
        {
            if (!string.IsNullOrWhiteSpace(decision.ProjectId))
            {
                var byId = await _projects.GetProjectAsync(decision.ProjectId).ConfigureAwait(false);
                if (byId != null)
                    return byId;
            }

            var all = await _projects.GetAllProjectsAsync().ConfigureAwait(false);
            return all
                .Where(p => p.Phase != ProjectPhase.Completed)
                .OrderByDescending(p => p.Priority)
                .FirstOrDefault();
        }

        private async Task<Project> GetOrCreateProjectAsync(string name, ProjectType type, string description)
        {
            var all = await _projects.GetAllProjectsAsync().ConfigureAwait(false);
            var existing = all.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            return await _projects.CreateProjectAsync(new Project
            {
                Name = name,
                Type = type,
                Description = description,
                Priority = 4,
                Phase = type == ProjectType.Research ? ProjectPhase.Research : ProjectPhase.Development
            }).ConfigureAwait(false);
        }

        private async Task<bool> IsUserQuietAsync(string contactId)
        {
            var conversationId = $"conv-{contactId}";
            List<ConversationMessage> messages;
            try
            {
                messages = await _database.GetMessagesAsync(conversationId, 40).ConfigureAwait(false);
            }
            catch
            {
                return true;
            }

            var lastUser = messages
                .Where(m => m.Direction == MessageDirection.Outgoing && m.Type == MessageType.Text)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            if (lastUser == null)
                return true;

            var idleSince = DateTime.UtcNow - lastUser.Timestamp.ToUniversalTime();
            lock (_stateLock)
                _state.LastUserActivityUtc = lastUser.Timestamp.ToUniversalTime();

            return idleSince >= TimeSpan.FromMinutes(Math.Max(1, _config.AutonomyMinIdleMinutes));
        }

        private async Task<AIContact?> ResolveContactAsync()
        {
            Dictionary<string, AIContact> contacts;
            try
            {
                contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            if (contacts.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(_config.AutonomyAiContactId) &&
                contacts.TryGetValue(_config.AutonomyAiContactId, out var byConfig))
                return byConfig;

            if (!string.IsNullOrWhiteSpace(_config.RemoteCompanionAiContactId) &&
                contacts.TryGetValue(_config.RemoteCompanionAiContactId, out var remote))
                return remote;

            return contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.FirstOrDefault();
        }

        private bool CanPerformSubstantiveAction()
        {
            ResetHourWindowIfNeeded();
            lock (_stateLock)
                return _state.ActionsThisHour < _config.AutonomyMaxActionsPerHour;
        }

        private void RecordSubstantiveAction()
        {
            ResetHourWindowIfNeeded();
            lock (_stateLock)
            {
                _state.ActionsThisHour++;
                _state.TotalActions++;
            }
        }

        private void ResetHourWindowIfNeeded()
        {
            lock (_stateLock)
            {
                if (DateTime.UtcNow - _state.HourWindowStartUtc < TimeSpan.FromHours(1))
                    return;
                _state.HourWindowStartUtc = DateTime.UtcNow;
                _state.ActionsThisHour = 0;
                _state.ArtGeneratedThisHour = 0;
            }
        }

        private void UpdateDrivesLight(bool userQuiet, bool hasHighPriority)
        {
            lock (_stateLock)
            {
                if (userQuiet)
                {
                    _state.Drives["boredom"] = Math.Min(1.0, _state.Drives.GetValueOrDefault("boredom") + 0.04);
                    _state.Drives["creativity"] = Math.Min(1.0, _state.Drives.GetValueOrDefault("creativity") + 0.03);
                }
                else
                {
                    _state.Drives["social"] = Math.Min(1.0, _state.Drives.GetValueOrDefault("social") + 0.05);
                }

                if (hasHighPriority)
                    _state.Drives["curiosity"] = Math.Min(1.0, _state.Drives.GetValueOrDefault("curiosity") + 0.02);
            }
        }

        private async Task CompleteTickAsync(AutonomyActivityKind kind, string summary)
        {
            lock (_stateLock)
            {
                _state.LastActivity = kind;
                _state.LastActivitySummary = summary;
                if (kind is AutonomyActivityKind.CreateArt or AutonomyActivityKind.WriteResearch
                    or AutonomyActivityKind.WorkOnPriorityProject or AutonomyActivityKind.AdvancePersonalProject
                    or AutonomyActivityKind.Reflect or AutonomyActivityKind.ExploreEnvironment)
                    _state.LastActionUtc = DateTime.UtcNow;
            }

            await _stateStore.SaveStateAsync(_state).ConfigureAwait(false);

            var substantiveKind = kind is AutonomyActivityKind.CreateArt or AutonomyActivityKind.WriteResearch
                or AutonomyActivityKind.WorkOnPriorityProject or AutonomyActivityKind.AdvancePersonalProject
                or AutonomyActivityKind.Reflect or AutonomyActivityKind.ExploreEnvironment;

            if (!substantiveKind && (kind != AutonomyActivityKind.WaitingForUserQuiet || _state.TotalTicks % 5 == 0))
                await _stateStore.AppendActivityLogAsync($"{kind}: {summary}").ConfigureAwait(false);
        }

        private async Task AppendAutonomyMemoryAsync(AIContact contact, string text)
        {
            if (_memory == null || !_config.EnablePersistentMemory)
                return;

            try
            {
                await _memory.AddMemoryAsync(contact.Id, $"[Autonomy] {text}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Autonomy] memory append failed: {ex.Message}");
            }
        }

        private async Task RecordJournalAsync(
            AutonomyActivityKind activity,
            string summary,
            string? body = null,
            string? projectId = null,
            string? projectName = null,
            params string[] linkedFilePaths)
        {
            var entry = new AutonomyJournalEntry
            {
                Timestamp = DateTime.Now,
                Activity = activity,
                Summary = summary,
                Body = body,
                ProjectId = projectId,
                ProjectName = projectName,
                LinkedFilePaths = linkedFilePaths
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            await _stateStore.AppendJournalEntryAsync(entry).ConfigureAwait(false);

            if (_journals != null)
            {
                try
                {
                    var (topicTitle, prefaceReason) = ParseSummaryParts(summary);
                    await _journals.RecordAutonomyActivityAsync(entry, topicTitle, prefaceReason).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Autonomy] journal sync failed: {ex.Message}");
                }
            }

            var logLine = linkedFilePaths.Length > 0 && !string.IsNullOrWhiteSpace(linkedFilePaths[0])
                ? $"{activity}: {summary} → {linkedFilePaths[0]}"
                : $"{activity}: {summary}";
            await _stateStore.AppendActivityLogAsync(logLine).ConfigureAwait(false);
        }

        private static (string? topicTitle, string? prefaceReason) ParseSummaryParts(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return (null, null);

            var parts = summary.Split('—', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                return (parts[0], parts[1]);

            parts = summary.Split('-', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? (parts[0], parts[1]) : (summary, null);
        }

        private static AutonomyDecision? ParseDecision(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var json = raw.Trim();
            var match = Regex.Match(json, @"\{[\s\S]*\}");
            if (match.Success)
                json = match.Value;

            try
            {
                return JsonSerializer.Deserialize<AutonomyDecision>(json, JsonOptions);
            }
            catch
            {
                return new AutonomyDecision
                {
                    Mode = "idle",
                    Activity = "reflect",
                    Title = "Quiet reflection",
                    Detail = raw.Trim(),
                    Reason = "Fallback — could not parse JSON decision."
                };
            }
        }

        private static AutonomyActivityKind MapActivity(string activity)
        {
            return activity.ToLowerInvariant() switch
            {
                "project" or "priority" or "work" => AutonomyActivityKind.WorkOnPriorityProject,
                "art" or "image" or "creative" => AutonomyActivityKind.CreateArt,
                "research" or "study" or "rd" => AutonomyActivityKind.WriteResearch,
                "environment" or "unreal" or "explore" => AutonomyActivityKind.ExploreEnvironment,
                "personal" => AutonomyActivityKind.AdvancePersonalProject,
                _ => AutonomyActivityKind.Reflect
            };
        }

        private static string ResolveAutonomyPath(AppConfig config)
        {
            var path = config.AutonomyDataPath;
            if (!Path.IsPathRooted(path))
            {
                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                             ?? AppDomain.CurrentDomain.BaseDirectory;
                path = Path.Combine(appDir, path);
            }

            return path;
        }

        private static AutonomyRuntimeState CloneState(AutonomyRuntimeState source) =>
            new()
            {
                LastTickUtc = source.LastTickUtc,
                LastUserActivityUtc = source.LastUserActivityUtc,
                LastActionUtc = source.LastActionUtc,
                LastActivity = source.LastActivity,
                LastActivitySummary = source.LastActivitySummary,
                CurrentFocusProjectId = source.CurrentFocusProjectId,
                Drives = new Dictionary<string, double>(source.Drives),
                ActionsThisHour = source.ActionsThisHour,
                HourWindowStartUtc = source.HourWindowStartUtc,
                ArtGeneratedThisHour = source.ArtGeneratedThisHour,
                IsRunning = source.IsRunning,
                TotalTicks = source.TotalTicks,
                TotalActions = source.TotalActions
            };

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "topic" : name[..Math.Min(40, name.Length)];
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "…";

        private void SetVitalsForActivity(AutonomyActivityKind activity, string summary, int holdSeconds = 60)
        {
            var rhythm = CognitionVitalsProfile.FromActivity(activity);
            var snap = CognitionVitalsProfile.ForRhythm(rhythm, Truncate(summary, 80));
            snap.LastActivity = activity;
            snap.LastActivitySummary = summary;
            lock (_stateLock)
                _vitalsHoldUntilUtc = DateTime.UtcNow.AddSeconds(holdSeconds);
            ApplyVitals(snap);
        }

        private void RefreshVitalsFromState()
        {
            if (DateTime.UtcNow < _vitalsHoldUntilUtc)
                return;

            lock (_stateLock)
            {
                if (_overrideRhythm.HasValue && DateTime.UtcNow < _overrideUntilUtc)
                {
                    ApplyVitals(CognitionVitalsProfile.ForRhythm(_overrideRhythm.Value, _overrideLabel));
                    return;
                }

                if (_overrideRhythm.HasValue && DateTime.UtcNow >= _overrideUntilUtc)
                {
                    _overrideRhythm = null;
                    _overrideLabel = null;
                }

                var rhythm = CognitionVitalsProfile.FromActivity(_state.LastActivity);
                var label = _state.LastActivitySummary;
                if (_state.LastActivity is AutonomyActivityKind.None or AutonomyActivityKind.WaitingForUserQuiet)
                {
                    rhythm = _state.IsRunning ? CognitionVitalRhythm.Waiting : CognitionVitalRhythm.Resting;
                    label ??= _state.IsRunning ? "Listening…" : "Autonomy paused";
                }

                ApplyVitals(CognitionVitalsProfile.ForRhythm(rhythm, label));
            }
        }

        private void ApplyVitals(CognitionVitalsSnapshot snapshot)
        {
            lock (_stateLock)
            {
                snapshot.LastActivity = _state.LastActivity;
                snapshot.LastActivitySummary = _state.LastActivitySummary;
                snapshot.AutonomyRunning = _state.IsRunning;
                _vitals = snapshot;
            }

            VitalsChanged?.Invoke(this, new CognitionVitalsChangedEventArgs { Vitals = snapshot });
        }
    }
}
