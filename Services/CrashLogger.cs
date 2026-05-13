namespace IntelligentPersonalHealthOptimization.Services;

/// <summary>
/// Persistent crash/error logger that writes to a file on device.
/// Survives app restarts so errors can be reviewed after a crash.
/// </summary>
public static class CrashLogger
{
    private const string LogFileName = "crash_log.txt";
    private const int MaxLogSizeBytes = 512 * 1024; // 512 KB max — rotates when exceeded

    private static string LogFilePath =>
        Path.Combine(FileSystem.AppDataDirectory, LogFileName);

    /// <summary>
    /// Log an error with context about where it happened.
    /// </summary>
    public static void Log(string source, Exception ex)
    {
        try
        {
            var entry = FormatEntry(source, ex.ToString());
            AppendToLog(entry);
        }
        catch
        {
            // Never let the logger itself crash the app
        }
    }

    /// <summary>
    /// Log an error message without an exception object.
    /// </summary>
    public static void Log(string source, string message)
    {
        try
        {
            var entry = FormatEntry(source, message);
            AppendToLog(entry);
        }
        catch
        {
            // Never let the logger itself crash the app
        }
    }

    /// <summary>
    /// Log a fatal/unhandled exception (app is about to crash).
    /// </summary>
    public static void LogFatal(string source, object exceptionObject)
    {
        try
        {
            var entry = FormatEntry($"FATAL - {source}", exceptionObject?.ToString() ?? "Unknown error");
            AppendToLog(entry);
        }
        catch
        {
            // Last resort — nothing we can do
        }
    }

    /// <summary>
    /// Read the full crash log contents.
    /// </summary>
    public static string ReadLog()
    {
        try
        {
            return File.Exists(LogFilePath) ? File.ReadAllText(LogFilePath) : "No crash log entries.";
        }
        catch (Exception ex)
        {
            return $"Error reading crash log: {ex.Message}";
        }
    }

    /// <summary>
    /// Get the last N entries from the log.
    /// </summary>
    public static string ReadLastEntries(int count = 20)
    {
        try
        {
            if (!File.Exists(LogFilePath)) return "No crash log entries.";

            var lines = File.ReadAllLines(LogFilePath);
            // Each entry starts with "=== " separator line
            var entries = new List<string>();
            var currentEntry = new List<string>();

            foreach (var line in lines)
            {
                if (line.StartsWith("=== ") && currentEntry.Count > 0)
                {
                    entries.Add(string.Join(Environment.NewLine, currentEntry));
                    currentEntry.Clear();
                }
                currentEntry.Add(line);
            }
            if (currentEntry.Count > 0)
                entries.Add(string.Join(Environment.NewLine, currentEntry));

            var lastEntries = entries.Skip(Math.Max(0, entries.Count - count)).ToList();
            return string.Join(Environment.NewLine + Environment.NewLine, lastEntries);
        }
        catch (Exception ex)
        {
            return $"Error reading crash log: {ex.Message}";
        }
    }

    /// <summary>
    /// Clear the crash log.
    /// </summary>
    public static void ClearLog()
    {
        try
        {
            if (File.Exists(LogFilePath))
                File.Delete(LogFilePath);
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Get the log file path (for sharing/exporting).
    /// </summary>
    public static string GetLogFilePath() => LogFilePath;

    private static string FormatEntry(string source, string details)
    {
        return $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===" + Environment.NewLine +
               $"Source: {source}" + Environment.NewLine +
               $"Details: {details}" + Environment.NewLine;
    }

    private static void AppendToLog(string entry)
    {
        // Rotate if log is too large
        if (File.Exists(LogFilePath))
        {
            var info = new FileInfo(LogFilePath);
            if (info.Length > MaxLogSizeBytes)
            {
                // Keep the last half of the file
                var content = File.ReadAllText(LogFilePath);
                var halfPoint = content.Length / 2;
                var nextEntry = content.IndexOf("=== ", halfPoint, StringComparison.Ordinal);
                if (nextEntry > 0)
                    content = content[nextEntry..];
                File.WriteAllText(LogFilePath, content);
            }
        }

        File.AppendAllText(LogFilePath, entry + Environment.NewLine);

        // Also write to debug output for IDE
        System.Diagnostics.Debug.WriteLine(entry);
    }
}
