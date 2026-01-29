namespace SQLScripter.Models
{
    /// <summary>
    /// Application settings loaded from appsettings.json
    /// </summary>
    public class AppSettings
    {
        public string OutputFolder { get; set; } = "c:\\sqlscripter_out";
        public bool ScriptOneFilePerObjectType { get; set; }
        public bool ZipFolder { get; set; }
        public string ZipPassword { get; set; } = string.Empty;
        public bool DeleteOutputFolderAfterZip { get; set; }
        public int DaysToKeepFilesInOutputFolder { get; set; }
        
        /// <summary>
        /// Maximum number of concurrent threads for processing servers.
        /// Default: 25. Range: 1-100.
        /// Higher values = faster processing but more CPU/memory usage.
        /// </summary>
        public int MaxConcurrentThreads { get; set; } = 25;
    }
}
