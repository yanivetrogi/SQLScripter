using System;

namespace SQLScripter.Models
{
    /// <summary>
    /// Configuration for a specific SQL Server to be processed.
    /// Simplified model: Authentication is resolved automatically via the Credential Manager.
    /// </summary>
    public class ServerSettings
    {
        public ServerSettings() { }

        public string SQLServer { get; set; } = string.Empty;
        public string Databases { get; set; } = "all";
        public string ObjectTypes { get; set; } = "all";

        // Runtime status
        public bool connnectionOK { get; set; }
        
        public string ServerDisplayName => SQLServer.Replace("]", "").Replace("[", "");

        public bool? WriteToConsole { get; set; }
        public string? ConsoleForeGroundColour { get; set; }
    }   
}
