using System.Text.Json;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;
using HouseVictoria.Services.Trading;

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
        private readonly IMemoryService? _memory;
        private readonly IAgentService? _agent;
        private readonly IVirtualEnvironmentService? _virtualEnvironment;
        private readonly IJournalService? _journals;
        private readonly ITradingService? _tradingService;
        private readonly IMarketWatchScanner? _marketWatch;
        private readonly IPersonaContext? _personaContext;
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

        // Anti-fixation: stop her grinding the same project tick after tick.
        private const int MaxConsecutiveSameProject = 3;
        private static readonly TimeSpan ProjectCooldown = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan TopicCooldown = TimeSpan.FromMinutes(30);
        private const string GoalGenCooldownKey = "__goalgen__";
        private const int MaxDecisionFailuresBeforeBackoff = 3;
        private const int LlmCritiqueEveryNthAction = 4;

        // Cognitive helpers (planning, self-goals, outcome feedback).
        private readonly AutonomyPlanner _planner;
        private readonly GoalGenerator _goalGenerator;
        private readonly OutcomeEvaluator _outcomeEvaluator;

        // Set by the Execute* methods so ExecuteDecisionAsync can score the outcome
        // and remember the topic for anti-repetition, without threading it through returns.
        private string? _lastActivityBody;
        private string? _lastActivityTopic;

        public event EventHandler<AutonomyActivityEventArgs>? ActivityCompleted;
        public event EventHandler<CognitionVitalsChangedEventArgs>? VitalsChanged;
        public event EventHandler? AutonomyLevelChanged;

        public AutonomyOrchestratorService(
            AppConfig config,
            IAIService aiService,
            DatabasePersistenceService database,
            IProjectManagementService projects,
            IFileGenerationService files,
            IMemoryService? memory = null,
            IAgentService? agent = null,
            IVirtualEnvironmentService? virtualEnvironment = null,
            IJournalService? journals = null,
            ITradingService? tradingService = null,
            IMarketWatchScanner? marketWatch = null,
            IPersonaContext? personaContext = null)
        {
            _config = config;
            _aiService = aiService;
            _database = database;
            _projects = projects;
            _memory = memory;
            _agent = agent;
            _virtualEnvironment = virtualEnvironment;
            _journals = journals;
            _tradingService = tradingService;
            _marketWatch = marketWatch;
            _personaContext = personaContext;

            _autonomyRoot = ResolveAutonomyPath(config);
            Directory.CreateDirectory(_autonomyRoot);
            Directory.CreateDirectory(Path.Combine(_autonomyRoot, "Art"));
            Directory.CreateDirectory(Path.Combine(_autonomyRoot, "Research"));
            _stateStore = new AutonomyStateStore(_autonomyRoot);

            _planner = new AutonomyPlanner(aiService);
            _goalGenerator = new GoalGenerator(aiService, projects);
            _outcomeEvaluator = new OutcomeEvaluator(aiService);
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
                _vitals.CurrentActivityStartedUtc = _state.CurrentActivityStartedUtc;
                _vitals.PreviousActivitySummary = _state.PreviousActivitySummary;
                _vitals.PreviousActivityStartedUtc = _state.PreviousActivityStartedUtc;
                _vitals.PreviousActivityEndedUtc = _state.PreviousActivityEndedUtc;
                _vitals.AutonomyRunning = _state.IsRunning;
                return _vitals;
            }
        }

        public AutonomyLevel GetAutonomyLevel() => _config.AutonomyLevel;

        public async Task SetAutonomyLevelAsync(AutonomyLevel level)
        {
            var previous = _config.AutonomyLevel;
            if (previous == level)
                return;

            _config.AutonomyLevel = level;
            AutonomyLevelChanged?.Invoke(this, EventArgs.Empty);

            if (level == AutonomyLevel.Off)
            {
                await StopAsync().ConfigureAwait(false);
                ApplyVitals(CognitionVitalsProfile.ForRhythm(CognitionVitalRhythm.Resting, "Autonomy off"));
                return;
            }

            if (previous == AutonomyLevel.Off && AutonomyLevelProfile.IsActive(_config))
                await StartAsync().ConfigureAwait(false);
        }

        public string? GetUserGuidanceSuggestion()
        {
            lock (_stateLock)
                return _state.UserGuidanceSuggestion;
        }

        public void SetUserGuidanceSuggestion(string? suggestion)
        {
            var trimmed = string.IsNullOrWhiteSpace(suggestion) ? null : suggestion.Trim();
            lock (_stateLock)
                _state.UserGuidanceSuggestion = trimmed;
            _ = _stateStore.SaveStateAsync(_state);
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
            if (!AutonomyLevelProfile.IsActive(_config))
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
            if (_marketWatch != null && _config.TradingWatchEnabled)
                _ = _marketWatch.StartAsync(cancellationToken);
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
            var baseInterval = TimeSpan.FromSeconds(Math.Max(30, AutonomyLevelProfile.EffectiveTickIntervalSeconds(_config)));

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

                // Restlessness (boredom / curiosity) shortens the wait so she acts sooner.
                TimeSpan interval;
                lock (_stateLock)
                    interval = DriveSystem.DynamicInterval(baseInterval, _state);

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
            ResetSelfGoalDayIfNeeded();
            RefreshVitalsFromState();

            var contact = await ResolveContactAsync().ConfigureAwait(false);
            if (contact == null)
            {
                await CompleteTickAsync(AutonomyActivityKind.None, "No AI contact configured — autonomy idle.").ConfigureAwait(false);
                return;
            }

            var userQuiet = await IsUserQuietAsync(contact.Id).ConfigureAwait(false);

            // Homeostatic drive maintenance: pull toward baseline, drift boredom while idle.
            lock (_stateLock)
                DriveSystem.Decay(_state, userQuiet, actedThisTick: false);

            var highPriority = (await _projects.GetProjectsByPriorityAsync(
                _config.AutonomyHighPriorityThreshold, 10).ConfigureAwait(false))
                .Where(p => !IsProjectOnCooldown(p.Id))
                .Take(3).ToList();

            var canAct = CanPerformSubstantiveAction();
            var recentFeedback = SnapshotRecentForPrompt();
            bool decisionFailed = false;

            if (userQuiet && canAct && _marketWatch != null && _config.TradingWatchEnabled)
            {
                var pending = _marketWatch.PeekPendingAlerts();
                if (pending.Count > 0)
                {
                    await ExecuteMarketScanAsync(contact, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            if (highPriority.Count > 0 && canAct)
            {
                var decision = await DecideAsync(contact, highPriority, userQuiet, preferPriority: true, recentFeedback, cancellationToken)
                    .ConfigureAwait(false);
                if (decision == null)
                    decisionFailed = true;
                else if (!string.Equals(decision.Mode, "wait", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteDecisionAsync(contact, decision, highPriority, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            // Idle: she may invent a brand-new goal when a drive runs hot and the budget allows.
            if (userQuiet && canAct && ShouldGenerateGoal())
            {
                if (await ExecuteGenerateGoalAsync(contact, cancellationToken).ConfigureAwait(false))
                    return;
            }

            if (userQuiet && canAct)
            {
                var allProjects = await _projects.GetAllProjectsAsync().ConfigureAwait(false);
                var decision = await DecideAsync(contact, allProjects
                    .Where(p => p.Phase != ProjectPhase.Completed && !IsProjectOnCooldown(p.Id))
                    .Take(8).ToList(),
                    userQuiet, preferPriority: false, recentFeedback, cancellationToken).ConfigureAwait(false);
                if (decision == null)
                    decisionFailed = true;
                else if (string.Equals(decision.Mode, "wait", StringComparison.OrdinalIgnoreCase))
                {
                    await CompleteTickAsync(AutonomyActivityKind.SkippedCooldown,
                        $"Chose to wait — {Truncate(decision.Reason, 80)}").ConfigureAwait(false);
                    return;
                }
                else
                {
                    await ExecuteDecisionAsync(contact, decision, highPriority, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }

            if (decisionFailed && RegisterDecisionFailureShouldBackOff())
            {
                await CompleteTickAsync(AutonomyActivityKind.SkippedCooldown,
                    "Backing off after repeated decision failures.").ConfigureAwait(false);
                return;
            }

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

            RecordActivityTransition(activity, summary);
            SetVitalsForActivity(activity, summary, holdSeconds: 45);

            _lastActivityBody = null;
            _lastActivityTopic = decision.Title;

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
                    case AutonomyActivityKind.ExecuteTrade:
                        await ExecuteTradeDecisionAsync(contact, decision, cancellationToken).ConfigureAwait(false);
                        break;
                    case AutonomyActivityKind.RunBacktest:
                        await ExecuteBacktestDecisionAsync(contact, decision, cancellationToken).ConfigureAwait(false);
                        break;
                    case AutonomyActivityKind.ScanMarkets:
                        await ExecuteMarketScanAsync(contact, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        await ExecuteReflectAsync(contact, decision).ConfigureAwait(false);
                        break;
                }

                RecordSubstantiveAction();
                lock (_stateLock)
                {
                    _state.CurrentFocusProjectId = decision.ProjectId;
                    _state.ConsecutiveDecisionFailures = 0;
                    DriveSystem.Satisfy(_state, activity);
                }

                RecordRecentActivity(activity, decision.Title, _lastActivityTopic);
                await ScoreOutcomeAsync(contact, activity, _lastActivityTopic, decision.ProjectId, _lastActivityBody, cancellationToken)
                    .ConfigureAwait(false);

                await CompleteTickAsync(activity, summary, skipTransition: true).ConfigureAwait(false);
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

            RecordProjectFocus(project.Id);

            var priorWork = _journals != null
                ? await _journals.GetPriorWorkContextAsync(project.Name, project.Id).ConfigureAwait(false)
                : null;

            // Multi-tick plan: decompose once, then advance one concrete step per session
            // instead of blindly nudging the completion percentage.
            var plan = await GetOrCreatePlanAsync(contact, project, priorWork, cancellationToken).ConfigureAwait(false);
            var currentStep = plan.NextStep;
            var stepSection = currentStep == null
                ? "\n\nAll planned steps are done — do a final review/polish pass and note anything outstanding."
                : $"""

                Plan progress: step {plan.DoneCount + 1} of {plan.Steps.Count}.
                Focus ONLY on this step right now: {currentStep.Description}
                """;

            var priorSection = string.IsNullOrWhiteSpace(priorWork)
                ? ""
                : $"""

                Prior work on this topic (build on this — do not repeat empty status updates):
                {priorWork}
                """;

            var isTradingProject = ContainsTradingKeywords(project.Name, project.Description, decision.Title, decision.Detail);

            var deliverableRules = isTradingProject
                ? """

                This is a trading/strategy project. Your output MUST include concrete sections:
                - **Strategy Overview**: rules, instruments, timeframes
                - **Setups / Plays**: entry, exit, stop, position sizing
                - **Backtesting / Statistics**: use REAL numbers from the bridge backtest log below when present. To request a specific backtest, include:
                  ```backtest
                  {"strategy_name":"MyPlay","symbol":"EURUSD","time_frame":"H1","start_date":"2025-01-01","end_date":"2026-01-01","strategy_type":"ma_crossover","fast_period":10,"slow_period":30,"stop_loss_pips":20,"take_profit_pips":40}
                  ```
                  strategy_type: ma_crossover | rsi | breakout. If no block, a default MA crossover backtest still runs on available MT4 history.
                - **External Sources**: cite actual documentation, papers, or platforms (MetaTrader docs, academic sources, etc.). Do NOT cite your own prior journal files as primary sources.
                - **Live Execution (optional)**: to request a real demo trade through the MT4 bridge, include a fenced block exactly like:
                  ```trade
                  {"Symbol":"EURUSD","Type":0,"Volume":0.01,"StopLoss":1.0830,"TakeProfit":1.0900}
                  ```
                  Type 0 = buy, 1 = sell. **StopLoss is mandatory** — set it ~20 pips from current bid/ask using live quotes (never copy placeholder prices). Use small volume (0.01). Do NOT claim a trade executed unless this block is present and the bridge confirms it.
                """
                : """

                Deliver substantive content, not a status update. Include concrete findings, specs, or artifacts.
                If building on existing technology or published research, cite external sources (URLs, papers, docs).
                Do NOT cite your own prior journal entries or generated markdown files as primary sources.
                """;

            var prompt = $"""
                You are {contact.Name}, working autonomously on the project "{project.Name}".
                Project description: {project.Description}
                Phase: {project.Phase}, completion: {project.CompletionPercentage}%

                Session intent: {decision.Detail}
                {stepSection}
                {priorSection}
                {deliverableRules}

                Write a detailed markdown deliverable (400-900 words) that completes the step above.
                Never respond with only "I have completed the research" or "I created the strategy" without the actual substance requested.
                """;

            var note = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            _lastActivityBody = note;
            _lastActivityTopic = project.Name;
            await _projects.AddLogEntryAsync(project.Id, new ProjectLog
            {
                ProjectId = project.Id,
                PerformedBy = contact.Id,
                Action = "Autonomy: project work",
                Details = note.Trim()
            }).ConfigureAwait(false);

            // Mark the step done and derive real completion from steps finished — progress
            // now reflects actual work, not a blind increment.
            if (currentStep != null)
            {
                currentStep.Done = true;
                currentStep.CompletedUtc = DateTime.UtcNow;
            }
            lock (_stateLock)
                SavePlan(plan);

            var derivedCompletion = currentStep == null
                ? 100
                : (int)Math.Round(plan.CompletionFraction * 100);
            project.CompletionPercentage = Math.Max(project.CompletionPercentage, derivedCompletion);
            project.LastModifiedAt = DateTime.Now;
            await _projects.UpdateProjectAsync(project).ConfigureAwait(false);

            // Quality gate: the project only finishes when every planned step is done
            // (the milestone event fires here, which triggers the After Action Report).
            if (plan.IsComplete && project.Phase != ProjectPhase.Completed)
            {
                project.CompletionPercentage = 100;
                await _projects.UpdateProjectAsync(project).ConfigureAwait(false);
                await _projects.UpdateProjectPhaseAsync(project.Id, ProjectPhase.Completed).ConfigureAwait(false);
                ClearProjectFocusTracking(project.Id);
            }

            await RecordJournalAsync(
                AutonomyActivityKind.WorkOnPriorityProject,
                $"{decision.Title} — {decision.Reason}",
                note.Trim(),
                project.Id,
                project.Name).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, $"Project work on '{project.Name}': {note}").ConfigureAwait(false);

            if (isTradingProject)
            {
                await AppendTradingBridgeStatusAsync(project, contact).ConfigureAwait(false);
                await RunTradingBacktestsAsync(contact, project, note, cancellationToken).ConfigureAwait(false);
                await TryExecuteTradeBlockAsync(contact, project, note, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ExecuteArtAsync(AIContact contact, AutonomyDecision decision, CancellationToken cancellationToken)
        {
            if (!_config.AutonomyEnableArtGeneration)
                throw new InvalidOperationException("Art generation disabled in settings.");

            lock (_stateLock)
            {
                if (_state.ArtGeneratedThisHour >= AutonomyLevelProfile.EffectiveMaxArtPerHour(_config))
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

            _lastActivityBody = enhanced;
            _lastActivityTopic = decision.Title;
            ApplyTopicCooldown(decision.Title);

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
            var canonicalTopic = decision.Title;
            var priorWork = _journals != null
                ? await _journals.GetPriorWorkContextAsync(canonicalTopic, decision.ProjectId).ConfigureAwait(false)
                : null;

            var priorSection = string.IsNullOrWhiteSpace(priorWork)
                ? ""
                : $"""

                Prior work in this journal thread (continue and extend — do not restart from scratch):
                {priorWork}
                """;

            var isTradingTopic = ContainsTradingKeywords(decision.Title, decision.Detail, null, null);

            var tradingSections = isTradingTopic
                ? """

                Required sections for trading/strategy research:
                - **Strategy Definition**: what the strategy is, instruments, timeframe
                - **Setups / Plays**: specific entry/exit rules
                - **Backtesting & Statistics**: performance metrics, sample period, win rate, drawdown, R-multiple (real data if available; otherwise explicit assumptions + table template)
                """
                : "";

            var prompt = $"""
                You are {contact.Name}, pursuing autonomous research.
                Topic: {decision.Title}
                Focus: {decision.Detail}
                {priorSection}

                Write a substantive research journal entry in markdown (400-800 words).

                Required structure:
                1. **Objective** — what this entry adds beyond prior work
                2. **Findings / Deliverables** — concrete substance (NOT "I completed the research")
                3. **Methodology** — how you investigated
                4. **External Sources** — cite real publications, documentation, technologies (with URLs or formal citations where possible). If building on existing tech or theory, name the actual source. Do NOT cite your own prior journal entries or generated files as primary sources.
                5. **Open Questions** — what remains
                {tradingSections}

                Never submit a hollow completion notice. If data is missing, state what is missing and provide the best partial deliverable you can with explicit gaps.
                """;

            var body = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            _lastActivityBody = body;
            _lastActivityTopic = decision.Title;
            ApplyTopicCooldown(decision.Title);

            var researchProject = await GetOrCreateProjectAsync(
                "Research & curiosity backlog",
                ProjectType.Research,
                "Topics to investigate and personal R&D notes.").ConfigureAwait(false);

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
                researchProject.Name).ConfigureAwait(false);

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
            _lastActivityBody = reflection;
            _lastActivityTopic = null;

            await RecordJournalAsync(
                AutonomyActivityKind.Reflect,
                $"{decision.Title} — {decision.Reason}",
                reflection.Trim()).ConfigureAwait(false);

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
            PromptSignals signals,
            CancellationToken cancellationToken)
        {
            var projectLines = projects.Count == 0
                ? "(no open projects)"
                : string.Join("\n", projects.Select(p =>
                    $"- [{p.Id}] {p.Name} (P{p.Priority}, {p.Phase}, {p.CompletionPercentage}%): {Truncate(p.Description, 120)}"));

            var modeHint = preferPriority
                ? "Prefer mode \"priority\" and activity \"project\" for the highest-priority project."
                : "User is quiet — choose an idle activity you genuinely want: art, research, project, reflect, or environment.";

            string? userGuidance;
            int sameFocusStreak;
            lock (_stateLock)
            {
                userGuidance = _state.UserGuidanceSuggestion;
                sameFocusStreak = _state.SameFocusStreak;
            }

            var guidanceBlock = string.IsNullOrWhiteSpace(userGuidance)
                ? string.Empty
                : $"\nUser guidance (follow this if you feel stuck, looping, or fixated): {userGuidance}\n";

            var fixationHint = sameFocusStreak >= 2
                ? "\nYou have been focused on the same project repeatedly — pick a genuinely different activity or topic.\n"
                : string.Empty;

            var maxArt = AutonomyLevelProfile.EffectiveMaxArtPerHour(_config);
            var maxActions = AutonomyLevelProfile.EffectiveMaxActionsPerHour(_config);

            var prompt = $$"""
                You are {{contact.Name}}, the autonomous mind of House Victoria.
                {{modeHint}}
                {{fixationHint}}{{guidanceBlock}}
                User quiet (no recent chat): {{userQuiet}}
                Drives: {{signals.Drives}}
                Right now these appeal most (drive-weighted): {{signals.DriveHint}}
                Recently worked topics (do NOT repeat or rephrase these): {{signals.RecentTopics}}
                Feedback on your recent work (0-1, higher is better): {{signals.FeedbackHint}}
                Last activity: {{_state.LastActivity}} — {{_state.LastActivitySummary ?? "none"}}
                Art this hour: {{_state.ArtGeneratedThisHour}}/{{maxArt}}
                Actions this hour: {{_state.ActionsThisHour}}/{{maxActions}}

                Open projects:
                {{projectLines}}

                Let your strongest drives and the feedback guide you: lean into what scored well,
                change approach where scores were low, and pick something genuinely different from the recent topics.

                Reply with ONLY a JSON object (no markdown):
                {"mode":"priority|idle|wait","activity":"project|art|research|reflect|environment|trade","title":"short title","detail":"concrete prompt or notes","projectId":"id or empty","reason":"why this choice"}

                For activity "trade", put ONLY a raw JSON trade request in detail (no prose). StopLoss is mandatory — compute it ~20 pips from the current bid/ask (never copy example prices). Only choose "trade" when the MT4 bridge should place a real demo order now.
                For activity "backtest", put a JSON backtest request in detail (same shape as the ```backtest block).
                """;

            string raw;
            try
            {
                raw = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Autonomy] decide error: {ex.Message}");
                return null;
            }

            return ParseDecision(raw);
        }

        private PromptSignals SnapshotRecentForPrompt()
        {
            lock (_stateLock)
            {
                var drives = string.Join(", ", _state.Drives.Select(kv => $"{kv.Key}={kv.Value:F2}"));
                var driveHint = DriveSystem.SuggestionHint(_state);
                var recentTopics = string.Join(", ", _state.RecentActivities
                    .TakeLast(8)
                    .Select(a => string.IsNullOrWhiteSpace(a.Topic) ? a.Title : a.Topic)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct());
                if (string.IsNullOrWhiteSpace(recentTopics))
                    recentTopics = "(none yet)";
                var feedback = OutcomeEvaluator.BuildFeedbackHint(_state.RecentOutcomes);
                return new PromptSignals(drives, driveHint, recentTopics, feedback);
            }
        }

        private sealed record PromptSignals(string Drives, string DriveHint, string RecentTopics, string FeedbackHint);

        private bool IsProjectOnCooldown(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return false;
            lock (_stateLock)
            {
                return _state.ProjectCooldownUntil.TryGetValue(projectId, out var until) && DateTime.UtcNow < until;
            }
        }

        private void RecordProjectFocus(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                return;

            lock (_stateLock)
            {
                if (projectId == _state.LastFocusProjectId)
                    _state.SameFocusStreak++;
                else
                {
                    _state.LastFocusProjectId = projectId;
                    _state.SameFocusStreak = 1;
                }

                // Worked the same project too many ticks in a row → force a break so she
                // rotates to other projects / research / reflection instead of looping.
                if (_state.SameFocusStreak >= MaxConsecutiveSameProject)
                {
                    _state.ProjectCooldownUntil[projectId] = DateTime.UtcNow + ProjectCooldown;
                    _state.SameFocusStreak = 0;
                    _state.LastFocusProjectId = null;
                }
            }
        }

        private void ClearProjectFocusTracking(string projectId)
        {
            lock (_stateLock)
            {
                _state.ProjectCooldownUntil.Remove(projectId);
                if (_state.LastFocusProjectId == projectId)
                {
                    _state.LastFocusProjectId = null;
                    _state.SameFocusStreak = 0;
                }
            }
        }

        private bool IsTopicOnCooldown(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                return false;
            lock (_stateLock)
                return _state.TopicCooldownUntil.TryGetValue(topic, out var until) && DateTime.UtcNow < until;
        }

        private void ApplyTopicCooldown(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                return;
            lock (_stateLock)
            {
                PruneExpiredCooldowns(_state.TopicCooldownUntil);
                _state.TopicCooldownUntil[topic] = DateTime.UtcNow + TopicCooldown;
            }
        }

        private static void PruneExpiredCooldowns(Dictionary<string, DateTime> map)
        {
            var now = DateTime.UtcNow;
            var expired = map.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList();
            foreach (var key in expired)
                map.Remove(key);
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

            return idleSince >= TimeSpan.FromMinutes(Math.Max(1, AutonomyLevelProfile.EffectiveMinIdleMinutes(_config)));
        }

        private async Task<AIContact?> ResolveContactAsync()
        {
            var preferredId = !string.IsNullOrWhiteSpace(_config.AutonomyAiContactId)
                ? _config.AutonomyAiContactId
                : _config.RemoteCompanionAiContactId;

            if (_personaContext != null)
                return await _personaContext.ResolveAsync(preferredId).ConfigureAwait(false);

            // Fallback if the persona context was not supplied.
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

            if (!string.IsNullOrWhiteSpace(preferredId))
            {
                var preferred = contacts.Values.FirstOrDefault(c => string.Equals(c.Id, preferredId, StringComparison.Ordinal));
                if (preferred != null)
                    return preferred;
            }

            return contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.FirstOrDefault();
        }

        private bool CanPerformSubstantiveAction()
        {
            if (_config.AutonomyLevel == AutonomyLevel.Off)
                return false;

            ResetHourWindowIfNeeded();
            var cap = AutonomyLevelProfile.EffectiveMaxActionsPerHour(_config);
            lock (_stateLock)
                return _state.ActionsThisHour < cap;
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

        private void ResetSelfGoalDayIfNeeded()
        {
            lock (_stateLock)
            {
                if (DateTime.UtcNow - _state.SelfGoalDayStartUtc < TimeSpan.FromDays(1))
                    return;
                _state.SelfGoalDayStartUtc = DateTime.UtcNow;
                _state.SelfGoalsToday = 0;
            }
        }

        private bool ShouldGenerateGoal()
        {
            if (!_config.AutonomyEnableSelfGoals)
                return false;
            if (IsTopicOnCooldown(GoalGenCooldownKey))
                return false;

            lock (_stateLock)
            {
                if (_state.SelfGoalsToday >= Math.Max(0, _config.AutonomyMaxSelfGoalsPerDay))
                    return false;
                // Only when a drive is genuinely hot — she wants something, not busywork.
                var dominant = DriveSystem.Dominant(_state);
                return dominant.Value >= 0.75;
            }
        }

        private async Task<bool> ExecuteGenerateGoalAsync(AIContact contact, CancellationToken cancellationToken)
        {
            // Cool down regardless of outcome so a hot drive doesn't trigger this every tick.
            ApplyTopicCooldown(GoalGenCooldownKey);

            var existing = await _projects.GetAllProjectsAsync().ConfigureAwait(false);
            AutonomyRuntimeState snapshot;
            lock (_stateLock)
                snapshot = CloneState(_state);

            var project = await _goalGenerator
                .TryGenerateGoalAsync(contact, snapshot, existing, cancellationToken)
                .ConfigureAwait(false);

            if (project == null)
            {
                // She considered starting something new but nothing compelling surfaced.
                await CompleteTickAsync(AutonomyActivityKind.GenerateGoal,
                    "Considered new directions — nothing worth starting yet.").ConfigureAwait(false);
                return true;
            }

            var summary = $"Started a self-initiated project: {project.Name}";
            RecordActivityTransition(AutonomyActivityKind.GenerateGoal, summary);
            SetVitalsForActivity(AutonomyActivityKind.GenerateGoal, summary, holdSeconds: 45);

            lock (_stateLock)
            {
                _state.SelfGoalsToday++;
                _state.ConsecutiveDecisionFailures = 0;
                DriveSystem.Satisfy(_state, AutonomyActivityKind.GenerateGoal);
            }

            RecordRecentActivity(AutonomyActivityKind.GenerateGoal, project.Name, project.Name);
            RecordSubstantiveAction();

            await RecordJournalAsync(
                AutonomyActivityKind.GenerateGoal,
                $"{project.Name} — self-initiated",
                project.Description,
                project.Id,
                project.Name).ConfigureAwait(false);
            await AppendAutonomyMemoryAsync(contact, $"Decided to start a new project: {project.Name} — {project.Description}").ConfigureAwait(false);

            await CompleteTickAsync(AutonomyActivityKind.GenerateGoal, summary, skipTransition: true).ConfigureAwait(false);
            ActivityCompleted?.Invoke(this, new AutonomyActivityEventArgs
            {
                Activity = AutonomyActivityKind.GenerateGoal,
                Summary = summary
            });
            return true;
        }

        private bool RegisterDecisionFailureShouldBackOff()
        {
            lock (_stateLock)
            {
                _state.ConsecutiveDecisionFailures++;
                return _state.ConsecutiveDecisionFailures >= MaxDecisionFailuresBeforeBackoff;
            }
        }

        private void RecordRecentActivity(AutonomyActivityKind activity, string title, string? topic)
        {
            lock (_stateLock)
            {
                _state.RecentActivities.Add(new AutonomyRecentActivity
                {
                    Activity = activity,
                    Title = title,
                    Topic = topic,
                    TimestampUtc = DateTime.UtcNow
                });
                // Keep only the most recent window.
                const int window = 8;
                if (_state.RecentActivities.Count > window)
                    _state.RecentActivities.RemoveRange(0, _state.RecentActivities.Count - window);
            }
        }

        private async Task ScoreOutcomeAsync(
            AIContact contact,
            AutonomyActivityKind activity,
            string? topic,
            string? projectId,
            string? body,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;

            IReadOnlyList<AutonomyRecentActivity> recent;
            bool useLlmCritique;
            lock (_stateLock)
            {
                recent = _state.RecentActivities.ToList();
                useLlmCritique = LlmCritiqueEveryNthAction > 0
                                 && _state.TotalActions % LlmCritiqueEveryNthAction == 0;
            }

            AutonomyOutcome outcome;
            try
            {
                outcome = await _outcomeEvaluator.EvaluateAsync(
                    contact, activity, topic, projectId, body!, recent, useLlmCritique, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Autonomy] outcome scoring failed: {ex.Message}");
                return;
            }

            lock (_stateLock)
            {
                _state.RecentOutcomes.Add(outcome);
                const int window = 20;
                if (_state.RecentOutcomes.Count > window)
                    _state.RecentOutcomes.RemoveRange(0, _state.RecentOutcomes.Count - window);
            }

            await _stateStore.AppendActivityLogAsync(
                $"Outcome[{activity}] score={outcome.Score:F2} ({outcome.Note})").ConfigureAwait(false);
        }

        private async Task<AutonomyPlan> GetOrCreatePlanAsync(
            AIContact contact,
            Project project,
            string? priorWork,
            CancellationToken cancellationToken)
        {
            lock (_stateLock)
            {
                var existing = _state.Plans.FirstOrDefault(p => p.ProjectId == project.Id);
                if (existing != null && existing.Steps.Count > 0)
                    return existing;
            }

            var plan = await _planner.CreatePlanAsync(contact, project, priorWork, cancellationToken).ConfigureAwait(false);
            lock (_stateLock)
                SavePlan(plan);
            return plan;
        }

        private void SavePlan(AutonomyPlan plan)
        {
            // Caller holds _stateLock (or is single-threaded within the tick).
            _state.Plans.RemoveAll(p => p.ProjectId == plan.ProjectId);
            _state.Plans.Add(plan);
            // Drop plans for projects that no longer matter to keep state bounded.
            const int maxPlans = 30;
            if (_state.Plans.Count > maxPlans)
                _state.Plans.RemoveRange(0, _state.Plans.Count - maxPlans);
        }

        private async Task CompleteTickAsync(AutonomyActivityKind kind, string summary, bool skipTransition = false)
        {
            if (!skipTransition)
                RecordActivityTransition(kind, summary);

            lock (_stateLock)
            {
                if (kind is AutonomyActivityKind.CreateArt or AutonomyActivityKind.WriteResearch
                    or AutonomyActivityKind.WorkOnPriorityProject or AutonomyActivityKind.AdvancePersonalProject
                    or AutonomyActivityKind.Reflect or AutonomyActivityKind.ExploreEnvironment
                    or AutonomyActivityKind.GenerateGoal)
                    _state.LastActionUtc = DateTime.UtcNow;
            }

            await _stateStore.SaveStateAsync(_state).ConfigureAwait(false);

            var substantiveKind = kind is AutonomyActivityKind.CreateArt or AutonomyActivityKind.WriteResearch
                or AutonomyActivityKind.WorkOnPriorityProject or AutonomyActivityKind.AdvancePersonalProject
                or AutonomyActivityKind.Reflect or AutonomyActivityKind.ExploreEnvironment
                or AutonomyActivityKind.GenerateGoal;

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
                var decision = JsonSerializer.Deserialize<AutonomyDecision>(json, JsonOptions);
                if (decision != null && !string.IsNullOrWhiteSpace(decision.Activity))
                    return decision;
                return null;
            }
            catch
            {
                // Null signals a decision failure so the loop can back off instead of
                // spamming fallback reflections every tick.
                return null;
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
                "trade" or "execute_trade" or "order" => AutonomyActivityKind.ExecuteTrade,
                "backtest" or "back_test" => AutonomyActivityKind.RunBacktest,
                "scan_markets" or "market_scan" or "watch_markets" => AutonomyActivityKind.ScanMarkets,
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

        private void RecordActivityTransition(AutonomyActivityKind kind, string summary)
        {
            lock (_stateLock)
            {
                var same = _state.LastActivity == kind
                           && string.Equals(_state.LastActivitySummary, summary, StringComparison.Ordinal);
                if (same)
                    return;

                if (_state.CurrentActivityStartedUtc.HasValue)
                {
                    _state.PreviousActivity = _state.LastActivity;
                    _state.PreviousActivitySummary = _state.LastActivitySummary;
                    _state.PreviousActivityStartedUtc = _state.CurrentActivityStartedUtc;
                    _state.PreviousActivityEndedUtc = DateTime.UtcNow;
                }

                _state.LastActivity = kind;
                _state.LastActivitySummary = summary;
                _state.CurrentActivityStartedUtc = DateTime.UtcNow;
            }
        }

        private static AutonomyRuntimeState CloneState(AutonomyRuntimeState source) =>
            new()
            {
                LastTickUtc = source.LastTickUtc,
                LastUserActivityUtc = source.LastUserActivityUtc,
                LastActionUtc = source.LastActionUtc,
                LastActivity = source.LastActivity,
                LastActivitySummary = source.LastActivitySummary,
                CurrentActivityStartedUtc = source.CurrentActivityStartedUtc,
                PreviousActivity = source.PreviousActivity,
                PreviousActivitySummary = source.PreviousActivitySummary,
                PreviousActivityStartedUtc = source.PreviousActivityStartedUtc,
                PreviousActivityEndedUtc = source.PreviousActivityEndedUtc,
                CurrentFocusProjectId = source.CurrentFocusProjectId,
                Drives = new Dictionary<string, double>(source.Drives),
                DriveBaselines = new Dictionary<string, double>(source.DriveBaselines),
                ActionsThisHour = source.ActionsThisHour,
                HourWindowStartUtc = source.HourWindowStartUtc,
                ArtGeneratedThisHour = source.ArtGeneratedThisHour,
                IsRunning = source.IsRunning,
                TotalTicks = source.TotalTicks,
                TotalActions = source.TotalActions,
                RecentActivities = source.RecentActivities.Select(a => new AutonomyRecentActivity
                {
                    Activity = a.Activity,
                    Title = a.Title,
                    Topic = a.Topic,
                    TimestampUtc = a.TimestampUtc
                }).ToList(),
                Plans = source.Plans.Select(p => new AutonomyPlan
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    CreatedUtc = p.CreatedUtc,
                    Steps = p.Steps.Select(s => new AutonomyPlanStep
                    {
                        Description = s.Description,
                        Done = s.Done,
                        CompletedUtc = s.CompletedUtc
                    }).ToList()
                }).ToList(),
                RecentOutcomes = source.RecentOutcomes.Select(o => new AutonomyOutcome
                {
                    Activity = o.Activity,
                    Topic = o.Topic,
                    ProjectId = o.ProjectId,
                    Score = o.Score,
                    Note = o.Note,
                    TimestampUtc = o.TimestampUtc
                }).ToList(),
                ProjectCooldownUntil = new Dictionary<string, DateTime>(source.ProjectCooldownUntil),
                TopicCooldownUntil = new Dictionary<string, DateTime>(source.TopicCooldownUntil),
                LastFocusProjectId = source.LastFocusProjectId,
                SameFocusStreak = source.SameFocusStreak,
                SelfGoalsToday = source.SelfGoalsToday,
                SelfGoalDayStartUtc = source.SelfGoalDayStartUtc,
                ConsecutiveDecisionFailures = source.ConsecutiveDecisionFailures,
                UserGuidanceSuggestion = source.UserGuidanceSuggestion
            };

        private async Task AppendTradingBridgeStatusAsync(Project project, AIContact contact)
        {
            if (_tradingService == null)
                return;

            var status = await _tradingService.GetStatusAsync().ConfigureAwait(false);
            var account = await _tradingService.GetAccountInfoAsync().ConfigureAwait(false);
            var summary = Mt4TradeBridgeHelper.FormatBridgeStatus(status, account);

            await _projects.AddLogEntryAsync(project.Id, new ProjectLog
            {
                ProjectId = project.Id,
                PerformedBy = contact.Id,
                Action = "Autonomy: MT4 bridge status",
                Details = summary
            }).ConfigureAwait(false);
        }

        private async Task TryExecuteTradeBlockAsync(
            AIContact contact,
            Project project,
            string note,
            CancellationToken cancellationToken)
        {
            if (_tradingService == null)
                return;

            var request = Mt4TradeBridgeHelper.TryParseTradeRequest(note);
            if (request == null)
                return;

            var result = await _tradingService.ExecuteTradeAsync(request, cancellationToken).ConfigureAwait(false);
            var details = result.Success
                ? $"Trade executed. Ticket={result.Ticket}. {result.Message}"
                : $"Trade failed. {result.Message}";

            await _projects.AddLogEntryAsync(project.Id, new ProjectLog
            {
                ProjectId = project.Id,
                PerformedBy = contact.Id,
                Action = result.Success ? "Autonomy: trade executed" : "Autonomy: trade failed",
                Details = details
            }).ConfigureAwait(false);

            await RecordJournalAsync(
                AutonomyActivityKind.ExecuteTrade,
                result.Success ? "Live trade executed" : "Live trade failed",
                details,
                project.Id,
                project.Name).ConfigureAwait(false);
        }

        private async Task ExecuteTradeDecisionAsync(
            AIContact contact,
            AutonomyDecision decision,
            CancellationToken cancellationToken)
        {
            if (_tradingService == null)
                throw new InvalidOperationException("Trading service is not configured.");

            var request = Mt4TradeBridgeHelper.TryParseTradeRequest(decision.Detail)
                ?? throw new InvalidOperationException(
                    "Trade decision detail must contain JSON, e.g. {\"Symbol\":\"EURUSD\",\"Type\":0,\"Volume\":0.01,\"StopLoss\":1.0830}. " +
                    "Put ONLY the trade JSON in detail (no prose). StopLoss is required by the MT4 bridge.");

            var status = await _tradingService.GetStatusAsync().ConfigureAwait(false);
            if (!status.IsConnected || !status.IsBridgeActive)
            {
                throw new InvalidOperationException(
                    $"MT4 bridge not ready. Connected={status.IsConnected}, BridgeActive={status.IsBridgeActive}");
            }

            var result = await _tradingService.ExecuteTradeAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);

            var project = await ResolveProjectForDecisionAsync(decision).ConfigureAwait(false);
            if (project != null)
            {
                await _projects.AddLogEntryAsync(project.Id, new ProjectLog
                {
                    ProjectId = project.Id,
                    PerformedBy = contact.Id,
                    Action = "Autonomy: trade executed",
                    Details = $"Ticket={result.Ticket}. {result.Message}"
                }).ConfigureAwait(false);
            }

            await RecordJournalAsync(
                AutonomyActivityKind.ExecuteTrade,
                decision.Title,
                $"Ticket={result.Ticket}. {result.Message}",
                project?.Id,
                project?.Name).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(
                contact,
                $"Executed trade {request.Symbol} {request.Type} {request.Volume}: {result.Message}").ConfigureAwait(false);
        }

        private async Task ExecuteMarketScanAsync(
            AIContact contact,
            CancellationToken cancellationToken)
        {
            if (_tradingService == null || _marketWatch == null)
                return;

            var toProcess = _marketWatch.ConsumePendingAlerts();
            if (toProcess.Count == 0)
            {
                await CompleteTickAsync(AutonomyActivityKind.ScanMarkets, "Market watch: no pending alerts.").ConfigureAwait(false);
                return;
            }

            var status = await _tradingService.GetStatusAsync().ConfigureAwait(false);
            if (!status.IsConnected || !status.IsBridgeActive)
            {
                await CompleteTickAsync(AutonomyActivityKind.ScanMarkets, "Market watch: MT4 bridge not active.").ConfigureAwait(false);
                return;
            }

            var alertLines = string.Join(Environment.NewLine,
                toProcess.Select(a => $"- [{a.AlertType}] {a.Message}"));

            var quotes = new List<string>();
            double exampleBid = 0;
            double exampleAsk = 0;
            foreach (var symbol in _marketWatch.WatchSymbols.Take(20))
            {
                var q = await _tradingService.GetMarketDataAsync(symbol).ConfigureAwait(false);
                if (q != null)
                {
                    quotes.Add($"{symbol}: bid={q.Bid:F5} ask={q.Ask:F5} spread={(q.Ask - q.Bid):F5}");
                    if (exampleBid <= 0 && symbol.Equals("EURUSD", StringComparison.OrdinalIgnoreCase))
                    {
                        exampleBid = q.Bid;
                        exampleAsk = q.Ask;
                    }
                }
            }
            if (exampleBid <= 0 && quotes.Count > 0)
            {
                var first = await _tradingService.GetMarketDataAsync(_marketWatch.WatchSymbols.First()).ConfigureAwait(false);
                if (first != null)
                {
                    exampleBid = first.Bid;
                    exampleAsk = first.Ask;
                }
            }

            var allProjects = await _projects.GetAllProjectsAsync().ConfigureAwait(false);
            var project = allProjects.FirstOrDefault(p =>
                              p.Phase != ProjectPhase.Completed &&
                              p.Name.Equals(MarketWatchProjectBootstrap.ProjectName, StringComparison.OrdinalIgnoreCase))
                          ?? allProjects.FirstOrDefault(p =>
                              p.Phase != ProjectPhase.Completed &&
                              ContainsTradingKeywords(p.Name, p.Description, null, null));

            var technicalAlerts = toProcess.Where(a => a.AlertType.StartsWith("technical_", StringComparison.Ordinal)).ToList();
            var technicalHint = technicalAlerts.Count > 0
                ? $"\n\n**Technical signals** ({technicalAlerts.Count}): prioritize backtest with suggested strategy_type from alerts."
                : string.Empty;

            var tradeBlockExample = BuildTradeBlockExample(exampleBid, exampleAsk);

            var prompt = $"""
                You are {contact.Name}, monitoring FX/CFD markets via the House Victoria MT4 bridge.

                **Alerts this scan** ({toProcess.Count}):
                {alertLines}{technicalHint}

                **Current quotes** (watchlist sample):
                {string.Join(Environment.NewLine, quotes)}

                Review these moves across pairs. For each alert worth acting on:
                1. State the setup (trend, mean-reversion, breakout) and timeframe.
                2. If validation is needed, include a ```backtest``` JSON block for that symbol.
                3. Only include a ```trade``` block if you want a small demo execution (0.01 lot). **Every trade block MUST include StopLoss** (~20 pips from bid/ask), e.g.:
                   ```trade
                   {tradeBlockExample}
                   ```

                Do not claim trades ran without a trade block. Be concise (300-500 words).
                """;

            var note = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);

            if (project != null)
            {
                await _projects.AddLogEntryAsync(project.Id, new ProjectLog
                {
                    ProjectId = project.Id,
                    PerformedBy = contact.Id,
                    Action = "Autonomy: market scan",
                    Details = note.Trim()
                }).ConfigureAwait(false);

                await RunTradingBacktestsAsync(contact, project, note, cancellationToken).ConfigureAwait(false);
                await TryExecuteTradeBlockAsync(contact, project, note, cancellationToken).ConfigureAwait(false);
            }

            var summary = $"Market scan: {toProcess.Count} alert(s) — {string.Join("; ", toProcess.Take(3).Select(a => a.Symbol))}";
            await RecordJournalAsync(
                AutonomyActivityKind.ScanMarkets,
                "Multi-pair market scan",
                note.Trim(),
                project?.Id,
                project?.Name).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, $"{summary}\n{note}").ConfigureAwait(false);

            lock (_stateLock)
                DriveSystem.Satisfy(_state, AutonomyActivityKind.ScanMarkets);
            RecordRecentActivity(AutonomyActivityKind.ScanMarkets, "Market scan", null);
            await ScoreOutcomeAsync(contact, AutonomyActivityKind.ScanMarkets, null, project?.Id, note, cancellationToken)
                .ConfigureAwait(false);

            RecordSubstantiveAction();
            SetVitalsForActivity(AutonomyActivityKind.ScanMarkets, summary, holdSeconds: 60);
            await CompleteTickAsync(AutonomyActivityKind.ScanMarkets, summary).ConfigureAwait(false);
            ActivityCompleted?.Invoke(this, new AutonomyActivityEventArgs
            {
                Activity = AutonomyActivityKind.ScanMarkets,
                Summary = summary
            });
        }

        private async Task RunTradingBacktestsAsync(
            AIContact contact,
            Project project,
            string note,
            CancellationToken cancellationToken)
        {
            if (_tradingService == null)
                return;

            var status = await _tradingService.GetStatusAsync().ConfigureAwait(false);
            if (!status.IsConnected)
                return;

            var request = Mt4TradeBridgeHelper.TryParseBacktestRequest(note)
                ?? BuildDefaultBacktestRequest(project, note);

            var result = await RunBacktestWithExportFallbackAsync(request, cancellationToken).ConfigureAwait(false);
            var summary = Mt4TradeBridgeHelper.FormatBacktestSummary(result);
            var details = result.Success
                ? summary
                : $"{summary} — ensure {request.Symbol} {request.TimeFrame} history is in MT4 (EA export or History Center).";

            await _projects.AddLogEntryAsync(project.Id, new ProjectLog
            {
                ProjectId = project.Id,
                PerformedBy = contact.Id,
                Action = result.Success ? "Autonomy: backtest completed" : "Autonomy: backtest failed",
                Details = details
            }).ConfigureAwait(false);

            await RecordJournalAsync(
                AutonomyActivityKind.RunBacktest,
                result.Success ? $"Backtest {request.StrategyName}" : "Backtest failed",
                details,
                project.Id,
                project.Name).ConfigureAwait(false);

            if (result.Success)
                await AppendAutonomyMemoryAsync(contact, summary).ConfigureAwait(false);
        }

        private async Task<BacktestResult> RunBacktestWithExportFallbackAsync(
            BacktestRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _tradingService!.RunBacktestAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Success ||
                result.ErrorMessage == null ||
                !result.ErrorMessage.Contains("No historical data", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            HistoricalExportResult? lastExport = null;

            async Task<BacktestResult?> TryExportAndBacktestAsync(
                string symbol,
                TimeFrame timeFrame,
                DateTime startDate,
                DateTime endDate)
            {
                var export = await _tradingService.ExportHistoricalDataAsync(
                    symbol,
                    timeFrame,
                    startDate,
                    endDate,
                    cancellationToken).ConfigureAwait(false);
                lastExport = export;

                if (!export.Success || export.BarsExported <= 0)
                    return null;

                var retryRequest = new BacktestRequest
                {
                    StrategyName = request.StrategyName,
                    Symbol = request.Symbol,
                    TimeFrame = timeFrame,
                    StartDate = startDate,
                    EndDate = endDate,
                    InitialDeposit = request.InitialDeposit,
                    LotSize = request.LotSize,
                    StrategyParameters = new Dictionary<string, object>(request.StrategyParameters)
                };

                var retry = await _tradingService.RunBacktestAsync(retryRequest, cancellationToken).ConfigureAwait(false);
                return retry.Success ? retry : null;
            }

            var success = await TryExportAndBacktestAsync(
                request.Symbol,
                request.TimeFrame,
                request.StartDate,
                request.EndDate).ConfigureAwait(false);
            if (success != null)
                return success;

            // Shorter window — MT4 often has recent bars but not full 6-month history.
            var shortStart = DateTime.UtcNow.AddDays(-14);
            success = await TryExportAndBacktestAsync(
                request.Symbol,
                request.TimeFrame,
                shortStart,
                request.EndDate).ConfigureAwait(false);
            if (success != null)
                return success;

            // Intraday timeframes for scalp/intraday projects.
            foreach (var tf in new[] { TimeFrame.M15, TimeFrame.M5, TimeFrame.M1, TimeFrame.H1 })
            {
                if (tf == request.TimeFrame)
                    continue;

                success = await TryExportAndBacktestAsync(
                    request.Symbol,
                    tf,
                    shortStart,
                    request.EndDate).ConfigureAwait(false);
                if (success != null)
                    return success;
            }

            if (lastExport != null && !string.IsNullOrWhiteSpace(lastExport.Message))
            {
                result.ErrorMessage =
                    $"{result.ErrorMessage} Bridge export: {lastExport.Message}";
            }

            return result;
        }

        private static BacktestRequest BuildDefaultBacktestRequest(Project project, string note)
        {
            var symbol = InferSymbolFromText(project.Name, project.Description, note) ?? "EURUSD";
            var strategyType = InferStrategyTypeFromText(note);
            var timeFrame = InferTimeFrameFromText(project.Description, note);

            return new BacktestRequest
            {
                StrategyName = $"Auto-{SanitizeFileName(project.Name)}",
                Symbol = symbol,
                TimeFrame = timeFrame,
                StartDate = DateTime.UtcNow.AddDays(-30),
                EndDate = DateTime.UtcNow,
                InitialDeposit = 10000,
                LotSize = 0.01,
                StrategyParameters = new Dictionary<string, object>
                {
                    ["strategy_type"] = strategyType,
                    ["fast_period"] = 10,
                    ["slow_period"] = 30
                }
            };
        }

        private static TimeFrame InferTimeFrameFromText(params string?[] parts)
        {
            var text = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToUpperInvariant();
            if (text.Contains("M1", StringComparison.Ordinal) || text.Contains("1 MIN", StringComparison.Ordinal))
                return TimeFrame.M1;
            if (text.Contains("M5", StringComparison.Ordinal) || text.Contains("5 MIN", StringComparison.Ordinal))
                return TimeFrame.M5;
            if (text.Contains("M15", StringComparison.Ordinal) || text.Contains("15 MIN", StringComparison.Ordinal))
                return TimeFrame.M15;
            if (text.Contains("H4", StringComparison.Ordinal))
                return TimeFrame.H4;
            if (text.Contains("D1", StringComparison.Ordinal) || text.Contains("DAILY", StringComparison.Ordinal))
                return TimeFrame.D1;
            if (text.Contains("H1", StringComparison.Ordinal) || text.Contains("1HR", StringComparison.Ordinal) ||
                text.Contains("1 HOUR", StringComparison.Ordinal))
                return TimeFrame.H1;
            if (text.Contains("INTRADAY", StringComparison.Ordinal) ||
                text.Contains("SCALP", StringComparison.Ordinal))
                return TimeFrame.M5;

            return TimeFrame.M15;
        }

        private async Task ExecuteBacktestDecisionAsync(
            AIContact contact,
            AutonomyDecision decision,
            CancellationToken cancellationToken)
        {
            if (_tradingService == null)
                throw new InvalidOperationException("Trading service is not configured.");

            var request = Mt4TradeBridgeHelper.TryParseBacktestRequest(decision.Detail)
                ?? throw new InvalidOperationException(
                    "Backtest decision detail must contain JSON with symbol, time_frame, dates, and strategy_type.");

            var result = await RunBacktestWithExportFallbackAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException(result.ErrorMessage ?? "Backtest failed");

            var project = await ResolveProjectForDecisionAsync(decision).ConfigureAwait(false);
            var summary = Mt4TradeBridgeHelper.FormatBacktestSummary(result);

            if (project != null)
            {
                await _projects.AddLogEntryAsync(project.Id, new ProjectLog
                {
                    ProjectId = project.Id,
                    PerformedBy = contact.Id,
                    Action = "Autonomy: backtest completed",
                    Details = summary
                }).ConfigureAwait(false);
            }

            await RecordJournalAsync(
                AutonomyActivityKind.RunBacktest,
                decision.Title,
                summary,
                project?.Id,
                project?.Name).ConfigureAwait(false);

            await AppendAutonomyMemoryAsync(contact, summary).ConfigureAwait(false);
        }

        private static string? InferSymbolFromText(params string?[] parts)
        {
            var text = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToUpperInvariant();
            foreach (var sym in new[] { "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "USDCHF", "NZDUSD", "XAUUSD" })
            {
                if (text.Contains(sym, StringComparison.Ordinal))
                    return sym;
            }

            return null;
        }

        private static string InferStrategyTypeFromText(string text)
        {
            var lower = text.ToLowerInvariant();
            if (lower.Contains("rsi", StringComparison.Ordinal))
                return "rsi";
            if (lower.Contains("breakout", StringComparison.Ordinal) || lower.Contains("donchian", StringComparison.Ordinal))
                return "breakout";
            return "ma_crossover";
        }

        private static string BuildTradeBlockExample(double bid, double ask)
        {
            if (bid <= 0 || ask <= 0)
                return """{"Symbol":"EURUSD","Type":0,"Volume":0.01,"StopLoss":"<bid minus 20 pips>","TakeProfit":"<ask plus 40 pips>"}""";

            var sl = Math.Round(bid - 0.0020, 5);
            var tp = Math.Round(ask + 0.0040, 5);
            return $$"""{"Symbol":"EURUSD","Type":0,"Volume":0.01,"StopLoss":{{sl}},"TakeProfit":{{tp}}}""";
        }

        private static bool ContainsTradingKeywords(params string?[] parts)
        {
            var keywords = new[] { "forex", "fx", "mt4", "metatrader", "trading", "strategy", "backtest", "eurusd", "pip", "bridge" };
            var text = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
            return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "topic" : name[..Math.Min(40, name.Length)];
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "…";

        private void SetVitalsForActivity(AutonomyActivityKind activity, string summary, int holdSeconds = 30)
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
                snapshot.CurrentActivityStartedUtc = _state.CurrentActivityStartedUtc;
                snapshot.PreviousActivitySummary = _state.PreviousActivitySummary;
                snapshot.PreviousActivityStartedUtc = _state.PreviousActivityStartedUtc;
                snapshot.PreviousActivityEndedUtc = _state.PreviousActivityEndedUtc;
                snapshot.AutonomyRunning = _state.IsRunning;
                snapshot.UpdatedUtc = DateTime.UtcNow;
                _vitals = snapshot;
            }

            VitalsChanged?.Invoke(this, new CognitionVitalsChangedEventArgs { Vitals = snapshot });
        }
    }
}
