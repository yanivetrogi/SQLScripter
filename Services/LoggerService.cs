using System;

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
    }

    public class LoggerService : ILoggerService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(LoggerService));
        private static readonly object _consoleLock = new object();

        public void Info(string server, string database, string message)
        {
            string formattedMessage = FormatMessage(server, database, message);
            
            // Write to log file
            log.Info(formattedMessage);
            
            // Write to console
            WriteToConsole(formattedMessage, ConsoleColor.White);
        }

        public void Error(string server, string database, string message, Exception exception)
        {
            string formattedMessage = FormatMessage(server, database, message);
            
            // Write to log file
            log.Error(formattedMessage, exception);
            
            // Write to console
            WriteToConsole(formattedMessage, ConsoleColor.Red);
            if (exception != null)
            {
                WriteToConsole($"  Exception: {exception.Message}", ConsoleColor.Red);
            }
        }

        public void WriteToLog(string server, string database, string level, Exception exception)
        {
            string message = $"{exception.GetType().Name} - {exception.Message}";
            string formattedMessage = FormatMessage(server, database, message);
            
            switch (level.ToLower())
            {
                case "error":
                    log.Error(formattedMessage, exception);
                    WriteToConsole(formattedMessage, ConsoleColor.Red);
                    if (exception != null)
                    {
                        WriteToConsole($"  Exception: {exception.Message}", ConsoleColor.Red);
                    }
                    break;
                case "warn":
                case "warning":
                    log.Warn(formattedMessage, exception);
                    WriteToConsole(formattedMessage, ConsoleColor.Yellow);
                    break;
                case "info":
                    log.Info(formattedMessage, exception);
                    WriteToConsole(formattedMessage, ConsoleColor.White);
                    break;
                default:
                    log.Debug(formattedMessage, exception);
                    WriteToConsole(formattedMessage, ConsoleColor.Gray);
                    break;
            }
        }

        private string FormatMessage(string server, string database, string message)
        {
            if (string.IsNullOrEmpty(server) && string.IsNullOrEmpty(database))
            {
                return message;
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

                // #2 "Connecting to server" needs to be 1 space back of the baseline.
                if (message.Contains("Connecting to server"))
                {
                    offset = -1;
                }
                // #3 "Server processing completed" needs to be 1 space back from the baseline.
                else if (message.Contains("Server processing completed"))
                {
                    offset = -1;
                }

                string padding = new string(' ', basePadding + offset);
                return $"Server: {s}{padding}{message}";
            }
            else
            {
                // Active processing line with Database name
                string d = database.PadRight(dbWidth);
                return $"Server: {s}   Database: {d}   {message}";
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
    }
}
