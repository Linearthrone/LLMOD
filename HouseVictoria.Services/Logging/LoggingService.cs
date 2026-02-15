using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Logging
{
    /// <summary>
    /// Service for managing and reading logs from various sources
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly AppConfig _appConfig;
        private readonly IPersistenceService _persistenceService;
        private readonly IProjectManagementService? _projectManagementService;
        private readonly Dictionary<string, LogCategory> _categories = new();
        private readonly Dictionary<string, LogEntry> _allEntries = new();
        private readonly HashSet<string> _readLogIds = new();
        private DateTime _lastRefresh = DateTime.MinValue;
        private readonly object _refreshLock = new object();
        private bool _isRefreshing = false;

        public LoggingService(AppConfig appConfig, IPersistenceService persistenceService, IProjectManagementService? projectManagementService = null)
        {
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _projectManagementService = projectManagementService;
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
                    await RefreshLogsAsync();
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

        public async Task<LogEntry?> GetLogEntryAsync(string logId)
        {
            await RefreshLogsAsync();
            _allEntries.TryGetValue(logId, out var entry);
            return entry;
        }

        public async Task MarkAsReadAsync(string logId)
        {
            _readLogIds.Add(logId);
            if (_allEntries.TryGetValue(logId, out var entry))
            {
                entry.IsRead = true;
            }
            await SaveReadStatusAsync();
        }

        public async Task MarkMultipleAsReadAsync(IEnumerable<string> logIds)
        {
            foreach (var logId in logIds)
            {
                _readLogIds.Add(logId);
                if (_allEntries.TryGetValue(logId, out var entry))
                {
                    entry.IsRead = true;
                }
            }
            await SaveReadStatusAsync();
        }

        public async Task MarkAllAsReadAsync()
        {
            foreach (var entry in _allEntries.Values)
            {
                entry.IsRead = true;
                _readLogIds.Add(entry.Id);
            }
            await SaveReadStatusAsync();
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
            try
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Starting RefreshLogsAsync at {DateTime.Now}");
                
                _categories.Clear();
                _allEntries.Clear();

                // Load Serilog files
                await LoadSerilogFilesAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"LoggingService: After LoadSerilogFilesAsync - {_allEntries.Count} entries loaded");

                // Load project logs
                await LoadProjectLogsAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"LoggingService: After LoadProjectLogsAsync - {_allEntries.Count} total entries");

                // Apply read status
                foreach (var entry in _allEntries.Values)
                {
                    entry.IsRead = _readLogIds.Contains(entry.Id);
                }

                // Update category unread counts
                UpdateCategoryCounts();

                _lastRefresh = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"LoggingService: Refresh complete - {_categories.Count} categories, {_allEntries.Count} entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error in RefreshLogsAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoggingService: Stack trace: {ex.StackTrace}");
                throw; // Re-throw to let caller know something went wrong
            }
        }

        public async Task<int> GetUnreadCountAsync()
        {
            await RefreshLogsAsync();
            return _allEntries.Values.Count(e => !e.IsRead);
        }

        private async Task LoadSerilogFilesAsync()
        {
            try
            {
                var logsPath = _appConfig.LogsPath;
                System.Diagnostics.Debug.WriteLine($"LoggingService: Loading Serilog files from: {logsPath}");
                
                if (!Directory.Exists(logsPath))
                {
                    System.Diagnostics.Debug.WriteLine($"LoggingService: Logs directory does not exist, creating: {logsPath}");
                    Directory.CreateDirectory(logsPath);
                    return;
                }

                var logFiles = Directory.GetFiles(logsPath, "HouseVictoria-*.log", SearchOption.TopDirectoryOnly);
                System.Diagnostics.Debug.WriteLine($"LoggingService: Found {logFiles.Length} log files");
                
                if (logFiles.Length == 0)
                {
                    // Also check for any .log files
                    var allLogFiles = Directory.GetFiles(logsPath, "*.log", SearchOption.TopDirectoryOnly);
                    System.Diagnostics.Debug.WriteLine($"LoggingService: Found {allLogFiles.Length} total .log files");
                }
                
                foreach (var logFile in logFiles)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"LoggingService: Reading log file: {logFile}");
                        var lines = await File.ReadAllLinesAsync(logFile).ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine($"LoggingService: File {logFile} has {lines.Length} lines");
                        
                        int parsedCount = 0;
                        foreach (var line in lines)
                        {
                            var entry = ParseSerilogLine(line, logFile);
                            if (entry != null)
                            {
                                AddLogEntry(entry);
                                parsedCount++;
                            }
                        }
                        System.Diagnostics.Debug.WriteLine($"LoggingService: Parsed {parsedCount} entries from {logFile}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"LoggingService: Error reading log file {logFile}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"LoggingService: Stack trace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error in LoadSerilogFilesAsync: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoggingService: Stack trace: {ex.StackTrace}");
            }
        }

        private LogEntry? ParseSerilogLine(string line, string filePath)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                // Serilog format: [2026-01-04 14:32:15.123 INF] Message...
                // Also handles: [2026-01-04 14:32:15.123 WRN] [Source] Message...
                // Also handles JSON format: {"Timestamp":"2026-01-04T14:32:15.123Z","Level":"Information","Message":"..."}
                var pattern = @"\[(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?)\s+(\w+)\]\s*(?:\[([^\]]+)\]\s*)?(.+)";
                var match = Regex.Match(line, pattern);

                if (!match.Success)
                {
                    // Try JSON format
                    if (line.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            var jsonDoc = System.Text.Json.JsonDocument.Parse(line);
                            var root = jsonDoc.RootElement;
                            
                            var timestampStr = root.TryGetProperty("Timestamp", out var tsProp) ? tsProp.GetString() : null;
                            var levelStr = root.TryGetProperty("Level", out var levelProp) ? levelProp.GetString() : "Information";
                            var jsonMessage = root.TryGetProperty("Message", out var msgProp) ? msgProp.GetString() : line;
                            var jsonSource = root.TryGetProperty("SourceContext", out var srcProp) ? srcProp.GetString() : "Application";

                            if (DateTime.TryParse(timestampStr, out var timestamp))
                            {
                                var jsonSeverity = ParseSeverity(levelStr ?? "Information");
                                var jsonCategory = DetermineCategory(jsonSource ?? "Application", jsonSeverity);
                                var jsonSubCategory = DetermineSubCategory(jsonSource ?? "Application");

                                return new LogEntry
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Category = jsonCategory,
                                    SubCategory = jsonSubCategory,
                                    Title = TruncateTitle(jsonMessage ?? line, 100),
                                    Content = jsonMessage ?? line,
                                    Summary = TruncateTitle(jsonMessage ?? line, 200),
                                    Timestamp = timestamp,
                                    Severity = jsonSeverity,
                                    Source = jsonSource ?? "Application",
                                    FilePath = filePath,
                                    Tags = ExtractTags(jsonMessage ?? line)
                                };
                            }
                        }
                        catch
                        {
                            // Not valid JSON, continue with fallback
                        }
                    }
                    
                    // Fallback: create entry from entire line if it doesn't match standard format
                    return new LogEntry
                    {
                        Id = Guid.NewGuid().ToString(),
                        Category = "System",
                        SubCategory = "General",
                        Title = TruncateTitle(line, 100),
                        Content = line,
                        Summary = TruncateTitle(line, 200),
                        Timestamp = DateTime.Now,
                        Severity = LogSeverity.Info,
                        Source = "Log File",
                        FilePath = filePath,
                        Tags = ExtractTags(line)
                    };
                }

                var timestampStr2 = match.Groups[1].Value;
                var severityStr = match.Groups[2].Value;
                var source = match.Groups[3].Success ? match.Groups[3].Value : "Application";
                var message = match.Groups[4].Value.Trim();

                if (!DateTime.TryParse(timestampStr2, out var timestamp2))
                    timestamp2 = DateTime.Now;

                var severity = ParseSeverity(severityStr);
                var category = DetermineCategory(source, severity);
                var subCategory = DetermineSubCategory(source);

                var entry = new LogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Category = category,
                    SubCategory = subCategory,
                    Title = TruncateTitle(message, 100),
                    Content = message,
                    Summary = TruncateTitle(message, 200),
                    Timestamp = timestamp2,
                    Severity = severity,
                    Source = source,
                    FilePath = filePath,
                    Tags = ExtractTags(message)
                };

                return entry;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error parsing log line: {ex.Message}");
                return null;
            }
        }

        private LogSeverity ParseSeverity(string severity)
        {
            return severity.ToUpperInvariant() switch
            {
                "DBG" or "DEBUG" => LogSeverity.Debug,
                "INF" or "INFO" => LogSeverity.Info,
                "WRN" or "WARN" or "WARNING" => LogSeverity.Warning,
                "ERR" or "ERROR" => LogSeverity.Error,
                "FTL" or "FATAL" or "CRIT" or "CRITICAL" => LogSeverity.Critical,
                _ => LogSeverity.Info
            };
        }

        private string DetermineCategory(string source, LogSeverity severity)
        {
            if (severity == LogSeverity.Error || severity == LogSeverity.Critical)
                return "System";
            
            if (source.Contains("AI", StringComparison.OrdinalIgnoreCase) || 
                source.Contains("Model", StringComparison.OrdinalIgnoreCase))
                return "AI";
            
            if (source.Contains("Project", StringComparison.OrdinalIgnoreCase))
                return "Project";
            
            return "System";
        }

        private string DetermineSubCategory(string source)
        {
            if (source.Contains("Application", StringComparison.OrdinalIgnoreCase))
                return "Application";
            
            if (source.Contains("Error", StringComparison.OrdinalIgnoreCase))
                return "Errors";
            
            if (source.Contains("Performance", StringComparison.OrdinalIgnoreCase) || 
                source.Contains("Perf", StringComparison.OrdinalIgnoreCase))
                return "Performance";
            
            if (source.Contains("Model", StringComparison.OrdinalIgnoreCase))
                return "Model Interactions";
            
            if (source.Contains("Training", StringComparison.OrdinalIgnoreCase))
                return "Training";
            
            return "General";
        }

        private List<string> ExtractTags(string message)
        {
            var tags = new List<string>();
            
            if (message.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("EXCEPTION", StringComparison.OrdinalIgnoreCase))
                tags.Add("Error");
            
            if (message.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
                tags.Add("Warning");
            
            if (message.Contains("AI", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Model", StringComparison.OrdinalIgnoreCase))
                tags.Add("AI");
            
            if (message.Contains("Project", StringComparison.OrdinalIgnoreCase))
                tags.Add("Project");
            
            return tags;
        }

        private string TruncateTitle(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            
            if (text.Length <= maxLength)
                return text;
            
            return text.Substring(0, maxLength - 3) + "...";
        }

        private async Task LoadProjectLogsAsync()
        {
            if (_projectManagementService == null)
            {
                System.Diagnostics.Debug.WriteLine("LoggingService: ProjectManagementService is null, skipping project logs");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("LoggingService: Loading project logs...");
                var projects = await _projectManagementService.GetAllProjectsAsync();
                System.Diagnostics.Debug.WriteLine($"LoggingService: Found {projects.Count} projects");
                
                int projectLogCount = 0;
                foreach (var project in projects)
                {
                    try
                    {
                        var projectLogs = await _projectManagementService.GetProjectLogsAsync(project.Id);
                        foreach (var projectLog in projectLogs)
                        {
                            var entry = new LogEntry
                            {
                                Id = $"project_{project.Id}_{projectLog.Id}",
                                Category = "Project",
                                SubCategory = project.Name,
                                Title = $"{project.Name}: {TruncateTitle(projectLog.Action, 80)}",
                                Content = projectLog.Action,
                                Summary = projectLog.Action,
                                Timestamp = projectLog.Timestamp,
                                Severity = LogSeverity.Info,
                                Source = "Project Management",
                                Tags = new List<string> { "Project", project.Name }
                            };
                            AddLogEntry(entry);
                            projectLogCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"LoggingService: Error loading logs for project {project.Id}: {ex.Message}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"LoggingService: Loaded {projectLogCount} project log entries");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggingService: Error loading project logs: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoggingService: Stack trace: {ex.StackTrace}");
            }
        }

        private void AddLogEntry(LogEntry entry)
        {
            _allEntries[entry.Id] = entry;

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

        private string GetCategoryDisplayName(string category)
        {
            return category switch
            {
                "System" => "System Logs",
                "AI" => "AI Logs",
                "Project" => "Project Logs",
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
                    {
                        _readLogIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading read status: {ex.Message}");
            }
        }

        private async Task SaveReadStatusAsync()
        {
            try
            {
                await _persistenceService.SetAsync("ReadLogIds", _readLogIds).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving read status: {ex.Message}");
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
