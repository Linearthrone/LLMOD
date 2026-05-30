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

            var normalized = NormalizeTopic(topic);
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j =>
                    NormalizeTopic(j.Topic) == normalized ||
                    NormalizeTopic(j.Title) == normalized);
                return journal == null ? null : CloneJournal(journal);
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
                await PersistAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

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
            var existing = await FindByTopicAsync(topic).ConfigureAwait(false);
            if (existing != null)
                return existing;

            return await CreateJournalAsync(
                topic.Trim(),
                preface,
                topic: topic.Trim(),
                projectId: projectId,
                projectName: projectName).ConfigureAwait(false);
        }

        public async Task AddPageEntryAsync(string journalId, JournalPageEntry entry)
        {
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
                RaiseUpdated(journal);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task AddReferenceAsync(string journalId, ReferencedMaterial reference, string? entryId = null)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId)
                              ?? throw new InvalidOperationException($"Journal not found: {journalId}");

                if (!string.IsNullOrWhiteSpace(entryId))
                {
                    var entry = journal.Entries.FirstOrDefault(e => e.Id == entryId);
                    if (entry != null)
                    {
                        entry.References.Add(reference);
                    }
                }
                else
                {
                    var lastResearch = journal.Entries.LastOrDefault(e => e.Kind == JournalEntryKind.Research);
                    if (lastResearch != null)
                        lastResearch.References.Add(reference);
                }

                journal.UpdatedAt = DateTime.Now;
                await PersistAsync().ConfigureAwait(false);
                RaiseUpdated(journal);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ResearchJournal> RecordAutonomyActivityAsync(
            AutonomyJournalEntry autonomyEntry,
            string? topicTitle = null,
            string? prefaceReason = null)
        {
            var (title, reason) = ParseSummaryParts(autonomyEntry.Summary);
            var topic = topicTitle ?? title ?? autonomyEntry.ProjectName ?? "Personal reflections";
            var preface = prefaceReason ?? reason ?? autonomyEntry.Summary;

            ResearchJournal journal;
            if (autonomyEntry.Activity == AutonomyActivityKind.WriteResearch)
            {
                journal = await FindOrCreateForTopicAsync(topic, preface).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(autonomyEntry.ProjectId))
            {
                journal = await FindOrCreateForProjectAsync(
                    autonomyEntry.ProjectId,
                    autonomyEntry.ProjectName ?? topic,
                    preface).ConfigureAwait(false);
            }
            else
            {
                journal = await FindOrCreateForTopicAsync(
                    topic,
                    preface,
                    autonomyEntry.ProjectId,
                    autonomyEntry.ProjectName).ConfigureAwait(false);
            }

            var kind = MapActivityKind(autonomyEntry.Activity);
            var pageEntry = new JournalPageEntry
            {
                Timestamp = autonomyEntry.Timestamp,
                Kind = kind,
                Title = title ?? autonomyEntry.Summary,
                Body = autonomyEntry.Body ?? autonomyEntry.Summary,
                LinkedFilePaths = autonomyEntry.LinkedFilePaths.ToList(),
                AutonomyJournalEntryId = autonomyEntry.Id,
                SourceActivity = autonomyEntry.Activity
            };

            ExtractReferencesFromBody(pageEntry);
            await AddPageEntryAsync(journal.Id, pageEntry).ConfigureAwait(false);

            var updated = await GetJournalAsync(journal.Id).ConfigureAwait(false);
            return updated ?? journal;
        }

        public async Task ConcludeJournalAsync(
            string journalId,
            string summary,
            string implications,
            IEnumerable<ReferencedMaterial>? citations = null)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var journal = _journals.FirstOrDefault(j => j.Id == journalId)
                              ?? throw new InvalidOperationException($"Journal not found: {journalId}");

                journal.Status = JournalStatus.Concluded;
                journal.ConcludedAt = DateTime.Now;
                journal.ConclusionSummary = summary.Trim();
                journal.ConclusionImplications = implications.Trim();
                journal.ConclusionCitations = citations?.ToList() ?? CollectAllReferences(journal);
                journal.UpdatedAt = DateTime.Now;

                journal.Entries.Add(new JournalPageEntry
                {
                    Kind = JournalEntryKind.Conclusion,
                    Title = "Conclusion",
                    Body = $"{summary.Trim()}\n\nImplications:\n{implications.Trim()}",
                    Timestamp = DateTime.Now
                });

                await PersistAsync().ConfigureAwait(false);
                RaiseUpdated(journal);
            }
            finally
            {
                _lock.Release();
            }
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

            var refsText = string.Join("\n", CollectAllReferences(journal)
                .Select(r => $"- {r.Title}: {r.Source ?? r.Url ?? r.FilePath ?? r.Notes}"));

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

                Cite referenced materials naturally in the summary where relevant.
                """;

            var response = await _aiService.SendMessageAsync(contact, prompt, null).ConfigureAwait(false);
            var (summary, implications) = ParseConclusionResponse(response);

            var citations = CollectAllReferences(journal);
            await ConcludeJournalAsync(journalId, summary, implications, citations).ConfigureAwait(false);
            return await GetJournalAsync(journalId).ConfigureAwait(false);
        }

        public async Task SyncFromAutonomyLogAsync()
        {
            if (!File.Exists(_autonomyJournalPath))
                return;

            var lines = await File.ReadAllLinesAsync(_autonomyJournalPath).ConfigureAwait(false);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<AutonomyJournalEntry>(line, JsonOptions);
                    if (entry != null)
                        await RecordAutonomyActivityAsync(entry).ConfigureAwait(false);
                }
                catch
                {
                    // skip malformed lines
                }
            }
        }

        private async Task EnsureSyncedAsync()
        {
            if (_syncAttempted)
                return;

            _syncAttempted = true;
            if (_journals.Count == 0 && File.Exists(_autonomyJournalPath))
                await SyncFromAutonomyLogAsync().ConfigureAwait(false);
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
            }
            catch
            {
                // start fresh on corruption
            }
        }

        private async Task PersistAsync()
        {
            var bundle = new JournalStoreBundle { Journals = _journals };
            var json = JsonSerializer.Serialize(bundle, JsonOptions);
            await File.WriteAllTextAsync(_storePath, json).ConfigureAwait(false);
        }

        private void RaiseUpdated(ResearchJournal journal)
        {
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

        private static string NormalizeTopic(string topic) =>
            Regex.Replace(topic.Trim().ToLowerInvariant(), @"\s+", " ");

        private static void ExtractReferencesFromBody(JournalPageEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Body))
                return;

            foreach (Match match in Regex.Matches(entry.Body, @"https?://[^\s\)\]\>]+", RegexOptions.IgnoreCase))
            {
                entry.References.Add(new ReferencedMaterial
                {
                    Title = "Web reference",
                    Url = match.Value.TrimEnd('.', ',', ';'),
                    Source = "Extracted from entry",
                    CitedAt = entry.Timestamp
                });
            }

            foreach (var path in entry.LinkedFilePaths)
            {
                entry.References.Add(new ReferencedMaterial
                {
                    Title = Path.GetFileName(path),
                    FilePath = path,
                    Source = "Generated artifact",
                    CitedAt = entry.Timestamp
                });
            }
        }

        private static List<ReferencedMaterial> CollectAllReferences(ResearchJournal journal)
        {
            return journal.Entries
                .SelectMany(e => e.References)
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToList();
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
        }
    }
}
