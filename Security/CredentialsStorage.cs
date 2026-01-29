using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace SQLScripter.Security
{
    /// <summary>
    /// Secure credentials storage using Windows DPAPI encryption
    /// </summary>
    public class CredentialsStorage
    {
        private readonly string _filePath;

        public CredentialsStorage(string? filePath = null)
        {
            _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, "Configuration", "credentials.bin");
        }

        /// <summary>
        /// Load and decrypt credentials from encrypted file
        /// </summary>
        public List<Credential> LoadCredentials()
        {
            if (!File.Exists(_filePath))
                return new List<Credential>();

            try
            {
                var encryptedData = File.ReadAllBytes(_filePath);
#pragma warning disable CA1416 // Validate platform compatibility
                var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.LocalMachine);
#pragma warning restore CA1416 // Validate platform compatibility
                var json = Encoding.UTF8.GetString(decryptedData);
                return JsonSerializer.Deserialize<List<Credential>>(json) ?? new List<Credential>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load credentials from {_filePath}. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Encrypt and save credentials to file
        /// </summary>
        public void SaveCredentials(List<Credential> credentials)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true });
                var data = Encoding.UTF8.GetBytes(json);
#pragma warning disable CA1416 // Validate platform compatibility
                var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
#pragma warning restore CA1416 // Validate platform compatibility
                File.WriteAllBytes(_filePath, encryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save credentials to {_filePath}. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get credential for a specific server and authentication type
        /// </summary>
        public Credential? GetCredential(string serverName, AuthenticationType? authType = null)
        {
            var credentials = LoadCredentials();
            return credentials.Find(c => 
                c.Server.Equals(serverName, StringComparison.OrdinalIgnoreCase) && 
                (!authType.HasValue || c.AuthType == authType.Value));
        }

        /// <summary>
        /// Add or update credential for a server
        /// </summary>
        public void AddOrUpdateCredential(string serverName, string username, string password, AuthenticationType authType = AuthenticationType.Sql)
        {
            var credentials = LoadCredentials();
            var existing = credentials.Find(c => c.Server.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Username = username;
                existing.Password = password;
                existing.AuthType = authType;
            }
            else
            {
                credentials.Add(new Credential
                {
                    Server = serverName,
                    Username = username,
                    Password = password,
                    AuthType = authType
                });
            }

            SaveCredentials(credentials);
        }

        /// <summary>
        /// Remove credential for a server
        /// </summary>
        public bool RemoveCredential(string serverName)
        {
            var credentials = LoadCredentials();
            var removed = credentials.RemoveAll(c => c.Server.Equals(serverName, StringComparison.OrdinalIgnoreCase));
            
            if (removed > 0)
            {
                SaveCredentials(credentials);
                return true;
            }

            return false;
        }
    }
}
