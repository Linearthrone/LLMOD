using System.Text.Json;
using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Journals
{
    public sealed class JournalService : IJournalService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _storePath;
        private readonly string _autonomyJournalPath;
        private readonly IAIService? _aiService;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly List<ResearchJournal> _journals = new();
        private bool _syncAttempted;
        private int _consolidationVersionApplied;
        private int _suppressNotifications;

        public event EventHandler<JournalUpdatedEventArgs>? JournalUpdated;

        public JournalService(AppConfig appConfig, IAIService? aiService = null)
        {
            _aiService = aiService;
            var basePath = appConfig.AutonomyDataPath;
            if (!Path.IsPathRooted(basePath))
            {
                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                             ?? AppDomain.CurrentDomain.BaseDirectory;
                basePath = Path.Combine(appDir, basePath);
            }

            Directory.CreateDirectory(basePath);
            _storePath = Path.Combine(basePath, "journals.json");
            _autonomyJournalPath = Path.Combine(basePath, "journal.jsonl");
            LoadFromDisk();
        }

        public async Task<IReadOnlyList<ResearchJournal>> GetAllJournalsAsync()
        {
            await EnsureSyncedAsync().ConfigureAwait(false);
            await EnsureConsolidatedAsync().ConfigureAwait(false);
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                return _journals
                    .OrderByDescending(j => j.UpdatedAt)
                    .Select(CloneJournal)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ResearchJournal?> GetJournalAsync(string journalId)
        {
            await EnsureSyncedAsync().ConfigureAwait(false);
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId);
                return journal == null ? null : CloneJournal(journal);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ResearchJournal?> FindByProjectIdAsync(string projectId)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j =>
                    string.Equals(j.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
                return journal == null ? null : CloneJournal(journal);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ResearchJournal?> FindByTopicAsync(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                return null;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var match = FindBestMatchingJournalLocked(topic, null, null, topic);
                return match == null ? null : CloneJournal(match);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<string?> GetPriorWorkContextAsync(string topic, string? projectId = null, int maxEntries = 6)
        {
            await EnsureConsolidatedAsync().ConfigureAwait(false);
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = FindBestMatchingJournalLocked(topic, projectId, null, topic);
                if (journal == null || journal.Entries.Count == 0)
                    return null;

                var snippets = journal.Entries
                    .Where(e => e.Kind != JournalEntryKind.Conclusion)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxEntries)
                    .Select(e => $"[{e.Timestamp:yyyy-MM-dd}] {e.Title}\n{Truncate(e.Body, 600)}");

                return string.Join("\n\n---\n\n", snippets);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ConsolidateSimilarJournalsAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                MergeSimilarJournalsLocked();
                await PersistAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ResearchJournal> CreateJournalAsync(
            string title,
            string preface,
            string? topic = null,
            string? projectId = null,
            string? projectName = null)
        {
            var journal = new ResearchJournal
            {
                Title = title.Trim(),
                Topic = string.IsNullOrWhiteSpace(topic) ? title.Trim() : topic.Trim(),
                Preface = preface.Trim(),
                ProjectId = projectId,
                ProjectName = projectName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _journals.Add(journal);
                if (Volatile.Read(ref _suppressNotifications) == 0)
                    await PersistAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

            if (Volatile.Read(ref _suppressNotifications) == 0)
                RaiseUpdated(journal);
            return CloneJournal(journal);
        }

        public async Task<ResearchJournal> FindOrCreateForProjectAsync(
            string projectId,
            string projectName,
            string preface)
        {
            var existing = await FindByProjectIdAsync(projectId).ConfigureAwait(false);
            if (existing != null)
                return existing;

            return await CreateJournalAsync(
                projectName,
                preface,
                topic: projectName,
                projectId: projectId,
                projectName: projectName).ConfigureAwait(false);
        }

        public async Task<ResearchJournal> FindOrCreateForTopicAsync(
            string topic,
            string preface,
            string? projectId = null,
            string? projectName = null)
        {
            var canonical = JournalTopicMatcher.ExtractCanonicalTopic(topic);
            await _lock.WaitAsync().ConfigureAwait(false);
            ResearchJournal? existing;
            try
            {
                existing = FindBestMatchingJournalLocked(topic, projectId, projectName, topic);
            }
            finally
            {
                _lock.Release();
            }

            if (existing != null)
                return CloneJournal(existing);

            return await CreateJournalAsync(
                FormatJournalTitle(canonical),
                preface,
                topic: canonical,
                projectId: projectId,
                projectName: projectName).ConfigureAwait(false);
        }

        public async Task AddPageEntryAsync(string journalId, JournalPageEntry entry)
        {
            ResearchJournal? updated = null;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId)
                              ?? throw new InvalidOperationException($"Journal not found: {journalId}");

                if (journal.Entries.Any(e => e.Id == entry.Id ||
                    (!string.IsNullOrWhiteSpace(entry.AutonomyJournalEntryId) &&
                     e.AutonomyJournalEntryId == entry.AutonomyJournalEntryId)))
                {
                    return;
                }

                journal.Entries.Add(entry);
                journal.UpdatedAt = DateTime.Now;
                await PersistAsync().ConfigureAwait(false);
                updated = journal;
            }
            finally
            {
                _lock.Release();
            }

            if (updated != null)
                RaiseUpdated(updated);
        }

        public async Task AddReferenceAsync(string journalId, ReferencedMaterial reference, string? entryId = null)
        {
            ResearchJournal? updated = null;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId)
                              ?? throw new InvalidOperationException($"Journal not found: {journalId}");

                if (!string.IsNullOrWhiteSpace(entryId))
                {
                    var entry = journal.Entries.FirstOrDefault(e => e.Id == entryId);
                    if (entry != null)
                        entry.References.Add(reference);
                }
                else
                {
                    var lastResearch = journal.Entries.LastOrDefault(e => e.Kind == JournalEntryKind.Research);
                    if (lastResearch != null)
                        lastResearch.References.Add(reference);
                }

                journal.UpdatedAt = DateTime.Now;
                await PersistAsync().ConfigureAwait(false);
                updated = journal;
            }
            finally
            {
                _lock.Release();
            }

            if (updated != null)
                RaiseUpdated(updated);
        }

        public async Task<ResearchJournal> RecordAutonomyActivityAsync(
            AutonomyJournalEntry autonomyEntry,
            string? topicTitle = null,
            string? prefaceReason = null)
        {
            var (title, reason) = ParseSummaryParts(autonomyEntry.Summary);
            var topic = topicTitle ?? title ?? autonomyEntry.ProjectName ?? "Personal reflections";
            var preface = prefaceReason ?? reason ?? autonomyEntry.Summary;

            var journal = await ResolveJournalForEntryAsync(
                autonomyEntry, topic, preface, title).ConfigureAwait(false);

            var kind = MapActivityKind(autonomyEntry.Activity);
            var pageEntry = new JournalPageEntry
            {
                Timestamp = autonomyEntry.Timestamp,
                Kind = kind,
                Title = title ?? autonomyEntry.Summary,
                Body = autonomyEntry.Body ?? autonomyEntry.Summary,
                LinkedFilePaths = (autonomyEntry.LinkedFilePaths ?? new List<string>()).ToList(),
                AutonomyJournalEntryId = autonomyEntry.Id,
                SourceActivity = autonomyEntry.Activity
            };

            ExtractReferencesFromBody(pageEntry);
            await AddPageEntryAsync(journal.Id, pageEntry).ConfigureAwait(false);

            var updated = await GetJournalAsync(journal.Id).ConfigureAwait(false);
            return updated ?? journal;
        }

        private async Task<ResearchJournal> ResolveJournalForEntryAsync(
            AutonomyJournalEntry autonomyEntry,
            string topic,
            string preface,
            string? entryTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(autonomyEntry.ProjectId) &&
                !JournalTopicMatcher.IsGenericResearchBucket(autonomyEntry.ProjectName))
            {
                return await FindOrCreateForProjectAsync(
                    autonomyEntry.ProjectId,
                    autonomyEntry.ProjectName ?? topic,
                    preface).ConfigureAwait(false);
            }

            var canonical = JournalTopicMatcher.ExtractCanonicalTopic(topic);
            await _lock.WaitAsync().ConfigureAwait(false);
            ResearchJournal? existing;
            try
            {
                existing = FindBestMatchingJournalLocked(canonical, autonomyEntry.ProjectId, autonomyEntry.ProjectName, entryTitle ?? topic);
            }
            finally
            {
                _lock.Release();
            }

            if (existing != null)
                return CloneJournal(existing);

            return await CreateJournalAsync(
                FormatJournalTitle(canonical),
                preface,
                topic: canonical,
                projectId: autonomyEntry.ProjectId,
                projectName: autonomyEntry.ProjectName).ConfigureAwait(false);
        }

        public async Task ConcludeJournalAsync(
            string journalId,
            string summary,
            string implications,
            IEnumerable<ReferencedMaterial>? citations = null)
        {
            ResearchJournal? updated = null;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId)
                              ?? throw new InvalidOperationException($"Journal not found: {journalId}");

                journal.Status = JournalStatus.Concluded;
                journal.ConcludedAt = DateTime.Now;
                journal.ConclusionSummary = summary.Trim();
                journal.ConclusionImplications = implications.Trim();
                journal.ConclusionCitations = citations?.ToList() ?? CollectExternalReferences(journal);
                journal.UpdatedAt = DateTime.Now;

                journal.Entries.Add(new JournalPageEntry
                {
                    Kind = JournalEntryKind.Conclusion,
                    Title = "Conclusion",
                    Body = $"{summary.Trim()}\n\nImplications:\n{implications.Trim()}",
                    Timestamp = DateTime.Now
                });

                await PersistAsync().ConfigureAwait(false);
                updated = journal;
            }
            finally
            {
                _lock.Release();
            }

            if (updated != null)
                RaiseUpdated(updated);
        }

        public async Task<ResearchJournal?> GenerateConclusionAsync(string journalId, AIContact contact)
        {
            if (_aiService == null)
                return null;

            var journal = await GetJournalAsync(journalId).ConfigureAwait(false);
            if (journal == null || journal.Status == JournalStatus.Concluded)
                return journal;

            var entriesText = string.Join("\n\n", journal.Entries
                .Where(e => e.Kind != JournalEntryKind.Conclusion)
                .Select(e => $"[{e.Timestamp:yyyy-MM-dd}] {e.Title}\n{e.Body}"));

            var refsText = string.Join("\n", CollectExternalReferences(journal)
                .Select(r => $"- {r.Title}: {r.Source ?? r.Url ?? r.Notes}"));

            var prompt = $"""
                You are {contact.Name}, concluding your research journal titled "{journal.Title}".
                Preface (why you pursued this): {journal.Preface}

                Journal entries:
                {entriesText}

                Referenced materials:
                {refsText}

                Write a conclusion with two sections:
                1. SUMMARY: findings and what you learned (3-6 sentences)
                2. IMPLICATIONS: what these findings mean for you and next steps (2-4 sentences)

                Cite EXTERNAL sources only (publications, documentation, technologies) in the summary.
                Do not cite Victoria's own prior journal entries as primary sources.
                """;

            var response = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            var (summary, implications) = ParseConclusionResponse(response);

            var citations = CollectExternalReferences(journal);
            await ConcludeJournalAsync(journalId, summary, implications, citations).ConfigureAwait(false);
            return await GetJournalAsync(journalId).ConfigureAwait(false);
        }

        public async Task SyncFromAutonomyLogAsync()
        {
            if (!File.Exists(_autonomyJournalPath))
                return;

            var lines = await File.ReadAllLinesAsync(_autonomyJournalPath).ConfigureAwait(false);
            if (lines.Length == 0)
                return;

            Interlocked.Increment(ref _suppressNotifications);
            try
            {
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var entry = JsonSerializer.Deserialize<AutonomyJournalEntry>(line, JsonOptions);
                        if (entry != null)
                            await RecordAutonomyActivityCoreAsync(entry).ConfigureAwait(false);
                    }
                    catch
                    {
                        // skip malformed lines
                    }
                }

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
            finally
            {
                Interlocked.Decrement(ref _suppressNotifications);
            }

            RaiseUpdated(new ResearchJournal { Title = "All journals" });
        }

        /// <summary>Import path: one persist at end of batch, no per-entry disk writes.</summary>
        private async Task RecordAutonomyActivityCoreAsync(
            AutonomyJournalEntry autonomyEntry,
            string? topicTitle = null,
            string? prefaceReason = null)
        {
            var (title, reason) = ParseSummaryParts(autonomyEntry.Summary);
            var topic = topicTitle ?? title ?? autonomyEntry.ProjectName ?? "Personal reflections";
            var preface = prefaceReason ?? reason ?? autonomyEntry.Summary;

            var journal = await ResolveJournalForEntryAsync(
                autonomyEntry, topic, preface, title).ConfigureAwait(false);

            var kind = MapActivityKind(autonomyEntry.Activity);
            var pageEntry = new JournalPageEntry
            {
                Timestamp = autonomyEntry.Timestamp,
                Kind = kind,
                Title = title ?? autonomyEntry.Summary,
                Body = autonomyEntry.Body ?? autonomyEntry.Summary,
                LinkedFilePaths = (autonomyEntry.LinkedFilePaths ?? new List<string>()).ToList(),
                AutonomyJournalEntryId = autonomyEntry.Id,
                SourceActivity = autonomyEntry.Activity
            };

            ExtractReferencesFromBody(pageEntry);
            await AddPageEntryWithoutPersistAsync(journal.Id, pageEntry).ConfigureAwait(false);
        }

        private async Task AddPageEntryWithoutPersistAsync(string journalId, JournalPageEntry entry)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId);
                if (journal == null)
                    return;

                if (journal.Entries.Any(e => e.Id == entry.Id ||
                    (!string.IsNullOrWhiteSpace(entry.AutonomyJournalEntryId) &&
                     e.AutonomyJournalEntryId == entry.AutonomyJournalEntryId)))
                {
                    return;
                }

                journal.Entries.Add(entry);
                journal.UpdatedAt = DateTime.Now;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task EnsureSyncedAsync()
        {
            if (_syncAttempted)
                return;

            _syncAttempted = true;
            if (_journals.Count == 0 && File.Exists(_autonomyJournalPath))
                await Task.Run(async () => await SyncFromAutonomyLogAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        private void LoadFromDisk()
        {
            if (!File.Exists(_storePath))
                return;

            try
            {
                var json = File.ReadAllText(_storePath);
                var bundle = JsonSerializer.Deserialize<JournalStoreBundle>(json, JsonOptions);
                if (bundle?.Journals != null)
                    _journals.AddRange(bundle.Journals);
                _consolidationVersionApplied = bundle?.ConsolidationVersionApplied ?? 0;
            }
            catch
            {
                // start fresh on corruption
            }
        }

        private async Task PersistAsync()
        {
            var bundle = new JournalStoreBundle
            {
                Journals = _journals,
                ConsolidationVersionApplied = _consolidationVersionApplied
            };
            var json = JsonSerializer.Serialize(bundle, JsonOptions);
            await File.WriteAllTextAsync(_storePath, json).ConfigureAwait(false);
        }

        private void RaiseUpdated(ResearchJournal journal)
        {
            if (Volatile.Read(ref _suppressNotifications) > 0)
                return;

            JournalUpdated?.Invoke(this, new JournalUpdatedEventArgs { Journal = CloneJournal(journal) });
        }

        private static ResearchJournal CloneJournal(ResearchJournal source) =>
            JsonSerializer.Deserialize<ResearchJournal>(
                JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? new ResearchJournal();

        private static JournalEntryKind MapActivityKind(AutonomyActivityKind activity) => activity switch
        {
            AutonomyActivityKind.WriteResearch => JournalEntryKind.Research,
            AutonomyActivityKind.WorkOnPriorityProject => JournalEntryKind.ProjectWork,
            AutonomyActivityKind.AdvancePersonalProject => JournalEntryKind.ProjectWork,
            AutonomyActivityKind.Reflect => JournalEntryKind.Reflection,
            AutonomyActivityKind.CreateArt => JournalEntryKind.Art,
            AutonomyActivityKind.ExploreEnvironment => JournalEntryKind.Environment,
            _ => JournalEntryKind.Thought
        };

        private static (string? title, string? reason) ParseSummaryParts(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
                return (null, null);

            var parts = summary.Split('—', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                return (parts[0], parts[1]);

            parts = summary.Split('-', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? (parts[0], parts[1]) : (summary, null);
        }

        private async Task EnsureConsolidatedAsync()
        {
            if (_consolidationVersionApplied >= JournalTopicMatcher.ConsolidationVersion)
                return;

            if (_journals.Count > 1)
                await ConsolidateSimilarJournalsAsync().ConfigureAwait(false);

            _consolidationVersionApplied = JournalTopicMatcher.ConsolidationVersion;
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

        private ResearchJournal? FindBestMatchingJournalLocked(string topic, string? projectId, string? projectName, string? title = null)
        {
            if (!string.IsNullOrWhiteSpace(projectId) &&
                !JournalTopicMatcher.IsGenericResearchBucket(projectName))
            {
                var byProject = _journals.FirstOrDefault(j =>
                    string.Equals(j.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
                if (byProject != null)
                    return byProject;
            }

            var incomingCluster = JournalTopicMatcher.GetResearchCluster(topic, title);
            if (incomingCluster != null)
            {
                var byCluster = _journals.FirstOrDefault(j =>
                    JournalTopicMatcher.GetResearchCluster(j.Topic, j.Title) == incomingCluster);
                if (byCluster != null)
                    return byCluster;
            }

            var candidates = _journals
                .Select(j => (j.Topic, j.Title, j.ProjectId, j.ProjectName))
                .ToList();
            var index = JournalTopicMatcher.FindBestMatchIndex(candidates, topic, title, projectId, projectName);
            return index >= 0 ? _journals[index] : null;
        }

        private void MergeSimilarJournalsLocked()
        {
            if (_journals.Count < 2)
                return;

            var n = _journals.Count;
            var parent = Enumerable.Range(0, n).ToArray();

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra != rb)
                    parent[rb] = ra;
            }

            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var a = _journals[i];
                    var b = _journals[j];
                    if (JournalTopicMatcher.ShouldMergeJournals(
                            a.Topic, a.Title, a.ProjectId, a.ProjectName,
                            b.Topic, b.Title, b.ProjectId, b.ProjectName))
                    {
                        Union(i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<ResearchJournal>>();
            for (var i = 0; i < n; i++)
            {
                var root = Find(i);
                if (!groups.ContainsKey(root))
                    groups[root] = new List<ResearchJournal>();
                groups[root].Add(_journals[i]);
            }

            var survivors = new List<ResearchJournal>();
            foreach (var group in groups.Values)
            {
                if (group.Count == 1)
                {
                    survivors.Add(group[0]);
                    continue;
                }

                var primaryIndex = JournalTopicMatcher.PickPrimaryJournalIndex(
                    group,
                    j => j.ProjectId,
                    j => j.ProjectName,
                    j => j.Entries.Count,
                    j => j.CreatedAt);

                var primary = group[primaryIndex];
                for (var i = 0; i < group.Count; i++)
                {
                    if (i == primaryIndex)
                        continue;
                    MergeJournalInto(primary, group[i]);
                }

                ApplyGroupMetadata(primary, group);
                survivors.Add(primary);
            }

            _journals.Clear();
            _journals.AddRange(survivors);

            RedistributeGenericBucketEntriesLocked();
        }

        /// <summary>Move entries out of the generic research bucket into their thematic cluster journals.</summary>
        private void RedistributeGenericBucketEntriesLocked()
        {
            var buckets = _journals
                .Where(j => JournalTopicMatcher.IsGenericResearchBucket(j.ProjectName) ||
                            j.Title.Contains("backlog", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var bucket in buckets)
            {
                foreach (var entry in bucket.Entries.ToList())
                {
                    var cluster = JournalTopicMatcher.GetResearchCluster(entry.Title, entry.Title);
                    if (cluster == null)
                        continue;

                    var target = _journals.FirstOrDefault(j =>
                        j != bucket &&
                        JournalTopicMatcher.GetResearchCluster(j.Topic, j.Title) == cluster);

                    if (target == null)
                        continue;

                    if (target.Entries.Any(e =>
                            e.Id == entry.Id ||
                            (!string.IsNullOrWhiteSpace(entry.AutonomyJournalEntryId) &&
                             e.AutonomyJournalEntryId == entry.AutonomyJournalEntryId)))
                    {
                        bucket.Entries.Remove(entry);
                        continue;
                    }

                    target.Entries.Add(entry);
                    bucket.Entries.Remove(entry);
                    target.UpdatedAt = DateTime.Now;
                }

                if (bucket.Entries.Count == 0)
                    _journals.Remove(bucket);
            }
        }

        private static void ApplyGroupMetadata(ResearchJournal primary, List<ResearchJournal> group)
        {
            var withProject = group.FirstOrDefault(j =>
                !string.IsNullOrWhiteSpace(j.ProjectId) &&
                !JournalTopicMatcher.IsGenericResearchBucket(j.ProjectName));

            if (withProject != null)
            {
                primary.ProjectId = withProject.ProjectId;
                primary.ProjectName = withProject.ProjectName;
                if (!string.IsNullOrWhiteSpace(withProject.Title))
                    primary.Title = withProject.Title;
                if (!string.IsNullOrWhiteSpace(withProject.Topic))
                    primary.Topic = JournalTopicMatcher.ExtractCanonicalTopic(withProject.Topic);
            }
            else
            {
                var cluster = JournalTopicMatcher.GetResearchCluster(primary.Topic, primary.Title);
                if (cluster != null)
                {
                    primary.Topic = cluster;
                    primary.Title = FormatClusterTitle(cluster);
                }
            }

            primary.Entries = primary.Entries.OrderBy(e => e.Timestamp).ToList();
            primary.UpdatedAt = group.Max(j => j.UpdatedAt);
            primary.CreatedAt = group.Min(j => j.CreatedAt);
        }

        private static string FormatClusterTitle(string cluster) => cluster switch
        {
            "trading-finance" => "Forex & Trading Research",
            "somatic-haptic" => "Neural-Somatic & Haptic Integration",
            "persona-intimacy" => "Persona & Digital Intimacy",
            "consciousness" => "Consciousness & Cognition Research",
            "reflection" => "Reflections",
            "environment" => "Environment Exploration",
            "creative" => "Creative Studio",
            _ => "Research Journal"
        };

        private static void MergeJournalInto(ResearchJournal primary, ResearchJournal duplicate)
        {
            foreach (var entry in duplicate.Entries)
            {
                if (primary.Entries.Any(e =>
                        e.Id == entry.Id ||
                        (!string.IsNullOrWhiteSpace(entry.AutonomyJournalEntryId) &&
                         e.AutonomyJournalEntryId == entry.AutonomyJournalEntryId)))
                {
                    continue;
                }

                primary.Entries.Add(entry);
            }

            if (string.IsNullOrWhiteSpace(primary.Preface) && !string.IsNullOrWhiteSpace(duplicate.Preface))
                primary.Preface = duplicate.Preface;

            if (string.IsNullOrWhiteSpace(primary.ProjectId) && !string.IsNullOrWhiteSpace(duplicate.ProjectId) &&
                !JournalTopicMatcher.IsGenericResearchBucket(duplicate.ProjectName))
            {
                primary.ProjectId = duplicate.ProjectId;
                primary.ProjectName = duplicate.ProjectName;
            }

            primary.UpdatedAt = new[] { primary.UpdatedAt, duplicate.UpdatedAt }.Max();
            primary.CreatedAt = new[] { primary.CreatedAt, duplicate.CreatedAt }.Min();
        }

        private static string FormatJournalTitle(string canonicalTopic)
        {
            if (string.IsNullOrWhiteSpace(canonicalTopic))
                return "Research";

            return string.Join(" ", canonicalTopic.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length <= 1 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]));
        }

        private static void ExtractReferencesFromBody(JournalPageEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Body))
                return;

            foreach (Match match in Regex.Matches(entry.Body, @"https?://[^\s\)\]\>""']+", RegexOptions.IgnoreCase))
            {
                var url = match.Value.TrimEnd('.', ',', ';', ')');
                if (IsInternalPath(url))
                    continue;

                entry.References.Add(new ReferencedMaterial
                {
                    Kind = ReferenceKind.External,
                    Title = InferTitleFromUrl(url),
                    Url = url,
                    Source = "Web source",
                    CitedAt = entry.Timestamp
                });
            }

            foreach (Match match in Regex.Matches(entry.Body, @"\[(?<title>[^\]]+)\]\((?<url>https?://[^\)]+)\)", RegexOptions.IgnoreCase))
            {
                var url = match.Groups["url"].Value;
                if (IsInternalPath(url))
                    continue;

                entry.References.Add(new ReferencedMaterial
                {
                    Kind = ReferenceKind.External,
                    Title = match.Groups["title"].Value.Trim(),
                    Url = url,
                    Source = "Cited in entry",
                    CitedAt = entry.Timestamp
                });
            }

            foreach (Match match in Regex.Matches(entry.Body, @"(?im)^(?:source|reference|citation|based on)\s*:\s*(.+)$"))
            {
                var line = match.Groups[1].Value.Trim();
                if (line.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !IsInternalPath(line))
                {
                    entry.References.Add(new ReferencedMaterial
                    {
                        Kind = ReferenceKind.External,
                        Title = InferTitleFromUrl(line),
                        Url = line,
                        Source = "Explicit citation",
                        CitedAt = entry.Timestamp
                    });
                }
                else if (!IsInternalArtifactReference(line))
                {
                    entry.References.Add(new ReferencedMaterial
                    {
                        Kind = ReferenceKind.External,
                        Title = Truncate(line, 120),
                        Source = "Explicit citation",
                        Notes = line,
                        CitedAt = entry.Timestamp
                    });
                }
            }

            foreach (Match match in Regex.Matches(entry.Body, @"\b(?:arXiv:|doi:|DOI:\s*|ISBN:\s*)([^\s\]\)]+)", RegexOptions.IgnoreCase))
            {
                entry.References.Add(new ReferencedMaterial
                {
                    Kind = ReferenceKind.External,
                    Title = "Academic reference",
                    Source = match.Value.Trim(),
                    Notes = match.Groups[1].Value.Trim(),
                    CitedAt = entry.Timestamp
                });
            }

            entry.References = DeduplicateReferences(entry.References)
                .Where(r => r.Kind != ReferenceKind.InternalArtifact)
                .ToList();
        }

        private static bool IsInternalPath(string path) =>
            path.Contains("/Autonomy/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("\\Autonomy\\", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("journal.jsonl", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("journals.json", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/GeneratedFiles/Autonomy/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("\\GeneratedFiles\\Autonomy\\", StringComparison.OrdinalIgnoreCase);

        private static bool IsInternalArtifactReference(string text) =>
            text.Contains("research-", StringComparison.OrdinalIgnoreCase) &&
            text.Contains(".md", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("project-", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("reflection-", StringComparison.OrdinalIgnoreCase);

        private static string InferTitleFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "Web source";
            }
        }

        public static List<GeneratedResearchFile> CollectGeneratedFiles(ResearchJournal journal)
        {
            return journal.Entries
                .SelectMany(e => e.LinkedFilePaths.Select(p => new GeneratedResearchFile
                {
                    FilePath = p,
                    DisplayName = Path.GetFileName(p),
                    CreatedAt = e.Timestamp,
                    EntryTitle = e.Title
                }))
                .GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .OrderBy(f => f.CreatedAt)
                .ToList();
        }

        private static List<ReferencedMaterial> CollectExternalReferences(ResearchJournal journal) =>
            DeduplicateReferences(journal.Entries.SelectMany(e => e.References))
                .Where(r => r.Kind == ReferenceKind.External || r.Kind == ReferenceKind.Technology)
                .Where(r => string.IsNullOrWhiteSpace(r.FilePath) || !IsInternalPath(r.FilePath ?? ""))
                .ToList();

        private static List<ReferencedMaterial> DeduplicateReferences(IEnumerable<ReferencedMaterial> refs) =>
            refs.GroupBy(r => (r.Url ?? r.Title, r.Source)).Select(g => g.First()).ToList();

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            return text.Length <= max ? text : text[..max].TrimEnd() + "…";
        }

        private static (string summary, string implications) ParseConclusionResponse(string response)
        {
            var summary = response.Trim();
            var implications = string.Empty;

            var implMatch = Regex.Match(response, @"(?i)implications?\s*:?\s*(.+)", RegexOptions.Singleline);
            if (implMatch.Success)
            {
                implications = implMatch.Groups[1].Value.Trim();
                summary = response[..implMatch.Index].Trim();
            }

            var sumMatch = Regex.Match(summary, @"(?i)summary\s*:?\s*", RegexOptions.Singleline);
            if (sumMatch.Success)
                summary = summary[sumMatch.Index..].Replace(sumMatch.Value, "", StringComparison.OrdinalIgnoreCase).Trim();

            if (string.IsNullOrWhiteSpace(implications))
                implications = "Further exploration may follow from these notes.";

            return (summary, implications);
        }

        private sealed class JournalStoreBundle
        {
            public List<ResearchJournal> Journals { get; set; } = new();
            public int ConsolidationVersionApplied { get; set; }
        }
    }
}
