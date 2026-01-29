using System;
using System.IO;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Smo.Agent;
using Microsoft.SqlServer.Management.Common;
using SQLScripter.Services;

namespace SQLScripter.Services
{
    /// <summary>
    /// Service for scripting SQL Server database objects
    /// </summary>
    public interface IScriptingService
    {
        void ScriptDatabase(string serverName, string databaseName, Server server, Database database, 
            string[] objectTypes, string outputPath, bool scriptServerLevelObjects, string connectionString);
    }

    public class ScriptingService : IScriptingService
    {
        private readonly ILoggerService _logger;
        private readonly bool _scriptOneFilePerObjectType;

        public ScriptingService(ILoggerService logger, bool scriptOneFilePerObjectType)
        {
            _logger = logger;
            _scriptOneFilePerObjectType = scriptOneFilePerObjectType;
        }

        public void ScriptDatabase(string serverName, string databaseName, Server server, Database database,
            string[] objectTypes, string outputPath, bool scriptServerLevelObjects, string connectionString)
        {
            try
            {
                foreach (string type in objectTypes)
                {
                    string objectType = type.Trim().ToUpper();
                    if (string.IsNullOrWhiteSpace(objectType))
                        continue;

                    ProcessObjectType(serverName, databaseName, server, database, objectType, 
                        outputPath, scriptServerLevelObjects, connectionString);
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, databaseName, "Error", ex);
            }
        }

        private void ProcessObjectType(string serverName, string databaseName, Server server, Database database,
            string objectType, string outputPath, bool scriptServerLevelObjects, string connectionString)
        {
            if (objectType != "ALL")
            {
                _logger.Info(serverName, databaseName, $"Scripting {objectType}...");
            }

            switch (objectType)
            {
                case "ALL":
                    ScriptAllObjects(serverName, databaseName, server, database, outputPath, 
                        scriptServerLevelObjects, connectionString);
                    break;
                case "U":
                case "TABLES":
                    ScriptTables(serverName, databaseName, database, outputPath);
                    break;
                case "V":
                case "VIEWS":
                    ScriptViews(serverName, databaseName, database, outputPath);
                    break;
                case "P":
                case "PROCEDURES":
                    ScriptProcedures(serverName, databaseName, database, outputPath);
                    break;
                case "FN":
                case "FUNCTIONS":
                    ScriptFunctions(serverName, databaseName, database, outputPath);
                    break;
                case "TR":
                case "TRIGGERS":
                    ScriptTriggers(serverName, databaseName, database, outputPath);
                    break;
                case "SYN":
                case "SYNONYMS":
                    ScriptSynonyms(serverName, databaseName, database, outputPath);
                    break;
                case "JOBS":
                    ScriptJobs(serverName, databaseName, server, outputPath);
                    break;
                case "I":
                case "INDEXES":
                    ScriptIndexes(serverName, databaseName, database, outputPath);
                    break;
                case "F":
                case "FOREIGNKEYS":
                    ScriptForeignKeys(serverName, databaseName, database, outputPath);
                    break;
                case "UDT":
                case "USERDEFINEDTYPES":
                    ScriptUserDefinedTypes(serverName, databaseName, database, outputPath);
                    break;
                case "UDTT":
                case "USERDEFINEDTABLETYPES":
                    ScriptUserDefinedTableTypes(serverName, databaseName, database, outputPath);
                    break;
                case "UDDT":
                case "USERDEFINEDDATATYPES":
                    ScriptUserDefinedDataTypes(serverName, databaseName, database, outputPath);
                    break;
                case "ASS":
                case "ASSEMBLIES":
                    ScriptAssemblies(serverName, databaseName, database, outputPath);
                    break;
                case "CHECK":
                case "CHECKS":
                    ScriptChecks(serverName, databaseName, database, outputPath);
                    break;
                case "FG":
                case "FILEGROUPS":
                    ScriptFileGroups(serverName, databaseName, database, outputPath);
                    break;
                case "PS":
                case "PARTITIONSCHEMES":
                    ScriptPartitionSchemes(serverName, databaseName, database, outputPath);
                    break;
                case "PF":
                case "PARTITIONFUNCTIONS":
                    ScriptPartitionFunctions(serverName, databaseName, database, outputPath);
                    break;
                case "PG":
                case "PLANGUIDES":
                    ScriptPlanGuides(serverName, databaseName, database, outputPath);
                    break;
                case "SCH":
                case "SCHEMAS":
                    ScriptSchemas(serverName, databaseName, database, outputPath);
                    break;
                case "ROL":
                case "ROLES":
                    ScriptRoles(serverName, databaseName, database, outputPath);
                    break;
                case "USE":
                case "USERS":
                    ScriptUsers(serverName, databaseName, database, outputPath);
                    break;
                case "LS":
                case "LINKEDSERVERS":
                    ScriptLinkedServers(serverName, databaseName, server, outputPath);
                    break;
                case "CRE":
                case "CREDENTIALS":
                    ScriptCredentials(serverName, databaseName, server, outputPath);
                    break;
                case "PRA":
                case "PROXYACCOUNTS":
                    ScriptProxyAccounts(serverName, databaseName, server, outputPath);
                    break;
                case "SDT":
                case "SERVERDDLTRIGGERS":
                    ScriptServerDdlTriggers(serverName, databaseName, server, outputPath);
                    break;
                case "DDT":
                case "DATABASEDDLTRIGGERS":
                    ScriptDatabaseDdlTriggers(serverName, databaseName, database, outputPath);
                    break;
                case "NFR":
                    // This is a special case in the original code
                    ScriptNFR(serverName, databaseName, database, outputPath);
                    break;
                case "L":
                case "LOGINS":
                    // Logins might need custom handling if sp_help_revlogin_SQLScripter is used
                    ScriptLogins(serverName, databaseName, server, outputPath);
                    break;
                // Add more cases as needed
                default:
                    _logger.Info(serverName, databaseName, $"Unknown object type: {objectType}");
                    break;
            }
        }

        private void ScriptAllObjects(string serverName, string databaseName, Server server, Database database,
            string outputPath, bool scriptServerLevelObjects, string connectionString)
        {
            // Script all database-level objects
            ScriptTables(serverName, databaseName, database, outputPath);
            ScriptViews(serverName, databaseName, database, outputPath);
            ScriptProcedures(serverName, databaseName, database, outputPath);
            ScriptFunctions(serverName, databaseName, database, outputPath);
            ScriptTriggers(serverName, databaseName, database, outputPath);
            ScriptSynonyms(serverName, databaseName, database, outputPath);
            ScriptIndexes(serverName, databaseName, database, outputPath);
            ScriptForeignKeys(serverName, databaseName, database, outputPath);
            ScriptUserDefinedTypes(serverName, databaseName, database, outputPath);
            ScriptUserDefinedTableTypes(serverName, databaseName, database, outputPath);
            ScriptUserDefinedDataTypes(serverName, databaseName, database, outputPath);
            ScriptAssemblies(serverName, databaseName, database, outputPath);
            ScriptChecks(serverName, databaseName, database, outputPath);
            ScriptFileGroups(serverName, databaseName, database, outputPath);
            ScriptPartitionSchemes(serverName, databaseName, database, outputPath);
            ScriptPartitionFunctions(serverName, databaseName, database, outputPath);
            ScriptPlanGuides(serverName, databaseName, database, outputPath);
            ScriptSchemas(serverName, databaseName, database, outputPath);
            ScriptRoles(serverName, databaseName, database, outputPath);
            ScriptUsers(serverName, databaseName, database, outputPath);
            ScriptDatabaseDdlTriggers(serverName, databaseName, database, outputPath);
            
            // Script server-level objects if requested
            if (scriptServerLevelObjects)
            {
                ScriptJobs(serverName, databaseName, server, outputPath);
                ScriptLinkedServers(serverName, databaseName, server, outputPath);
                ScriptCredentials(serverName, databaseName, server, outputPath);
                ScriptProxyAccounts(serverName, databaseName, server, outputPath);
                ScriptServerDdlTriggers(serverName, databaseName, server, outputPath);
                ScriptLogins(serverName, databaseName, server, outputPath);
            }
        }

        private void ScriptTables(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting tables...");
                
                string objectPath = Path.Combine(outputPath, "Tables");
                Directory.CreateDirectory(objectPath);

                foreach (Table table in database.Tables)
                {
                    if (table.IsSystemObject)
                        continue;

                    try
                    {
                        ScriptTable(serverName, databaseName, table, objectPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(serverName, databaseName, $"Error scripting table {table.Name}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, databaseName, "Error", ex);
            }
        }

        private void ScriptTable(string serverName, string databaseName, Table table, string outputPath)
        {
            try
            {
                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    Indexes = true,
                    DriAll = true,
                    IncludeHeaders = true
                };

                string fileName = Path.Combine(outputPath, $"{FixObjectName(table.Schema)}.{FixObjectName(table.Name)}.sql");
                
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- Table: {table.Schema}.{table.Name}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");
                    writer.WriteLine();

                    var scripts = table.Script(options);
                    if (scripts != null)
                    {
                        foreach (string? script in scripts)
                        {
                            if (!string.IsNullOrEmpty(script))
                            {
                                writer.WriteLine(script);
                                writer.WriteLine("GO");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(serverName, databaseName, $"Error scripting table {table.Name}", ex);
            }
        }

        private void ScriptViews(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting views...");
                
                string objectPath = Path.Combine(outputPath, "Views");
                Directory.CreateDirectory(objectPath);

                foreach (View view in database.Views)
                {
                    if (view.IsSystemObject)
                        continue;

                    try
                    {
                        ScriptView(serverName, databaseName, view, objectPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(serverName, databaseName, $"Error scripting view {view.Name}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, databaseName, "Error", ex);
            }
        }

        private void ScriptView(string serverName, string databaseName, View view, string outputPath)
        {
            try
            {
                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    IncludeHeaders = true
                };

                string fileName = Path.Combine(outputPath, $"{FixObjectName(view.Schema)}.{FixObjectName(view.Name)}.sql");
                
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- View: {view.Schema}.{view.Name}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");
                    writer.WriteLine();

                    var scripts = view.Script(options);
                    if (scripts != null)
                    {
                        foreach (string? script in scripts)
                        {
                            if (!string.IsNullOrEmpty(script))
                            {
                                writer.WriteLine(script);
                                writer.WriteLine("GO");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(serverName, databaseName, $"Error scripting view {view.Name}", ex);
            }
        }

        private void ScriptProcedures(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting stored procedures...");
                
                string objectPath = Path.Combine(outputPath, "StoredProcedures");
                Directory.CreateDirectory(objectPath);

                foreach (StoredProcedure proc in database.StoredProcedures)
                {
                    if (proc.IsSystemObject)
                        continue;

                    try
                    {
                        ScriptProcedure(serverName, databaseName, proc, objectPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(serverName, databaseName, $"Error scripting procedure {proc.Name}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, databaseName, "Error", ex);
            }
        }

        private void ScriptProcedure(string serverName, string databaseName, StoredProcedure proc, string outputPath)
        {
            try
            {
                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    IncludeHeaders = true
                };

                string fileName = Path.Combine(outputPath, $"{FixObjectName(proc.Schema)}.{FixObjectName(proc.Name)}.sql");
                
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- Stored Procedure: {proc.Schema}.{proc.Name}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");
                    writer.WriteLine();

                    var scripts = proc.Script(options);
                    if (scripts != null)
                    {
                        foreach (string? script in scripts)
                        {
                            if (!string.IsNullOrEmpty(script))
                            {
                                writer.WriteLine(script);
                                writer.WriteLine("GO");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(serverName, databaseName, $"Error scripting procedure {proc.Name}", ex);
            }
        }

        private void ScriptFunctions(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting functions...");
                
                string objectPath = Path.Combine(outputPath, "Functions");
                Directory.CreateDirectory(objectPath);

                foreach (UserDefinedFunction func in database.UserDefinedFunctions)
                {
                    if (func.IsSystemObject)
                        continue;

                    try
                    {
                        ScriptFunction(serverName, databaseName, func, objectPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(serverName, databaseName, $"Error scripting function {func.Name}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, databaseName, "Error", ex);
            }
        }

        private void ScriptFunction(string serverName, string databaseName, UserDefinedFunction func, string outputPath)
        {
            try
            {
                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    IncludeHeaders = true
                };

                string fileName = Path.Combine(outputPath, $"{FixObjectName(func.Schema)}.{FixObjectName(func.Name)}.sql");
                
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- Function: {func.Schema}.{func.Name}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");
                    writer.WriteLine();

                    var scripts = func.Script(options);
                    if (scripts != null)
                    {
                        foreach (string? script in scripts)
                        {
                            if (!string.IsNullOrEmpty(script))
                            {
                                writer.WriteLine(script);
                                writer.WriteLine("GO");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(serverName, databaseName, $"Error scripting function {func.Name}", ex);
            }
        }

        private void ScriptTriggers(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting triggers...");
                
                string objectPath = Path.Combine(outputPath, "Triggers");
                Directory.CreateDirectory(objectPath);

                // Table triggers
                foreach (Table table in database.Tables)
                {
                    if (table.IsSystemObject)
                        continue;

                    foreach (Trigger trigger in table.Triggers)
                    {
                        try
                        {
                            ScriptTrigger(serverName, databaseName, trigger, objectPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(serverName, databaseName, $"Error scripting trigger {trigger.Name}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.WriteToLog(serverName, databaseName, "Error", ex);
            }
        }

        private void ScriptTrigger(string serverName, string databaseName, Trigger trigger, string outputPath)
        {
            try
            {
                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    IncludeHeaders = true
                };

                string fileName = Path.Combine(outputPath, $"{FixObjectName(trigger.Name)}.sql");
                
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- Trigger: {trigger.Name}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");
                    writer.WriteLine();

                    var scripts = trigger.Script(options);
                    if (scripts != null)
                    {
                        foreach (string? script in scripts)
                        {
                            if (!string.IsNullOrEmpty(script))
                            {
                                writer.WriteLine(script);
                                writer.WriteLine("GO");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(serverName, databaseName, $"Error scripting trigger {trigger.Name}", ex);
            }
        }

        private void ScriptIndexes(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting indexes...");
                
                string objectPath = Path.Combine(outputPath, "Indexes");
                Directory.CreateDirectory(objectPath);

                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    DriPrimaryKey = true,
                    DriUniqueKeys = true,
                    ClusteredIndexes = true,
                    NonClusteredIndexes = true
                };

                foreach (Table table in database.Tables)
                {
                    if (table.IsSystemObject || table.Indexes.Count == 0)
                        continue;

                    try
                    {
                        if (_scriptOneFilePerObjectType)
                        {
                            // In single file mode, we still group by table for indexes as it makes more sense
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(table.Name)}_Indexes.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Indexes for table: {table.Schema}.{table.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                foreach (Microsoft.SqlServer.Management.Smo.Index index in table.Indexes)
                                {
                                    var scripts = index.Script(options);
                                    if (scripts != null)
                                    {
                                        foreach (string? s in scripts)
                                        {
                                            writer.WriteLine(s?.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (Microsoft.SqlServer.Management.Smo.Index index in table.Indexes)
                            {
                                string fileName = Path.Combine(objectPath, $"{FixObjectName(table.Name)}.{FixObjectName(index.Name)}.sql");
                                using (var writer = new StreamWriter(fileName))
                                {
                                    writer.WriteLine($"-- Index: {index.Name} on {table.Schema}.{table.Name}");
                                    writer.WriteLine($"USE [{databaseName}];");
                                    writer.WriteLine("GO");

                                    var scripts = index.Script(options);
                                    if (scripts != null)
                                    {
                                        foreach (string? s in scripts)
                                        {
                                            writer.WriteLine(s?.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting indexes for table {table.Name}", ex); }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptForeignKeys(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting foreign keys...");
                
                string objectPath = Path.Combine(outputPath, "ForeignKeys");
                Directory.CreateDirectory(objectPath);

                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    DriForeignKeys = true
                };

                foreach (Table table in database.Tables)
                {
                    if (table.IsSystemObject || table.ForeignKeys.Count == 0)
                        continue;

                    try
                    {
                        if (_scriptOneFilePerObjectType)
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(table.Name)}_ForeignKeys.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Foreign Keys for table: {table.Schema}.{table.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = table.Script(options);
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET") && !s.Contains("CREATE TABLE"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (ForeignKey fk in table.ForeignKeys)
                            {
                                string fileName = Path.Combine(objectPath, $"{FixObjectName(table.Name)}.{FixObjectName(fk.Name)}.sql");
                                using (var writer = new StreamWriter(fileName))
                                {
                                    writer.WriteLine($"-- Foreign Key: {fk.Name} on {table.Schema}.{table.Name}");
                                    writer.WriteLine($"USE [{databaseName}];");
                                    writer.WriteLine("GO");

                                    var scripts = fk.Script(options);
                                    if (scripts != null)
                                    {
                                        foreach (string? s in scripts)
                                        {
                                            writer.WriteLine(s?.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting foreign keys for table {table.Name}", ex); }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptSynonyms(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting synonyms...");
                
                string objectPath = Path.Combine(outputPath, "Synonyms");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "Synonyms.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Synonyms for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");
                        writer.WriteLine();

                        int i = 1;
                        foreach (Synonym syn in database.Synonyms)
                        {
                            try
                            {
                                var scripts = syn.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    writer.WriteLine($"-- {i++}: {syn.Name}");
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                    writer.WriteLine();
                                }
                            }
                            catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting synonym {syn.Name}", ex); }
                        }
                    }
                }
                else
                {
                    foreach (Synonym syn in database.Synonyms)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(syn.Schema)}.{FixObjectName(syn.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Synonym: {syn.Schema}.{syn.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");
                                
                                var scripts = syn.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine((s?.TrimEnd() ?? string.Empty));
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting synonym {syn.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptJobs(string serverName, string databaseName, Server server, string outputPath)
        {
            try
            {
                _logger.Info(serverName, "", "Scripting jobs...");
                
                string objectPath = Path.Combine(outputPath, "Jobs");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "Jobs.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- SQL Server Agent Jobs for server: {serverName}");
                        writer.WriteLine("USE [msdb];");
                        writer.WriteLine("GO");
                        writer.WriteLine();

                        int i = 1;
                        foreach (Job job in server.JobServer.Jobs)
                        {
                            try
                            {
                                var scripts = job.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    writer.WriteLine($"-- {i++}: {job.Name}");
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                    writer.WriteLine();
                                }
                            }
                            catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting job {job.Name}", ex); }
                        }
                    }
                }
                else
                {
                    foreach (Job job in server.JobServer.Jobs)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(job.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Job: {job.Name}");
                                writer.WriteLine("USE [msdb];");
                                writer.WriteLine("GO");
                                
                                var scripts = job.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine((s?.TrimEnd() ?? string.Empty));
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting job {job.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, "", "Error", ex); }
        }

        private void ScriptUserDefinedTypes(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting user defined types...");
                
                string objectPath = Path.Combine(outputPath, "UserDefinedTypes");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "UserDefinedTypes.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- User Defined Types for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (UserDefinedType udt in database.UserDefinedTypes)
                        {
                            try
                            {
                                var scripts = udt.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    writer.WriteLine($"-- {i++}: {udt.Name}");
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting UDT {udt.Name}", ex); }
                        }
                    }
                }
                else
                {
                    foreach (UserDefinedType udt in database.UserDefinedTypes)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(udt.Schema)}.{FixObjectName(udt.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- UDT: {udt.Schema}.{udt.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = udt.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting UDT {udt.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptUserDefinedTableTypes(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting user defined table types...");
                
                string objectPath = Path.Combine(outputPath, "UserDefinedTableTypes");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "UserDefinedTableTypes.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- User Defined Table Types for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (UserDefinedTableType udtt in database.UserDefinedTableTypes)
                        {
                            try
                            {
                                var scripts = udtt.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    writer.WriteLine($"-- {i++}: {udtt.Name}");
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting UDTT {udtt.Name}", ex); }
                        }
                    }
                }
                else
                {
                    foreach (UserDefinedTableType udtt in database.UserDefinedTableTypes)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(udtt.Schema)}.{FixObjectName(udtt.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- UDTT: {udtt.Schema}.{udtt.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = udtt.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting UDTT {udtt.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptUserDefinedDataTypes(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting user defined data types...");
                
                string objectPath = Path.Combine(outputPath, "UserDefinedDataTypes");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "UserDefinedDataTypes.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- User Defined Data Types for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (UserDefinedDataType uddt in database.UserDefinedDataTypes)
                        {
                            try
                            {
                                var scripts = uddt.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    writer.WriteLine($"-- {i++}: {uddt.Name}");
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting UDDT {uddt.Name}", ex); }
                        }
                    }
                }
                else
                {
                    foreach (UserDefinedDataType uddt in database.UserDefinedDataTypes)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(uddt.Schema)}.{FixObjectName(uddt.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- UDDT: {uddt.Schema}.{uddt.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = uddt.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting UDDT {uddt.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptAssemblies(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting assemblies...");
                
                string objectPath = Path.Combine(outputPath, "Assemblies");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "Assemblies.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Assemblies for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (SqlAssembly ass in database.Assemblies)
                        {
                            try
                            {
                                var scripts = ass.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    writer.WriteLine($"-- {i++}: {ass.Name}");
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && !s.StartsWith("SET"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting assembly {ass.Name}", ex); }
                        }
                    }
                }
                else
                {
                    foreach (SqlAssembly ass in database.Assemblies)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(ass.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Assembly: {ass.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = ass.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting assembly {ass.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptChecks(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting check constraints...");
                
                string objectPath = Path.Combine(outputPath, "Checks");
                Directory.CreateDirectory(objectPath);

                var options = new ScriptingOptions
                {
                    AnsiFile = true,
                    AllowSystemObjects = false,
                    DriChecks = true
                };

                foreach (Table table in database.Tables)
                {
                    if (table.IsSystemObject || table.Checks.Count == 0)
                        continue;

                    try
                    {
                        if (_scriptOneFilePerObjectType)
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(table.Name)}_Checks.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Checks for table: {table.Schema}.{table.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = table.Script(options);
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        if (s != null && s.Contains("ADD"))
                                        {
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            var scripts = table.Script(options);
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    if (s != null && s.Contains("ADD"))
                                    {
                                        // A bit simplified compared to original GetNextWord logic for now
                                        string fileName = Path.Combine(objectPath, $"{FixObjectName(table.Name)}_Check_{Guid.NewGuid().ToString().Substring(0, 8)}.sql");
                                        using (var writer = new StreamWriter(fileName))
                                        {
                                            writer.WriteLine($"-- Check for table: {table.Schema}.{table.Name}");
                                            writer.WriteLine($"USE [{databaseName}];");
                                            writer.WriteLine("GO");
                                            writer.WriteLine(s.TrimEnd());
                                            writer.WriteLine("GO");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting checks for table {table.Name}", ex); }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptFileGroups(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting file groups...");
                
                string objectPath = Path.Combine(outputPath, "FileGroups");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "FileGroups.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- File Groups for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        foreach (FileGroup fg in database.FileGroups)
                        {
                            if (fg.Name.ToUpper() == "PRIMARY") continue;

                            writer.WriteLine($"IF NOT EXISTS (SELECT groupname FROM sys.sysfilegroups WHERE groupname = '{fg.Name}')");
                            writer.WriteLine($"    ALTER DATABASE [{databaseName}] ADD FILEGROUP [{fg.Name}];");
                            writer.WriteLine("GO");
                        }
                    }
                }
                else
                {
                    foreach (FileGroup fg in database.FileGroups)
                    {
                        if (fg.Name.ToUpper() == "PRIMARY") continue;

                        string fileName = Path.Combine(objectPath, $"{FixObjectName(fg.Name)}.sql");
                        using (var writer = new StreamWriter(fileName))
                        {
                            writer.WriteLine($"-- File Group: {fg.Name}");
                            writer.WriteLine($"USE [{databaseName}];");
                            writer.WriteLine("GO");
                            writer.WriteLine($"IF NOT EXISTS (SELECT groupname FROM sys.sysfilegroups WHERE groupname = '{fg.Name}')");
                            writer.WriteLine($"    ALTER DATABASE [{databaseName}] ADD FILEGROUP [{fg.Name}];");
                            writer.WriteLine("GO");
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptPartitionSchemes(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting partition schemes...");
                
                string objectPath = Path.Combine(outputPath, "PartitionSchemes");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "PartitionSchemes.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Partition Schemes for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        foreach (PartitionScheme ps in database.PartitionSchemes)
                        {
                            var scripts = ps.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (PartitionScheme ps in database.PartitionSchemes)
                    {
                        string fileName = Path.Combine(objectPath, $"{FixObjectName(ps.Name)}.sql");
                        using (var writer = new StreamWriter(fileName))
                        {
                            writer.WriteLine($"-- Partition Scheme: {ps.Name}");
                            writer.WriteLine($"USE [{databaseName}];");
                            writer.WriteLine("GO");

                            var scripts = ps.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    writer.WriteLine(s?.TrimEnd());
                                    writer.WriteLine("GO");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptPartitionFunctions(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting partition functions...");
                
                string objectPath = Path.Combine(outputPath, "PartitionFunctions");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "PartitionFunctions.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Partition Functions for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        foreach (PartitionFunction pf in database.PartitionFunctions)
                        {
                            var scripts = pf.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (PartitionFunction pf in database.PartitionFunctions)
                    {
                        string fileName = Path.Combine(objectPath, $"{FixObjectName(pf.Name)}.sql");
                        using (var writer = new StreamWriter(fileName))
                        {
                            writer.WriteLine($"-- Partition Function: {pf.Name}");
                            writer.WriteLine($"USE [{databaseName}];");
                            writer.WriteLine("GO");

                            var scripts = pf.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    writer.WriteLine(s?.TrimEnd());
                                    writer.WriteLine("GO");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptPlanGuides(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting plan guides...");
                
                string objectPath = Path.Combine(outputPath, "PlanGuides");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "PlanGuides.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Plan Guides for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        foreach (PlanGuide pg in database.PlanGuides)
                        {
                            var scripts = pg.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (PlanGuide pg in database.PlanGuides)
                    {
                        string fileName = Path.Combine(objectPath, $"{FixObjectName(pg.Name)}.sql");
                        using (var writer = new StreamWriter(fileName))
                        {
                            writer.WriteLine($"-- Plan Guide: {pg.Name}");
                            writer.WriteLine($"USE [{databaseName}];");
                            writer.WriteLine("GO");

                            var scripts = pg.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                foreach (string? s in scripts)
                                {
                                    writer.WriteLine(s?.TrimEnd());
                                    writer.WriteLine("GO");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptSchemas(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting schemas...");
                
                string objectPath = Path.Combine(outputPath, "Schemas");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "Schemas.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Schemas for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (Schema sch in database.Schemas)
                        {
                            if (sch.IsSystemObject) continue;
                            var scripts = sch.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {sch.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (Schema sch in database.Schemas)
                    {
                        if (sch.IsSystemObject) continue;
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(sch.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Schema: {sch.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = sch.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting schema {sch.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptRoles(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting database roles...");
                
                string objectPath = Path.Combine(outputPath, "Roles");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "Roles.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Roles for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (DatabaseRole role in database.Roles)
                        {
                            var scripts = role.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {role.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (DatabaseRole role in database.Roles)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(role.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Role: {role.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = role.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting role {role.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptUsers(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting database users...");
                
                string objectPath = Path.Combine(outputPath, "Users");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "Users.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Users for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (User user in database.Users)
                        {
                            if (user.IsSystemObject) continue;
                            var scripts = user.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {user.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (User user in database.Users)
                    {
                        if (user.IsSystemObject) continue;
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(user.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- User: {user.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = user.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting user {user.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptLinkedServers(string serverName, string databaseName, Server server, string outputPath)
        {
            try
            {
                _logger.Info(serverName, "", "Scripting linked servers...");
                
                string objectPath = Path.Combine(outputPath, "LinkedServers");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "LinkedServers.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Linked Servers for server: {serverName}");
                        writer.WriteLine("USE [master];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (LinkedServer ls in server.LinkedServers)
                        {
                            var scripts = ls.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {ls.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (LinkedServer ls in server.LinkedServers)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(ls.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Linked Server: {ls.Name}");
                                writer.WriteLine("USE [master];");
                                writer.WriteLine("GO");

                                var scripts = ls.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting linked server {ls.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, "", "Error", ex); }
        }

        private void ScriptCredentials(string serverName, string databaseName, Server server, string outputPath)
        {
            try
            {
                _logger.Info(serverName, "", "Scripting credentials...");
                
                string objectPath = Path.Combine(outputPath, "Credentials");
                Directory.CreateDirectory(objectPath);

                string fileName = Path.Combine(objectPath, "Credentials.sql");
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- Credentials for server: {serverName}");
                    writer.WriteLine("USE [master];");
                    writer.WriteLine("GO");

                    int i = 1;
                    foreach (Credential c in server.Credentials)
                    {
                        try
                        {
                            var scripts = c.EnumLogins(); // Original used EnumLogins
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {c.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting credential {c.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, "", "Error", ex); }
        }

        private void ScriptProxyAccounts(string serverName, string databaseName, Server server, string outputPath)
        {
            try
            {
                _logger.Info(serverName, "", "Scripting proxy accounts...");
                
                string objectPath = Path.Combine(outputPath, "ProxyAccounts");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "ProxyAccounts.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Proxy Accounts for server: {serverName}");
                        writer.WriteLine("USE [msdb];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (ProxyAccount pa in server.JobServer.ProxyAccounts)
                        {
                            var scripts = pa.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {pa.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (ProxyAccount pa in server.JobServer.ProxyAccounts)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(pa.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Proxy Account: {pa.Name}");
                                writer.WriteLine("USE [msdb];");
                                writer.WriteLine("GO");

                                var scripts = pa.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting proxy account {pa.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, "", "Error", ex); }
        }

        private void ScriptServerDdlTriggers(string serverName, string databaseName, Server server, string outputPath)
        {
            try
            {
                _logger.Info(serverName, "", "Scripting server DDL triggers...");
                
                string objectPath = Path.Combine(outputPath, "ServerDdlTriggers");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "ServerDdlTriggers.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Server DDL Triggers for server: {serverName}");
                        writer.WriteLine("USE [master];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (ServerDdlTrigger tr in server.Triggers)
                        {
                            var scripts = tr.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {tr.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (ServerDdlTrigger tr in server.Triggers)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(tr.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Server DDL Trigger: {tr.Name}");
                                writer.WriteLine("USE [master];");
                                writer.WriteLine("GO");

                                var scripts = tr.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting server DDL trigger {tr.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, "", "Error", ex); }
        }

        private void ScriptDatabaseDdlTriggers(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting database DDL triggers...");
                
                string objectPath = Path.Combine(outputPath, "DatabaseDdlTriggers");
                Directory.CreateDirectory(objectPath);

                if (_scriptOneFilePerObjectType)
                {
                    string fileName = Path.Combine(objectPath, "DatabaseDdlTriggers.sql");
                    using (var writer = new StreamWriter(fileName))
                    {
                        writer.WriteLine($"-- Database DDL Triggers for database: {databaseName}");
                        writer.WriteLine($"USE [{databaseName}];");
                        writer.WriteLine("GO");

                        int i = 1;
                        foreach (DatabaseDdlTrigger tr in database.Triggers)
                        {
                            var scripts = tr.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- {i++}: {tr.Name}");
                                foreach (string? s in scripts)
                                {
                                    if (s != null && !s.StartsWith("SET"))
                                    {
                                        writer.WriteLine(s.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (DatabaseDdlTrigger tr in database.Triggers)
                    {
                        try
                        {
                            string fileName = Path.Combine(objectPath, $"{FixObjectName(tr.Name)}.sql");
                            using (var writer = new StreamWriter(fileName))
                            {
                                writer.WriteLine($"-- Database DDL Trigger: {tr.Name}");
                                writer.WriteLine($"USE [{databaseName}];");
                                writer.WriteLine("GO");

                                var scripts = tr.Script(new ScriptingOptions { AnsiFile = true });
                                if (scripts != null)
                                {
                                    foreach (string? s in scripts)
                                    {
                                        writer.WriteLine(s?.TrimEnd());
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, databaseName, $"Error scripting database DDL trigger {tr.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptNFR(string serverName, string databaseName, Database database, string outputPath)
        {
            try
            {
                _logger.Info(serverName, databaseName, "Scripting NFR triggers and foreign keys...");
                
                string objectPath = Path.Combine(outputPath, "NFR");
                Directory.CreateDirectory(objectPath);

                ScriptNFRTriggers(serverName, databaseName, database, objectPath);
                ScriptNFRForeignKeys(serverName, databaseName, database, objectPath);
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, databaseName, "Error", ex); }
        }

        private void ScriptNFRTriggers(string serverName, string databaseName, Database database, string objectPath)
        {
            try
            {
                string fileName = Path.Combine(objectPath, "Triggers.sql");
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- NFR Triggers for database: {databaseName}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");

                    var so = new ScriptingOptions { AnsiFile = true, AllowSystemObjects = false, Triggers = true };
                    int i = 1;

                    foreach (Table t in database.Tables)
                    {
                        if (t.IsSystemObject || t.Triggers.Count == 0) continue;
                        var scripts = t.Script(so);
                        if (scripts != null)
                        {
                            foreach (string? s in scripts)
                            {
                                if (s != null && s.Contains("TRIGGER"))
                                {
                                    writer.WriteLine($"-- {i++}");
                                    writer.WriteLine("GO");
                                    if (!s.StartsWith("SET"))
                                    {
                                        string temp = s.ToUpper();
                                        if (!temp.Contains("NOT FOR REPLICATION"))
                                        {
                                            int asIdx = temp.IndexOf("AS");
                                            if (asIdx != -1)
                                            {
                                                string first = s.Substring(0, asIdx);
                                                string second = s.Substring(asIdx);
                                                writer.WriteLine(first + "NOT FOR REPLICATION " + Environment.NewLine + second);
                                            }
                                            else writer.WriteLine(s);
                                        }
                                        else writer.WriteLine(s);
                                        writer.WriteLine("GO");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.Error(serverName, databaseName, "Error scripting NFR triggers", ex); }
        }

        private void ScriptNFRForeignKeys(string serverName, string databaseName, Database database, string objectPath)
        {
            try
            {
                string fileName = Path.Combine(objectPath, "ForeignKeys.sql");
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- NFR Foreign Keys for database: {databaseName}");
                    writer.WriteLine($"USE [{databaseName}];");
                    writer.WriteLine("GO");

                    var so = new ScriptingOptions { AnsiFile = true, AllowSystemObjects = false, DriForeignKeys = true };
                    int i = 1;

                    foreach (Table t in database.Tables)
                    {
                        if (t.IsSystemObject || t.ForeignKeys.Count == 0) continue;
                        var scripts = t.Script(so);
                        if (scripts != null)
                        {
                            foreach (string? s in scripts)
                            {
                                if (s != null && !s.StartsWith("SET") && !s.StartsWith("CREATE") && s.Contains("ADD"))
                                {
                                    writer.WriteLine($"-- {i++}");
                                    writer.WriteLine("GO");
                                    if (!s.ToUpper().Contains("REPLICATION"))
                                    {
                                        writer.WriteLine(s.TrimEnd(' ', ';') + " NOT FOR REPLICATION;");
                                    }
                                    else writer.WriteLine(s);
                                    writer.WriteLine("GO");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.Error(serverName, databaseName, "Error scripting NFR foreign keys", ex); }
        }

        private void ScriptLogins(string serverName, string databaseName, Server server, string outputPath)
        {
            try
            {
                _logger.Info(serverName, "", "Scripting logins...");
                
                string objectPath = Path.Combine(outputPath, "Logins");
                Directory.CreateDirectory(objectPath);

                string fileName = Path.Combine(objectPath, "Logins.sql");
                using (var writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"-- Logins for server: {serverName}");
                    writer.WriteLine("USE [master];");
                    writer.WriteLine("GO");

                    int i = 1;
                    // Attempt to use SMO for scripting logins as a fallback/primary if possible
                    foreach (Login login in server.Logins)
                    {
                        if (login.IsSystemObject) continue;
                        try
                        {
                            var scripts = login.Script(new ScriptingOptions { AnsiFile = true });
                            if (scripts != null)
                            {
                                writer.WriteLine($"-- Login: {login.Name}");
                                foreach (string? s in scripts)
                                {
                                    writer.WriteLine(s?.TrimEnd());
                                    writer.WriteLine("GO");
                                }
                            }
                        }
                        catch (Exception ex) { _logger.Error(serverName, "", $"Error scripting login {login.Name}", ex); }
                    }
                }
            }
            catch (Exception ex) { _logger.WriteToLog(serverName, "", "Error", ex); }
        }

        private string FixObjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return name.Replace("[", "").Replace("]", "").Replace("\\", "_").Replace("/", "_");
        }
    }
}
