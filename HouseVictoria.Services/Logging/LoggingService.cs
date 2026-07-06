using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Autonomy;

namespace HouseVictoria.Services.Logging
{
    /// <summary>
    /// Service for managing and reading logs from various sources
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly AppConfig _appConfig;
        private readonly IPersistenceService _persistenceService;
        private readonly Dictionary<string, LogCategory> _categories = new();
        private readonly Dictionary<string, LogEntry> _allEntries = new();
        private readonly HashSet<string> _readLogIds = new();
        private readonly HashSet<string> _archivedLogIds = new();
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly object _refreshLock = new object();
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private bool _isRefreshing = false;

        /// <inheritdoc />
        public bool IncludeArchived { get; set; } = false;

        public LoggingService(AppConfig appConfig, IPersistenceService persistenceService, IProjectManagementService? projectManagementService = null)
        {
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            // Fire-and-forget async initialization to avoid blocking constructor
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadReadStatusAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading read status during initialization: {ex.Message}");
                }
            });
        }

        public async Task<Dictionary<string, LogCategory>> GetLogCategoriesAsync()
        {
            // Always refresh if empty or if it's been more than 1 minute
            if (_categories.Count == 0 || (DateTime.Now - _lastRefresh).TotalMinutes > 1)
            {
                // Prevent concurrent refreshes
                lock (_refreshLock)
                {
                    if (_isRefreshing)
                    {
                        // Wait a bit and return current state if refresh is in progress
                        System.Diagnostics.Debug.WriteLine("LoggingService: Refresh already in progress, returning current state");
                        return new Dictionary<string, LogCategory>(_categories);
                    }
                    _isRefreshing = true;
                }

                try
                {
                    await RefreshLogsAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoggingService: GetLogCategoriesAsync refresh failed: {ex.Message}");
                }
                finally
                {
                    lock (_refreshLock)
                    {
                        _isRefreshing = false;
                    }
                }
            }

            // Return a copy to prevent external modification
            return new Dictionary<string, LogCategory>(_categories);
        }

        public async Task<List<LogEntry>> GetLogEntriesAsync(string category)
        {
            await RefreshLogsAsync();
            if (_categories.TryGetValue(category, out var cat))
            {
                return cat.Entries.OrderByDescending(e => e.Timestamp).ToList();
            }
            return new List<LogEntry>();
        }

        public Task<LogEntry?> GetLogEntryAsync(string logId)
        {
            _allEntries.TryGetValue(logId, out var entry);
            return Task.FromResult(entry);
        }

        public async Task MarkAsReadAsync(string logId)
        {
            await ArchiveAsync(logId).ConfigureAwait(false);
        }

        public async Task ArchiveAsync(string logId)
        {
            _readLogIds.Add(logId);
            _archivedLogIds.Add(logId);
            if (_allEntries.TryGetValue(logId, out var entry))
            {
                entry.IsRead = true;
                entry.IsArchived = true;
            }
            await SaveInboxStatusAsync().ConfigureAwait(false);
            RebuildInboxCategories();
        }

        public async Task UnarchiveAsync(string logId)
        {
            _archivedLogIds.Remove(logId);
            _readLogIds.Remove(logId);
            if (_allEntries.TryGetValue(logId, out var entry))
            {
                entry.IsArchived = false;
                entry.IsRead = false;
            }
            await SaveInboxStatusAsync().ConfigureAwait(false);
            RebuildInboxCategories();
        }

        public async Task MarkMultipleAsReadAsync(IEnumerable<string> logIds)
        {
            foreach (var logId in logIds)
            {
                _readLogIds.Add(logId);
                _archivedLogIds.Add(logId);
                if (_allEntries.TryGetValue(logId, out var entry))
                {
                    entry.IsRead = true;
                    entry.IsArchived = true;
                }
            }
            await SaveInboxStatusAsync().ConfigureAwait(false);
            RebuildInboxCategories();
        }

        public async Task MarkAllAsReadAsync()
        {
            foreach (var entry in _allEntries.Values)
            {
                entry.IsRead = true;
                entry.IsArchived = true;
                _readLogIds.Add(entry.Id);
                _archivedLogIds.Add(entry.Id);
            }
            await SaveInboxStatusAsync().ConfigureAwait(false);
            RebuildInboxCategories();
        }

        public async Task ExportLogsAsync(string filePath, LogExportOptions? options = null)
        {
            await RefreshLogsAsync();
            options ??= new LogExportOptions();

            var entriesToExport = _allEntries.Values.Where(e =>
                (options.Categories == null || options.Categories.Contains(e.Category)) &&
                (options.MinSeverity == null || e.Severity >= options.MinSeverity) &&
                (!options.StartDate.HasValue || e.Timestamp >= options.StartDate.Value) &&
                (!options.EndDate.HasValue || e.Timestamp <= options.EndDate.Value) &&
                (options.IncludeRead || !e.IsRead) &&
                (options.IncludeUnread || e.IsRead)
            ).OrderBy(e => e.Timestamp).ToList();

            switch (options.Format)
            {
                case LogExportFormat.Text:
                    await ExportAsTextAsync(filePath, entriesToExport).ConfigureAwait(false);
                    break;
                case LogExportFormat.Json:
                    await ExportAsJsonAsync(filePath, entriesToExport).ConfigureAwait(false);
                    break;
                case LogExportFormat.Csv:
                    await ExportAsCsvAsync(filePath, entriesToExport).ConfigureAwait(false);
                    break;
            }
        }

        public async Task RefreshLogsAsync()
        {
            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"LoggingService: Starting RefreshLogsAsync at {DateTime.Now}");

                    _categories.Clear();
                    _allEntries.Clear();

                    // GLD is a review inbox — not a dump of Serilog/sidecar/activity logs.
                    await LoadAutonomyReviewInboxAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"LoggingService: After LoadAutonomyReviewInboxAsync - {_allEntries.Count} entries");

                    await LoadUserRequestedGeneratedFilesAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"LoggingService: After LoadUserRequestedGeneratedFilesAsync - {_allEntries.Count} entries");

                    foreach (var entry in _allEntries.Values)
                    {
                        entry.IsRead = _readLogIds.Contains(entry.Id);
                        entry.IsArchived = _archivedLogIds.Contains(entry.Id);
                    }

                    RebuildInboxCategories();

                    _lastRefresh = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine($"LoggingService: Refresh complete - {_categories.Count} categories, {_allEntries.Count} entries");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoggingService: Error in RefreshLogsAsync: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"LoggingService: Stack trace: {ex.StackTrace}");
                    throw;
                }
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        public Task<Dictionary<string, LogCategory>> PeekLogCategoriesAsync()
        {
            lock (_refreshLock)
            {
                return Task.FromResult(new Dictionary<string, LogCategory>(_categories));
            }
        }

        public async Task<int> GetUnreadCountAsync()
        {
            await RefreshLogsAsync();
            return _allEntries.Values.Count(e => !e.IsArchived && !e.IsRead);
        }

        private string TruncateTitle(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }

        private void StoreLogEntry(LogEntry entry)
        {
            _allEntries[entry.Id] = entry;
        }

        private void RebuildInboxCategories()
        {
            _categories.Clear();
            foreach (var entry in _allEntries.Values.Where(e => IncludeArchived || !e.IsArchived))
                AddLogEntryToCategories(entry);

            UpdateCategoryCounts();
        }

        private void AddLogEntryToCategories(LogEntry entry)
        {
            if (!_categories.TryGetValue(entry.Category, out var category))
            {
                category = new LogCategory
                {
                    Name = entry.Category,
                    DisplayName = GetCategoryDisplayName(entry.Category)
                };
                _categories[entry.Category] = category;
            }

            // Add to subcategory
            if (!category.SubCategories.TryGetValue(entry.SubCategory, out var subCategory))
            {
                subCategory = new LogCategory
                {
                    Name = entry.SubCategory,
                    DisplayName = entry.SubCategory
                };
                category.SubCategories[entry.SubCategory] = subCategory;
            }

            subCategory.Entries.Add(entry);
        }

        private async Task LoadAutonomyReviewInboxAsync()
        {
            var autonomyDir = _appConfig.AutonomyDataPath;
            if (string.IsNullOrWhiteSpace(autonomyDir) || !Directory.Exists(autonomyDir))
                return;

            await LoadAutonomyJournalAsync(autonomyDir).ConfigureAwait(false);
            await LoadAutonomyArtInboxAsync(autonomyDir).ConfigureAwait(false);
        }

        private async Task LoadUserRequestedGeneratedFilesAsync()
        {
            var generatedRoot = Path.Combine(_appConfig.MediaPath, "GeneratedFiles");
            if (!Directory.Exists(generatedRoot))
                return;

            try
            {
                foreach (var filePath in Directory.GetFiles(generatedRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (filePath.Contains($"{Path.DirectorySeparatorChar}Autonomy{Path.DirectorySeparatorChar}",
                            StringComparison.OrdinalIgnoreCase) ||
                        filePath.Contains("/Autonomy/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!IsUserRequestedGeneratedFile(filePath))
                        continue;

                    var fileInfo = new FileInfo(filePath);
                    var id = AutonomyFileEntryId(filePath);
                    if (_allEntries.ContainsKey(id))
                        continue;

                    var entry = new LogEntry
                    {
                        Id = id,
                        Category = "Generated",
                        SubCategory = Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "Files",
                        Title = Path.GetFileName(filePath),
                        Content = $"Generated file: {filePath}",
                        Summary = Path.GetFileName(filePath),
                        Timestamp = fileInfo.CreationTime,
                        Severity = LogSeverity.Info,
                        Source = "File Generation",
                        Tags = new List<string> { "Generated", "Artifact" },
                        LinkedFilePaths = new List<string> { filePath }
                    };
                    StoreLogEntry(entry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error loading user-generated files: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private static bool IsUserRequestedGeneratedFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" or ".mp4" or ".webm" or ".wav" or ".mp3"
                or ".pdf"
                or ".py" or ".cs" or ".js" or ".ts" or ".tsx" or ".jsx" or ".mq4" or ".mq5" or ".cpp" or ".h" or ".java" or ".go" or ".rs"
                or ".md" or ".txt" or ".json" or ".yaml" or ".yml" or ".xml" or ".csv";
        }

        private const int MaxReviewInboxJournalEntries = 400;
        private const int MaxInboxContentChars = 6000;

        private async Task LoadAutonomyJournalAsync(string autonomyDir)
        {
            var journalPath = Path.Combine(autonomyDir, "journal.jsonl");
            if (!File.Exists(journalPath))
                return;

            try
            {
                var entries = await AutonomyJournalFile.ReadTailEntriesAsync(
                    journalPath,
                    MaxReviewInboxJournalEntries).ConfigureAwait(false);

                foreach (var journal in entries)
                {
                    if (journal == null)
                        continue;

                    if (ShouldSkipInboxJournalEntry(journal))
                        continue;

                    var body = journal.Body ?? journal.Summary ?? string.Empty;
                    var linked = (journal.LinkedFilePaths ?? new List<string>())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var inboxContent = body.Length > MaxInboxContentChars
                        ? body[..MaxInboxContentChars] + "\n\n… [truncated in inbox — open linked file or journal for full text]"
                        : body;

                    var entry = new LogEntry
                    {
                        Id = $"autonomy_journal_{journal.Id}",
                        Category = "Autonomy",
                        SubCategory = journal.Activity.ToString(),
                        Title = $"{journal.Activity}: {TruncateTitle(journal.Summary ?? string.Empty, 80)}",
                        Content = inboxContent,
                        Summary = TruncateTitle(body, 200),
                        Timestamp = journal.Timestamp,
                        Severity = LogSeverity.Info,
                        Source = "Autonomy Journal",
                        Tags = BuildAutonomyTags(journal),
                        LinkedFilePaths = linked,
                        FilePath = journalPath
                    };
                    StoreLogEntry(entry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error loading autonomy journal: {ex.Message}");
            }
        }

        private static bool ShouldSkipInboxJournalEntry(AutonomyJournalEntry journal)
        {
            // Routine autonomy failures are logged for audit but clutter the human review inbox.
            if (journal.Activity is AutonomyActivityKind.RunBacktest or AutonomyActivityKind.ExecuteTrade)
            {
                var summary = journal.Summary ?? string.Empty;
                if (summary.Contains("failed", StringComparison.OrdinalIgnoreCase)
                    || summary.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private Task LoadAutonomyArtInboxAsync(string autonomyDir)
        {
            var artDir = Path.Combine(autonomyDir, "Art");
            if (!Directory.Exists(artDir))
                return Task.CompletedTask;

            try
            {
                foreach (var filePath in Directory.GetFiles(artDir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif"))
                        continue;

                    var journalId = $"autonomy_journal_art_{ComputeStableHash(filePath)}";
                    if (_allEntries.ContainsKey(journalId) ||
                        _allEntries.Values.Any(e => (e.LinkedFilePaths ?? new List<string>()).Contains(filePath, StringComparer.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);
                    var id = AutonomyFileEntryId(filePath);
                    if (_allEntries.ContainsKey(id))
                        continue;

                    var entry = new LogEntry
                    {
                        Id = id,
                        Category = "Autonomy",
                        SubCategory = "Art",
                        Title = $"Art: {fileInfo.Name}",
                        Content = $"Autonomous art output: {filePath}",
                        Summary = fileInfo.Name,
                        Timestamp = fileInfo.CreationTime,
                        Severity = LogSeverity.Info,
                        Source = "Autonomy Art",
                        Tags = new List<string> { "Autonomy", "Art" },
                        LinkedFilePaths = new List<string> { filePath }
                    };
                    StoreLogEntry(entry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error loading autonomy art inbox: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private static List<string> BuildAutonomyTags(AutonomyJournalEntry journal)
        {
            var tags = new List<string> { "Autonomy", journal.Activity.ToString() };
            if (!string.IsNullOrWhiteSpace(journal.ProjectName))
                tags.Add(journal.ProjectName);
            return tags;
        }

        private static string AutonomyFileEntryId(string filePath) =>
            $"autonomy_file_{ComputeStableHash(Path.GetFullPath(filePath).ToLowerInvariant())}";

        private static string ComputeStableHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..12];
        }

        private string GetCategoryDisplayName(string category)
        {
            return category switch
            {
                "System" => "System Logs",
                "AI" => "AI Logs",
                "Project" => "Project Logs",
                "Autonomy" => "Autonomy (Victoria)",
                "Generated" => "Generated Files",
                "Sidecar" => "Sidecar Logs",
                _ => category
            };
        }

        private void UpdateCategoryCounts()
        {
            foreach (var category in _categories.Values)
            {
                UpdateCategoryUnreadCount(category);
            }
        }

        private void UpdateCategoryUnreadCount(LogCategory category)
        {
            category.UnreadCount = category.Entries.Count(e => !e.IsRead);
            foreach (var subCategory in category.SubCategories.Values)
            {
                UpdateCategoryUnreadCount(subCategory);
                category.UnreadCount += subCategory.UnreadCount;
            }
        }

        private async Task LoadReadStatusAsync()
        {
            try
            {
                var readStatus = await _persistenceService.GetAsync<HashSet<string>>("ReadLogIds").ConfigureAwait(false);
                if (readStatus != null)
                {
                    _readLogIds.Clear();
                    foreach (var id in readStatus)
                        _readLogIds.Add(id);
                }

                var archived = await _persistenceService.GetAsync<HashSet<string>>("ArchivedLogIds").ConfigureAwait(false);
                if (archived != null)
                {
                    _archivedLogIds.Clear();
                    foreach (var id in archived)
                        _archivedLogIds.Add(id);
                }

                // Legacy: treat previously-read items as archived in the review inbox.
                foreach (var id in _readLogIds)
                    _archivedLogIds.Add(id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading read status: {ex.Message}");
            }
        }

        private async Task SaveInboxStatusAsync()
        {
            try
            {
                await _persistenceService.SetAsync("ReadLogIds", _readLogIds).ConfigureAwait(false);
                await _persistenceService.SetAsync("ArchivedLogIds", _archivedLogIds).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving inbox status: {ex.Message}");
            }
        }

        private async Task ExportAsTextAsync(string filePath, List<LogEntry> entries)
        {
            using var writer = new StreamWriter(filePath);
            await writer.WriteLineAsync($"Log Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"Total Entries: {entries.Count}");
            await writer.WriteLineAsync(new string('=', 80));
            await writer.WriteLineAsync();

            foreach (var entry in entries)
            {
                await writer.WriteLineAsync($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.Severity} - {entry.Category}/{entry.SubCategory}");
                await writer.WriteLineAsync($"Source: {entry.Source}");
                await writer.WriteLineAsync($"Title: {entry.Title}");
                await writer.WriteLineAsync($"Tags: {string.Join(", ", entry.Tags)}");
                await writer.WriteLineAsync($"Read: {(entry.IsRead ? "Yes" : "No")}");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync(entry.Content);
                await writer.WriteLineAsync(new string('-', 80));
                await writer.WriteLineAsync();
            }
        }

        private async Task ExportAsJsonAsync(string filePath, List<LogEntry> entries)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task ExportAsCsvAsync(string filePath, List<LogEntry> entries)
        {
            using var writer = new StreamWriter(filePath);
            await writer.WriteLineAsync("Timestamp,Severity,Category,SubCategory,Source,Title,IsRead,Tags,Content");

            foreach (var entry in entries)
            {
                var tags = string.Join(";", entry.Tags);
                var content = entry.Content.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
                await writer.WriteLineAsync(
                    $"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\"," +
                    $"\"{entry.Severity}\"," +
                    $"\"{entry.Category}\"," +
                    $"\"{entry.SubCategory}\"," +
                    $"\"{entry.Source}\"," +
                    $"\"{entry.Title}\"," +
                    $"\"{(entry.IsRead ? "Yes" : "No")}\"," +
                    $"\"{tags}\"," +
                    $"\"{content}\"");
            }
        }
    }
}
