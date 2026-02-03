using System;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace SQLScripter.Services
{
    /// <summary>
    /// Logging service wrapper for log4net
    /// </summary>
    public interface ILoggerService
    {
        void Info(string server, string database, string message);
        void Error(string server, string database, string message, Exception exception);
        void WriteToLog(string server, string database, string level, Exception exception);
        void RegisterServer(string serverName, bool writeToConsole, string color);
        void LogEvent(string message, EventLogEntryType type);
    }

    public class LoggerService : ILoggerService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(LoggerService));
        private static readonly object _consoleLock = new object();
        
        private readonly bool _globalWriteToConsole;
        private readonly ConsoleColor _globalDefaultColor;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (bool writeToConsole, ConsoleColor color)> _serverOverrides = new(StringComparer.OrdinalIgnoreCase);

        public LoggerService(bool writeToConsole = true, string foregroundColor = "White")
        {
            _globalWriteToConsole = writeToConsole;
            if (!Enum.TryParse(foregroundColor, true, out _globalDefaultColor))
            {
                _globalDefaultColor = ConsoleColor.White;
            }
        }

        public void RegisterServer(string serverName, bool writeToConsole, string color)
        {
            if (string.IsNullOrEmpty(serverName)) return;

            if (!Enum.TryParse<ConsoleColor>(color, true, out var consoleColor))
            {
                consoleColor = _globalDefaultColor;
            }

            _serverOverrides[serverName] = (writeToConsole, consoleColor);
        }

        private (bool writeToConsole, ConsoleColor color) GetSettings(string server)
        {
            if (!string.IsNullOrEmpty(server) && _serverOverrides.TryGetValue(server, out var settings))
            {
                return settings;
            }
            return (_globalWriteToConsole, _globalDefaultColor);
        }

        public void Info(string server, string database, string message)
        {
            string formattedMessage = FormatMessage(server, database, message, "INFO");
            
            // Write to log file
            log.Info(formattedMessage);
            
            var settings = GetSettings(server);

            // Write to console
            if (settings.writeToConsole)
            {
                WriteToConsole(formattedMessage, settings.color);
            }
        }

        public void Error(string server, string database, string message, Exception exception)
        {
            string formattedMessage = FormatMessage(server, database, message, "ERROR");
            
            // Write to log file
            log.Error(formattedMessage, exception);
            
            var settings = GetSettings(server);

            // Write to console
            if (settings.writeToConsole)
            {
                WriteToConsole(formattedMessage, ConsoleColor.Red);
                if (exception != null)
                {
                    WriteToConsole($"  Exception: {exception.Message}", ConsoleColor.Red);
                }
            }
        }

        public void WriteToLog(string server, string database, string level, Exception exception)
        {
            string message = $"{exception.GetType().Name} - {exception.Message}";
            string formattedMessage = FormatMessage(server, database, message, level.ToUpper());
            
            var settings = GetSettings(server);

            switch (level.ToLower())
            {
                case "error":
                    log.Error(formattedMessage, exception);
                    if (settings.writeToConsole)
                    {
                        WriteToConsole(formattedMessage, ConsoleColor.Red);
                        if (exception != null)
                        {
                            WriteToConsole($"  Exception: {exception.Message}", ConsoleColor.Red);
                        }
                    }
                    break;
                case "warn":
                case "warning":
                    log.Warn(formattedMessage, exception);
                    if (settings.writeToConsole)
                    {
                        WriteToConsole(formattedMessage, ConsoleColor.Yellow);
                    }
                    break;
                case "info":
                    log.Info(formattedMessage, exception);
                    if (settings.writeToConsole)
                    {
                        WriteToConsole(formattedMessage, settings.color);
                    }
                    break;
                default:
                    log.Debug(formattedMessage, exception);
                    if (settings.writeToConsole)
                    {
                        WriteToConsole(formattedMessage, ConsoleColor.Gray);
                    }
                    break;
            }
        }

        private string FormatMessage(string server, string database, string message, string level)
        {
            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff");
            string levelPart = level.PadRight(6);

            if (string.IsNullOrEmpty(server) && string.IsNullOrEmpty(database))
            {
                return $"{ts}  {levelPart} {message}";
            }

            // Ensure minimum widths and vertical alignment
            int serverWidth = Math.Max(Program.SqlServerMaxNameLength, 15);
            int dbWidth = Math.Max(Program.SqlDatabaseMaxNameLength, 30);

            string s = (server ?? "").PadRight(serverWidth);
            
            if (string.IsNullOrEmpty(database))
            {
                // Align lifecycle messages with the MESSAGE column (the baseline)
                // Baseline is: 3 spaces + "Database: " (10) + dbWidth + 3 spaces separator
                int basePadding = 3 + 10 + dbWidth + 3;
                
                int offset = 0; 

                // Align lifecycle messages with the baseline column
                if (message.Contains("Connecting to server") || message.Contains("Server processing completed"))
                {
                    offset = 0;
                }

                string padding = new string(' ', basePadding + offset);
                return $"{ts}  {levelPart} Server: {s}{padding}{message}";
            }
            else
            {
                // Active processing line with Database name
                string d = database.PadRight(dbWidth);
                return $"{ts}  {levelPart} Server: {s}   Database: {d}   {message}";
            }
        }

        private void WriteToConsole(string message, ConsoleColor color)
        {
            lock (_consoleLock)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ForegroundColor = originalColor;
            }
        }

        [SupportedOSPlatform("windows")]
        public void LogEvent(string message, EventLogEntryType type)
        {
            const string source = "SQLScripter";
            const string logName = "Application";

            try
            {
                // Note: CreateEventSource requires Administrative privileges.
                // If it doesn't exist and we aren't Admin, this will fail.
                if (!EventLog.SourceExists(source))
                {
                    EventLog.CreateEventSource(source, logName);
                }

                EventLog.WriteEntry(source, message, type);
            }
            catch (Exception ex)
            {
                // Log the failure to the normal log so the user knows why it's missing
                log.Debug($"Failed to write to Windows Event Log. (Note: Creating a new Event Source '{source}' requires Administrative privileges). Error: {ex.Message}");
            }
        }
    }
}
