using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using Microsoft.Data.SqlClient;
using System.Security.Principal;
using SQLScripter.Models;

namespace SQLScripter.Services
{
    /// <summary>
    /// Main orchestration service that coordinates all scripting operations
    /// </summary>
    public interface IOrchestrationService
    {
        Task ProcessServersAsync(List<ServerSettings> servers, AppSettings appSettings, string applicationName);
    }

    public class OrchestrationService : IOrchestrationService
    {
        private readonly ILoggerService _logger;
        private readonly IConnectionService _connectionService;
        private readonly IScriptingService _scriptingService;
        private readonly IFileManagementService _fileManagementService;
        private readonly Security.CredentialsStorage _credentialsStorage;

        public OrchestrationService(
            ILoggerService logger,
            IConnectionService connectionService,
            IScriptingService scriptingService,
            IFileManagementService fileManagementService)
        {
            _logger = logger;
            _connectionService = connectionService;
            _scriptingService = scriptingService;
            _fileManagementService = fileManagementService;
            _credentialsStorage = new Security.CredentialsStorage();
        }

        public async Task ProcessServersAsync(List<ServerSettings> servers, AppSettings appSettings, string applicationName)
        {
            try
            {
                // Validate for duplicate servers
                var duplicateServers = ValidateAndDetectDuplicates(servers);
                
                // Get unique servers only
                var uniqueServers = GetUniqueServers(servers);

                // Set max server and database name lengths for logging alignment
                foreach (var server in uniqueServers)
                {
                    if (server.SQLServer.Length > Program.SqlServerMaxNameLength)
                    {
                        Program.SqlServerMaxNameLength = server.SQLServer.Length;
                    }

                    if (!string.IsNullOrEmpty(server.Databases) && server.Databases.ToLower() != "all")
                    {
                        var dbNames = server.Databases.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var dbName in dbNames)
                        {
                            if (dbName.Trim().Length > Program.SqlDatabaseMaxNameLength)
                            {
                                Program.SqlDatabaseMaxNameLength = dbName.Trim().Length;
                            }
                        }
                    }
                }
                
                // Get thread count from configuration with validation
                int maxThreads = appSettings.MaxConcurrentThreads;
                
                // Validate thread count (min: 1, max: 100)
                if (maxThreads < 1)
                {
                    _logger.Info("", "", $"Invalid MaxConcurrentThreads ({maxThreads}). Using default: 25");
                    maxThreads = 25;
                }
                else if (maxThreads > 100)
                {
                    _logger.Info("", "", $"MaxConcurrentThreads ({maxThreads}) exceeds maximum. Using 100");
                    maxThreads = 100;
                }

                _logger.Info("", "", $"Processing {uniqueServers.Count} unique server(s) with {maxThreads} concurrent threads...");

                // Use modern TPL with Parallel.ForEachAsync for efficient parallel processing
                var options = new ParallelOptions 
                { 
                    MaxDegreeOfParallelism = maxThreads 
                };

                await Parallel.ForEachAsync(uniqueServers, options, async (serverSettings, cancellationToken) =>
                {
                    // Get connection string once (this also tests the connection)
                    string connectionString = _connectionService.GetConnectionString(serverSettings, applicationName);

                    if (!serverSettings.connnectionOK)
                    {
                        _logger.Info(serverSettings.SQLServer, "", "Skipping - connection not OK");
                        return;
                    }

                    // Process server on thread pool
                    await Task.Run(() => ProcessServer(serverSettings, appSettings, applicationName, connectionString), cancellationToken);
                });

                _logger.Info("", "", "All servers processed successfully");

                // Cleanup old objects in the output folder
                if (appSettings.DaysToKeepFilesInOutputFolder > 0)
                {
                    _logger.Info("", "", $"Cleaning up files older than {appSettings.DaysToKeepFilesInOutputFolder} days in: {appSettings.OutputFolder}...");
                    _fileManagementService.CleanupOldFiles(appSettings.OutputFolder, appSettings.DaysToKeepFilesInOutputFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog("", "", "Error", ex);
            }
        }

        /// <summary>
        /// Validates server list and detects duplicates
        /// </summary>
        private Dictionary<string, int> ValidateAndDetectDuplicates(List<ServerSettings> servers)
        {
            var serverCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Count occurrences of each server
            foreach (var server in servers)
            {
                string serverName = server.SQLServer?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(serverName))
                {
                    _logger.Info("", "", "WARNING: Found server entry with empty/null name - skipping");
                    continue;
                }

                if (serverCounts.ContainsKey(serverName))
                {
                    serverCounts[serverName]++;
                }
                else
                {
                    serverCounts[serverName] = 1;
                }
            }

            // Identify and log duplicates
            foreach (var kvp in serverCounts)
            {
                if (kvp.Value > 1)
                {
                    duplicates[kvp.Key] = kvp.Value;
                    _logger.Info("", "", $"WARNING: Server '{kvp.Key}' is defined {kvp.Value} times in configuration - will process only once");
                }
            }

            if (duplicates.Count > 0)
            {
                _logger.Info("", "", $"Found {duplicates.Count} duplicate server(s) in configuration");
            }

            return duplicates;
        }

        /// <summary>
        /// Returns unique servers from the list (case-insensitive comparison)
        /// </summary>
        private List<ServerSettings> GetUniqueServers(List<ServerSettings> servers)
        {
            var uniqueServers = new List<ServerSettings>();
            var seenServers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var server in servers)
            {
                string serverName = server.SQLServer?.Trim() ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(serverName))
                {
                    continue; // Skip empty server names
                }

                if (!seenServers.Contains(serverName))
                {
                    seenServers.Add(serverName);
                    uniqueServers.Add(server);
                }
            }

            return uniqueServers;
        }

        private void ProcessServer(ServerSettings serverSettings, AppSettings appSettings, string applicationName, string connectionString)
        {
            string serverName = serverSettings.SQLServer;

            try
            {
                // Resolution Logic: Impersonate if a Windows credential is found for this server
                var credential = _credentialsStorage.GetCredential(serverName, Security.AuthenticationType.Windows);
                if (credential != null)
                {
                    using (var handle = Security.WindowsImpersonator.Logon(credential.Username, credential.Password))
                    {
                        if (handle != null && !handle.IsInvalid)
                        {
                            _logger.Info(serverName, "", $"Impersonating Windows user: {credential.Username}");
                            WindowsIdentity.RunImpersonated(handle, () =>
                            {
                                ProcessServerInternal(serverSettings, appSettings, applicationName, connectionString);
                            });
                            return;
                        }
                        else
                        {
                            _logger.Info(serverName, "", $"WARNING: Failed to impersonate Windows user: {credential.Username}. Continuing as current user.");
                        }
                    }
                }

                // Default path (no impersonation)
                ProcessServerInternal(serverSettings, appSettings, applicationName, connectionString);
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, "", "Error", ex);
            }
        }

        private void ProcessServerInternal(ServerSettings serverSettings, AppSettings appSettings, string applicationName, string connectionString)
        {
            string serverName = serverSettings.SQLServer;
            
            try
            {
                _logger.Info(serverName, "", "Connecting to server...");

                // Connect to server using SMO
                var connection = new ServerConnection(new SqlConnection(connectionString));
                var server = new Server(connection);

                // Create output folder for this server with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string serverPath = Path.Combine(appSettings.OutputFolder, $"{FixServerName(serverName)}_{timestamp}");
                Directory.CreateDirectory(serverPath);

                // Get databases to script
                string[] databasesToScript = GetDatabasesToScript(serverSettings.Databases, server);

                // Update max database name length dynamically for stable alignment
                foreach (string dbName in databasesToScript)
                {
                    if (dbName.Length > Program.SqlDatabaseMaxNameLength)
                    {
                        Program.SqlDatabaseMaxNameLength = dbName.Length;
                    }
                }

                // Script server-level objects once per server in their own master/msdb folders
                string[] serverObjectTypes = GetObjectTypesToScript(serverSettings.ObjectTypes);
                _scriptingService.ScriptServer(serverName, server, serverObjectTypes, serverPath, connectionString);

                // Process each database
                foreach (string databaseName in databasesToScript)
                {
                    ProcessDatabase(server, databaseName, serverSettings, appSettings, serverPath, connectionString);
                }

                // ZIP the output if configured
                if (appSettings.ZipFolder)
                {
                    string zipFile = $"{serverPath}.zip";
                    _logger.Info(serverName, "", $"Creating ZIP file: {zipFile}");
                    
                    bool zipSuccess = _fileManagementService.ZipFolder(serverPath, zipFile, appSettings.ZipPassword);
                    
                    if (zipSuccess && appSettings.DeleteOutputFolderAfterZip)
                    {
                        _fileManagementService.DeleteFolder(serverPath);
                    }
                }

                _logger.Info(serverName, "", "Server processing completed");
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, "", "Error", ex);
            }
        }

        private void ProcessDatabase(Server server, string databaseName, ServerSettings serverSettings, 
            AppSettings appSettings, string serverPath, string connectionString)
        {
            try
            {
                _logger.Info(serverSettings.SQLServer, databaseName, "Processing database...");

                var database = server.Databases[databaseName];
                if (database == null)
                {
                    _logger.Error(serverSettings.SQLServer, databaseName, "Database not found", 
                        new Exception($"Database {databaseName} does not exist"));
                    return;
                }

                // Create database output folder
                string databasePath = Path.Combine(serverPath, FixDatabaseName(databaseName));
                Directory.CreateDirectory(databasePath);

                // Get object types to script
                string[] objectTypes = GetObjectTypesToScript(serverSettings.ObjectTypes);

                // Script the database
                _scriptingService.ScriptDatabase(
                    serverSettings.SQLServer,
                    databaseName,
                    server,
                    database,
                    objectTypes,
                    databasePath,
                    scriptServerLevelObjects: false,
                    connectionString);

                _logger.Info(serverSettings.SQLServer, databaseName, "Database processing completed");
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverSettings.SQLServer, databaseName, "Error", ex);
            }
        }

        private string[] GetDatabasesToScript(string databasesConfig, Server server)
        {
            if (string.IsNullOrWhiteSpace(databasesConfig) || databasesConfig.Trim().ToUpper() == "ALL")
            {
                // Get all user databases
                var databases = new List<string>();
                foreach (Database db in server.Databases)
                {
                    if (!db.IsSystemObject && db.Name != "tempdb")
                    {
                        databases.Add(db.Name);
                    }
                }
                return databases.ToArray();
            }
            else
            {
                // Parse comma or semicolon-separated list
                return databasesConfig.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(db => db.Trim())
                                      .ToArray();
            }
        }

        private string[] GetObjectTypesToScript(string objectTypesConfig)
        {
            if (string.IsNullOrWhiteSpace(objectTypesConfig) || objectTypesConfig.Trim().ToUpper() == "ALL")
            {
                return new[] { "ALL" };
            }
            else
            {
                // Parse comma or semicolon-separated list
                return objectTypesConfig.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(ot => ot.Trim().ToUpper())
                                      .ToArray();
            }
        }

        private string FixServerName(string serverName)
        {
            if (string.IsNullOrEmpty(serverName))
                return serverName;

            return serverName.Replace("\\", "_").Replace("/", "_").Replace(":", "_");
        }

        private string FixDatabaseName(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
                return databaseName;

            return databaseName.Replace("[", "").Replace("]", "");
        }
    }
}
