using System.IO;

namespace HouseVictoria.Core.Utils
{
    /// <summary>
    /// Helper methods for logging
    /// </summary>
    public static class LoggingHelper
    {
        public static string FormatException(Exception ex)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR: {ex.GetType().Name}: {ex.Message}\n" +
                   $"Stack Trace: {ex.StackTrace}\n" +
                   $"{(ex.InnerException != null ? $"Inner Exception: {FormatException(ex.InnerException)}" : "")}";
        }

        public static string FormatInfo(string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO: {message}";
        }

        public static string FormatWarning(string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] WARNING: {message}";
        }

        public static string FormatDebug(string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] DEBUG: {message}";
        }

        /// <summary>
        /// Writes exception to log file, ensuring directory exists
        /// </summary>
        public static void WriteExceptionToLog(Exception exception, string logFileName = "UnhandledExceptions.log", string? logSubdirectory = null)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var logDir = string.IsNullOrWhiteSpace(logSubdirectory)
                    ? Path.Combine(baseDir, "Logs")
                    : Path.Combine(baseDir, "Logs", logSubdirectory);

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var logPath = Path.Combine(logDir, logFileName);
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception.GetType().Name}\n" +
                              $"Message: {exception.Message}\n" +
                              $"Stack: {exception.StackTrace}\n\n";
                              
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Silently fail if logging fails to prevent cascading errors
            }
        }

        /// <summary>
        /// Writes message to startup log file
        /// </summary>
        public static void WriteToStartupLog(string message)
        {
            try
            {
                var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }
}
