using System;
using System.Data;
using Microsoft.Data.SqlClient;
using SQLScripter.Security;
using SQLScripter.Models;

namespace SQLScripter.Services
{
    /// <summary>
    /// Service for managing SQL Server connections with automatic authentication resolution.
    /// </summary>
    public interface IConnectionService
    {
        string GetConnectionString(ServerSettings serverSettings, string applicationName);
        bool TestConnection(string connectionString, string serverName, string database);
    }

    public class ConnectionService : IConnectionService
    {
        private readonly ILoggerService _logger;
        private readonly CredentialsStorage _credentialsStorage;

        public ConnectionService(ILoggerService logger)
        {
            _logger = logger;
            _credentialsStorage = new CredentialsStorage();
        }

        public string GetConnectionString(ServerSettings serverSettings, string applicationName)
        {
            try
            {
                string serverName = serverSettings.SQLServer;
                string connectionString;

                // Resolution Logic: Check Credential Manager first
                var credential = _credentialsStorage.GetCredential(serverName);

                if (credential != null && credential.AuthType == AuthenticationType.Sql)
                {
                    // Case 1: Stored SQL Credential
                    _logger.Info(serverName, "", "Authenticating via stored SQL credentials");
                    connectionString = $"data source={serverName}; initial catalog=master; Application Name={applicationName}; User ID={credential.Username};Password={credential.Password}; persist security info=False; TrustServerCertificate=True;";
                }
                else
                {
                    // Case 2: No stored SQL credential -> default to Integrated Security
                    // Note: If a Windows credential exists, impersonation is handled separately in OrchestrationService
                    connectionString = $"data source={serverName}; initial catalog=master; Application Name={applicationName}; integrated security=SSPI; persist security info=False; TrustServerCertificate=True;";
                    
                    if (credential == null)
                        _logger.Info(serverName, "", "Authenticating via current Windows identity (Integrated Security)");
                }

                // Check connection
                serverSettings.connnectionOK = TestConnection(connectionString, serverName, "master");
                return connectionString;
            }
            catch (Exception ex)
            {
                _logger.WriteToLog("", "", "Error resolving connection string", ex);
                serverSettings.connnectionOK = false;
                return string.Empty;
            }
        }

        public bool TestConnection(string connectionString, string serverName, string database)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    connection.Close();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, database, "Connection Test Failed", ex);
                return false;
            }
        }
    }
}
