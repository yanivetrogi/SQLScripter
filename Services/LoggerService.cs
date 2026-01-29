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
            int serverWidth = Math.Max(Program.SqlServerMaxNameLength, 15);
            int dbWidth = Math.Max(Program.SqlDatabaseMaxNameLength, 15);

            string s = (server ?? "").PadRight(serverWidth);
            string d = (database ?? "").PadRight(dbWidth);

            return $"{s} {d} {message}";
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
