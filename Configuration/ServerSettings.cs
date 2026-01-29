using System;

namespace SQLScripter
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

        public bool WriteToConsole { get; set; }
                       
        private string _ConsoleForeGroundColour = "White";
        public string ConsoleForeGroundColour 
        {             
            get
            {
                if (Enum.TryParse<ConsoleColor>(_ConsoleForeGroundColour, true, out _))
                    return _ConsoleForeGroundColour;
                
                return "White";
            }
            set { _ConsoleForeGroundColour = value; }
        }       
    }   
}
