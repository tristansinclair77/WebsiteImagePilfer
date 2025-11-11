using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WebsiteImagePilfer.Services
{
    /// <summary>
    /// Centralized logging service for application-wide diagnostics and monitoring.
    /// Provides structured logging with multiple severity levels and automatic file output.
    /// </summary>
    public static class Logger
    {
        public enum LogLevel 
        { 
          Debug, 
            Info, 
       Warning, 
      Error 
      }

        private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
        private static readonly object _lockObject = new object();

    static Logger()
 {
       try
 {
    if (!Directory.Exists(LogDirectory))
    Directory.CreateDirectory(LogDirectory);
            }
   catch
            {
 // If we can't create log directory, continue without file logging
    }
        }

/// <summary>
      /// Logs a message with the specified severity level.
        /// </summary>
        /// <param name="level">The severity level of the log entry</param>
   /// <param name="message">The message to log</param>
        /// <param name="source">The source of the log entry (auto-populated with caller name)</param>
        /// <param name="ex">Optional exception to include in the log</param>
public static void Log(LogLevel level, string message, string? source = null, Exception? ex = null)
        {
        try
       {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
          var logEntry = $"[{timestamp}] [{level}] [{source ?? "App"}] {message}";

          if (ex != null)
           logEntry += $"\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}";

         // Write to debug output (visible in debugger)
      System.Diagnostics.Debug.WriteLine(logEntry);

       // Write to file (async, fire-and-forget)
         Task.Run(() => WriteToFileAsync(logEntry));
       }
      catch
         {
   // Don't let logging errors crash the application
   }
     }

        /// <summary>
        /// Logs a debug-level message. Used for detailed diagnostic information.
        /// </summary>
        public static void Debug(string message, [CallerMemberName] string? source = null)
        => Log(LogLevel.Debug, message, source);

        /// <summary>
        /// Logs an informational message. Used for general application flow tracking.
        /// </summary>
        public static void Info(string message, [CallerMemberName] string? source = null)
            => Log(LogLevel.Info, message, source);

        /// <summary>
     /// Logs a warning message. Used for recoverable issues or unexpected conditions.
 /// </summary>
        public static void Warning(string message, [CallerMemberName] string? source = null)
          => Log(LogLevel.Warning, message, source);

        /// <summary>
   /// Logs an error message with optional exception details.
        /// </summary>
        public static void Error(string message, Exception? ex = null, [CallerMemberName] string? source = null)
 => Log(LogLevel.Error, message, source, ex);

        /// <summary>
        /// Writes a log entry to the daily log file (thread-safe).
        /// </summary>
      private static async Task WriteToFileAsync(string logEntry)
        {
      try
 {
     await Task.Run(() =>
     {
              lock (_lockObject)
         {
     File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
         }
     });
    }
     catch
       {
                // Silent failure - don't crash if file write fails
         }
    }

        /// <summary>
        /// Gets the path to the current log file.
        /// </summary>
        public static string GetLogFilePath() => LogFilePath;

 /// <summary>
        /// Gets the directory containing log files.
      /// </summary>
   public static string GetLogDirectory() => LogDirectory;
    }
}
