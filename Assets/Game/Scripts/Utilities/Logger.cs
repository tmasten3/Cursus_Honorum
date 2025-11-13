using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Centralized logging utility supporting in-editor output, persistent file storage,
    /// and a rolling buffer for in-game debug overlays.
    /// </summary>
    public static class Logger
    {
        public enum LogLevel
        {
            Log,
            Info,
            Warning,
            Error
        }

        public static LogLevel MinimumLevel = LogLevel.Info;

        public readonly struct LogEntry
        {
            public readonly DateTime Timestamp;
            public readonly LogLevel Level;
            public readonly string Category;
            public readonly string Message;

            public LogEntry(DateTime timestamp, LogLevel level, string category, string message)
            {
                Timestamp = timestamp;
                Level = level;
                Category = category;
                Message = message;
            }
        }

        private const int MaxRecentEntries = 200;

        private static readonly Queue<LogEntry> recentEntries = new();
        private static readonly object syncRoot = new();
        private static readonly string logFilePath;
        private static readonly bool fileLoggingAvailable;

        static Logger()
        {
            try
            {
                string directory = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                    logFilePath = Path.Combine(directory, "cursus_honorum.log");
                    fileLoggingAvailable = true;
                    AppendToFile($"--- Log session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Logger] Failed to initialize persistent log file: {ex.Message}");
                fileLoggingAvailable = false;
            }
        }

        // Basic log  neutral white
        public static void Log(string category, string message) =>
            Write(LogLevel.Log, category, message);

        // Success / progression
        public static void Info(string category, string message) =>
            Write(LogLevel.Info, category, message);

        // Warnings
        public static void Warn(string category, string message) =>
            Write(LogLevel.Warning, category, message);

        // Errors
        public static void Error(string category, string message) =>
            Write(LogLevel.Error, category, message);

        private static void Write(LogLevel level, string category, string message)
        {
            if (level < MinimumLevel)
                return;

            if (string.IsNullOrWhiteSpace(category))
                category = "General";

            if (message == null)
                message = string.Empty;

            var timestamp = DateTime.Now;
            var entry = new LogEntry(timestamp, level, category, message);

            lock (syncRoot)
            {
                recentEntries.Enqueue(entry);
                while (recentEntries.Count > MaxRecentEntries)
                    recentEntries.Dequeue();
            }

            string formatted = FormatForConsole(level, category, message);
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(formatted);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formatted);
                    break;
                default:
                    Debug.Log(formatted);
                    break;
            }

            if (fileLoggingAvailable)
                AppendToFile(FormatForFile(entry));
        }

        private static string FormatForConsole(LogLevel level, string category, string message)
        {
            string color = level switch
            {
                LogLevel.Info => "cyan",
                LogLevel.Warning => "yellow",
                LogLevel.Error => "red",
                _ => "white"
            };
            return $"<color={color}>[{category}]</color> {message}";
        }

        private static string FormatForFile(LogEntry entry)
        {
            var builder = new StringBuilder();
            builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append(' ');
            builder.Append('[');
            builder.Append(entry.Level);
            builder.Append("] [");
            builder.Append(entry.Category);
            builder.Append("] ");
            builder.Append(entry.Message);
            return builder.ToString();
        }

        private static void AppendToFile(string line)
        {
            if (!fileLoggingAvailable || string.IsNullOrEmpty(logFilePath))
                return;

            try
            {
                File.AppendAllText(logFilePath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Logger] Failed to write to log file: {ex.Message}");
            }
        }

        public static void WriteToFile(string fileName, IEnumerable<string> lines)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "Game/Logs", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Logger] Failed to write log file '{fileName}': {ex}");
            }
        }

        /// <summary>
        /// Returns a snapshot of the recent log entries in chronological order.
        /// </summary>
        public static IReadOnlyList<LogEntry> GetRecentEntries()
        {
            lock (syncRoot)
            {
                return recentEntries.ToArray();
            }
        }
    }
}
