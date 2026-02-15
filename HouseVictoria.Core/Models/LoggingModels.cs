using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Represents a log entry
    /// </summary>
    public class LogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogSeverity Severity { get; set; } = LogSeverity.Info;
        public string Source { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public bool IsRead { get; set; } = false;
        public string? FilePath { get; set; } // Path to log file if from file system
        public int? LineNumber { get; set; } // Line number in log file if applicable
    }

    /// <summary>
    /// Represents a log category with its entries
    /// </summary>
    public class LogCategory
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<LogEntry> Entries { get; set; } = new();
        public Dictionary<string, LogCategory> SubCategories { get; set; } = new();
        public int UnreadCount { get; set; }
        public int TotalCount => Entries.Count + SubCategories.Values.Sum(sc => sc.TotalCount);
    }

    /// <summary>
    /// Log severity levels
    /// </summary>
    public enum LogSeverity
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Options for exporting logs
    /// </summary>
    public class LogExportOptions
    {
        public List<string>? Categories { get; set; }
        public LogSeverity? MinSeverity { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IncludeRead { get; set; } = true;
        public bool IncludeUnread { get; set; } = true;
        public LogExportFormat Format { get; set; } = LogExportFormat.Text;
    }

    /// <summary>
    /// Log export formats
    /// </summary>
    public enum LogExportFormat
    {
        Text,
        Json,
        Csv
    }
}
