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
        /// Mark a log entry as read
        /// </summary>
        Task MarkAsReadAsync(string logId);

        /// <summary>
        /// Mark multiple log entries as read
        /// </summary>
        Task MarkMultipleAsReadAsync(IEnumerable<string> logIds);

        /// <summary>
        /// Mark all logs as read
        /// </summary>
        Task MarkAllAsReadAsync();

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
