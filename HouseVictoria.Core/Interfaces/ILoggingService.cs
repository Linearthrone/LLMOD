using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for logging service operations
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Get all log categories with their log entries
        /// </summary>
        Task<Dictionary<string, LogCategory>> GetLogCategoriesAsync();

        /// <summary>
        /// Get log entries for a specific category
        /// </summary>
        Task<List<LogEntry>> GetLogEntriesAsync(string category);

        /// <summary>
        /// Get a specific log entry by ID
        /// </summary>
        Task<LogEntry?> GetLogEntryAsync(string logId);

        /// <summary>
        /// Mark a log entry as read and move it out of the review inbox (archive).
        /// </summary>
        Task MarkAsReadAsync(string logId);

        /// <summary>
        /// Mark multiple log entries as read and archive them.
        /// </summary>
        Task MarkMultipleAsReadAsync(IEnumerable<string> logIds);

        /// <summary>
        /// Mark all inbox entries as read and archive them.
        /// </summary>
        Task MarkAllAsReadAsync();

        /// <summary>
        /// Archive a log entry without requiring it to be opened first.
        /// </summary>
        Task ArchiveAsync(string logId);

        /// <summary>
        /// Restore a previously archived entry back into the review inbox (marks it unread).
        /// </summary>
        Task UnarchiveAsync(string logId);

        /// <summary>
        /// When true, archived entries are included in the category tree so they can be reviewed/restored.
        /// </summary>
        bool IncludeArchived { get; set; }

        /// <summary>
        /// Export logs to a file
        /// </summary>
        Task ExportLogsAsync(string filePath, LogExportOptions? options = null);

        /// <summary>
        /// Refresh logs from all sources
        /// </summary>
        Task RefreshLogsAsync();

        /// <summary>
        /// Get unread log count
        /// </summary>
        Task<int> GetUnreadCountAsync();
    }
}
