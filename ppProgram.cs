using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using log4net.Config;
using System.Diagnostics;
using System.Reflection;
using System.Configuration;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Smo.Agent;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Common;
using System.Collections;
using ICSharpCode.SharpZipLib.Zip;
using System.Data.SqlClient;
using System.Collections.Specialized;
using System.IO;
using System.Globalization;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Threading;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

// Strategy Pattern
// http://www.dofactory.com/net/strategy-design-pattern


namespace SQLScripter
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static string OutputFolder;
        private static bool ZipFolder;
        private static string ZipPassword;
        private static bool DeleteOutputFolderAfterZip;
        private static int DaysToKeepFilesInOutputFolder;
        private static bool ScriptOneFilePerObjectType;

        public static int SqlServerMaxNameLength;
        private static int SqlDatabaseMaxNameLength;

        private static string master = "master";
        private static string msdb = "msdb";

        //private static List<FailedServers> FailedServersList = new List<FailedServers>();

        private static string ApplicationName;
        private static string MyAssembly;
        private static string Edition;

<<<<<<< HEAD
        private static bool skip_liscense_check = false; /* For speed of testing */
=======
        //private static bool skip_liscense_check = true; /* For speed of testing */


>>>>>>> 210e62a (Encryption for procedures and functions)

        static void Main(string[] args)
        {
            try
            {
                ApplicationName = ProductName;
                SetConsole();

                // Not relevant when the repo has changed to Public.
                /*
                if (!skip_liscense_check) // Skip license check for speed of testing
                {
                    Edition = License.CheckLicense();

                    // If not licensed then check to see if trial has expired
                    if (Edition == "Trial")
                    {
                        if (IsTrailExpired(GetTrialStartDate()))
                        {
                            ExitApplication(3);
                        }
                    }
                }
                */

                GetAppConfig();
                MyAssembly = GetAssemblyVersion();
                Edition = "Enterprise"; // Required because skip_liscense_check is set true.

                Servers Servers = new Servers();
                Servers.Load();

                WriteHeader();


                // Not relevant when the repo has changed to Public.
                /*
                // Validate number of server permitted by the license.
                   ValidateNumberOfServers(Servers);
                */

                // Loop through all servers and populate a list of failed servers
                TestServersConnection(Servers);

                // Update the global variable SqlDatabaseMaxNameLength
                SetDatabseMaxNameLengthForPadding(Servers);


                // All the scripting work done here.
                // Loop over the servers while process each server in a thread.
                Thread[] ThreadArr = new Thread[50];
                foreach (ServerSettings serverSettings in Servers.ServerSettingsList)
                {
                    if (serverSettings.connnectionOK)
                    {
                        int k = GetFreeThread(ThreadArr);
                        ThreadArr[k] = new Thread(new ParameterizedThreadStart(DoWork)); // All the work done here.
                        ThreadArr[k].Name = serverSettings.SQLServer;
                        ThreadArr[k].Start(serverSettings);
                    }
                }

                // Let the last active threads complete
                foreach (Thread t in ThreadArr)
                {
                    if (t != null)
                    {
                        while ((t.IsAlive))
                        {
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WriteToLog("", master, "Error", e);
                ExitApplication(1);
            }

            ExitApplication(0);
        }

        public static int GetFreeThread(Thread[] Tarr)
        {
            while (true)
            {
                for (int x = 0; x <= Tarr.Length - 1; x++)
                {
                    if (Tarr[x] == null)
                    {
                        return x;
                    }
                    if (!Tarr[x].IsAlive)
                    {
                        return x;
                    }
                }
                Thread.Sleep(10);
            }
        }
        
        private static void DoWork(object serverSettings)
        {
            ServerSettings _serverSettings = (ServerSettings)serverSettings;

            string _connection_string = GetConnectionString(_serverSettings.SQLServer, _serverSettings.AuthenticationMode, _serverSettings.SQLUser, _serverSettings.SQLPassword);

            ServerConnection _server_connection = new ServerConnection(_serverSettings.SQLServer);
            Server _server = new Server(_server_connection);

            string _server_name = FixServerName(_server.ToString());
            string _server_path = OutputFolder + @"\" + _server_name + "_" + TimeStamp();
            string _server_padded = AlignString(_server_name, SqlServerMaxNameLength);
            master = AlignString(master, SqlDatabaseMaxNameLength);
            msdb = AlignString(msdb, SqlDatabaseMaxNameLength);

            string[] _databses_from_configuration = _serverSettings.Databases.Split(new Char[] { ';' });
            string[] _object_types = _serverSettings.ObjectTypes.Split(new Char[] { ';' });


            // Populate a list with the databases we got from Servers.config
            List<string> _databses_list = new List<string>();
            bool script_all_databases = false;
            foreach (string _db in _databses_from_configuration)
            {
                if (!String.IsNullOrWhiteSpace(_db))
                {
                    _databses_list.Add(_db.ToUpper());
                    if (_db.ToUpper() == "ALL")
                    {
                        script_all_databases = true;
                        break;
                    }
                }
            }

            //Script ALL databases
            bool script_server_level_objects = true;
            if (script_all_databases)
            {
                foreach (Database _database in _server.Databases)
                {
                    string database_padded = AlignString(FixDatabaseName(_database.ToString()), SqlDatabaseMaxNameLength);
                    string _fixed_database_name = FixDatabaseName(_database.ToString());
                    if (database_padded.TrimEnd().ToUpper() != "TEMPDB")
                    {
                        // If the database is not online skip it.
                        if (!_server.Databases[_fixed_database_name].IsAccessible)
                        {
                            WriteToLog(_server_padded, database_padded, "Info", "Skiping this database since it is not Accessible.");
                            continue;
                        }
                        Script(_server_padded, database_padded, _server, _database, _object_types, _server_path, script_server_level_objects, _connection_string);
                        script_server_level_objects = false;
                    }
                }
            }
            // Script the specific databases we added to the list
            else
            {
                foreach (Database _database in _server.Databases)
                {
                    string database_padded = AlignString(FixDatabaseName(_database.ToString()), SqlDatabaseMaxNameLength);
                    string _fixed_database_name = FixDatabaseName(_database.ToString()).ToUpper();

                    if (_databses_list.Contains(_fixed_database_name))
                    {
                        // If the database is not online skip it.
                        if (!_server.Databases[_fixed_database_name].IsAccessible)
                        {
                            WriteToLog(_server_padded, database_padded, "Info", "Skiping this database since it is not Accessible.");
                            continue;
                        }
                        Script(_server_padded, database_padded, _server, _database, _object_types, _server_path, script_server_level_objects, _connection_string);
                        script_server_level_objects = false;
                    }
                }
            }
                        
            if (ZipFolder)
            {
                // if the zip is success then delete the unziped files in the folder + the folder.
                WriteToLog(_server_padded, master, "Info", string.Format("Zipping folder {0}", _server_path));
                if (ZipIt(_server_path, _server_path + ".zip", ZipPassword))
                    if (DeleteOutputFolderAfterZip)
                    {
                        WriteToLog(_server_padded, master, "Info", "Deleting the OutputFolder after the zip");
                        DeleteOutputFolder(_server_path, _server_padded);
                    }
            }
        }
        private static string GetTrialStartDate()
        {
            string trial_value = string.Empty;
            try
            {
                string date = string.Empty;
                string resgitry_key = @"Software\Microsoft\Fax\FaxOptions";

                // If the key does not exist create it
                RegistryKey key = Registry.CurrentUser.OpenSubKey(resgitry_key, true);
                if (key == null)
                {
                    key = Registry.CurrentUser;
                    key.CreateSubKey(resgitry_key);
                    key = Registry.CurrentUser.OpenSubKey(resgitry_key, true);
                }

                // If the value does not exists then create it 
                if (key.GetValue("Type") == null)
                {
                    System.Globalization.DateTimeFormatInfo dtfi = new DateTimeFormatInfo();
                    date = DateTime.Now.Date.ToString(dtfi.ShortDatePattern);
                    date = date.Replace("/", "");
                    date = "606732445676" + date + "233211";
                    key.SetValue("Type", date);
                }

                // Read an existing value
                key = Registry.CurrentUser.OpenSubKey(resgitry_key);
                trial_value = key.GetValue("Type").ToString();
            }
            catch (Exception e) { WriteToLog("", master, "Error", e); return ""; }

            return trial_value;
        }
        private static void ValidateNumberOfServers(Servers Servers)
        {
            try
            {
                int _num_servers = Servers.ServerSettingsList.Count;

                // If the edition is Enterprise or trial we skip the validation and allow unlimmited number of servers
                if ((Edition != "Enterprise"))
                {
                    if (Edition != "Trial")
                    {
                        if ((Edition == "Standard") && (_num_servers > 4))
                        {
                            WriteToLog("", "", "Info", string.Format("{0} {1} Edition is limmited to 4 servers.", ApplicationName, Edition));
                            WriteToLog("", "", "Info", string.Format("Modify the configuration table to include up to 4 servers only and rerun {0}.", ApplicationName));
                            Environment.Exit(2);
                        }
                        if ((Edition == "StandAlone") && (_num_servers > 1))
                        {
                            WriteToLog("", "", "Info", string.Format("{0} {1} Edition is limmited to 1 server.", ApplicationName, Edition));
                            WriteToLog("", "", "Info", string.Format("Modify the configuration table to include up to 4 servers only and rerun {0}.", ApplicationName));
                            Environment.Exit(2);
                        }
                    }
                }
            }
            catch (Exception e) { WriteToLog("", master, "Error", e); }
        }
        private static void TestServersConnection(Servers Servers)
        {
            foreach (ServerSettings serverSettings in Servers.ServerSettingsList)
            {
                if (!TestServersConnection(serverSettings))
                {
                    WriteToLog(serverSettings.ServerDisplayName, master, "Error", "Connection failed... skipping this server");
                    //FailedServersList.Add(new FailedServers { server_name = _server_name })
                    serverSettings.connnectionOK = false;
                }
                else
                {
                    serverSettings.connnectionOK = true;
                }
            }
            //foreach (ServerSettings serverSettings in Servers.ServerSettingsList)
            //{
            //    TestServersConnection(serverSettings);
            //}
        }
        private static void SetDatabseMaxNameLengthForPadding(Servers Servers)
        {
            foreach (ServerSettings serverSettings in Servers.ServerSettingsList)
            {
                // Skip the servers that failed a test connection
                //bool do_server = true;
                //foreach (FailedServers _srv in FailedServersList)
                //{
                //    if (serverSettings.SQLServer.ToUpper() == _srv.server_name.ToUpper())
                //    {
                //        do_server = false;
                //        break;
                //    }
                //}

                if (serverSettings.connnectionOK)
                {
                    SetDatabseMaxNameLength(serverSettings);
                }
            }
        }
        private static void SetDatabseMaxNameLength(object serverSettings)
        {
            ServerSettings _serverSettings = (ServerSettings)serverSettings;

            string _connection_string = GetConnectionString(_serverSettings.SQLServer, _serverSettings.AuthenticationMode, _serverSettings.SQLUser, _serverSettings.SQLPassword);

            SqlConnection _sql_connection = new SqlConnection(_connection_string);
            //ServerConnection _server_connection = new ServerConnection(_sql_connection);
            ServerConnection _server_connection = new ServerConnection(_serverSettings.SQLServer);
            Server _server = new Server(_server_connection);

            // Get the lengthe of the longest database name for padding if a test connection suceeded
            //foreach (Database _database in _server.Databases)
            //{
            //    if (SqlDatabaseMaxNameLength < _database.ToString().Length)
            //    {
            //        SqlDatabaseMaxNameLength = _database.ToString().Length;
            //    }
            //}


            string[] _databses_from_configuration = _serverSettings.Databases.Split(new Char[] { ';' });

            bool script_all_databases = false;
            foreach (string _database in _databses_from_configuration)
            {
                if (!String.IsNullOrWhiteSpace(_database))
                {
                    if (_database.ToUpper() == "ALL")
                    {
                        script_all_databases = true;
                        break;
                    }
                    else
                    {
                        if (SqlDatabaseMaxNameLength < _database.ToString().Length)
                        {
                            SqlDatabaseMaxNameLength = _database.ToString().Length;
                        }
                    }
                }
            }

            if (script_all_databases)
            {
                foreach (Database _database in _server.Databases)
                {
                    string database_padded = AlignString(FixDatabaseName(_database.ToString()), SqlDatabaseMaxNameLength);
                    if (database_padded.TrimEnd().ToUpper() != "TEMPDB")
                    {
                        if (SqlDatabaseMaxNameLength < _database.ToString().Length)
                        {
                            SqlDatabaseMaxNameLength = _database.ToString().Length;
                        }
                    }

                }
            }



        }
        private static bool TestServersConnection(object serverSettings)
        {
            ServerSettings _serverSettings = (ServerSettings)serverSettings;

            string _connection_string = GetConnectionString(_serverSettings.SQLServer, _serverSettings.AuthenticationMode, _serverSettings.SQLUser, _serverSettings.SQLPassword);

            string _server_name = string.Empty;
            //_server_name = _serverSettings.SQLServer.Replace("]", string.Empty);
            //_server_name = _serverSettings.SQLServer.Replace("[", string.Empty);

            if (IsConnectionSuccess(_connection_string, _server_name, master))
            {
                //WriteToLog(_server_name, master, "Error", "Connection failed... skipping this server");
                //FailedServersList.Add(new FailedServers { server_name = _server_name })
                return true;
            }
            else
            {
                return false;
            }

            //if (!TestServerConnection(_connection_string, _server_name, master))
            //{
            //    WriteToLog(_server_name, master, "Error", "Connection failed... skipping this server");
            //    FailedServersList.Add(new FailedServers { server_name = _server_name });
            //}
        }
        private static bool IsConnectionSuccess(string connectionString, string server, string database)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = new SqlCommand("SELECT @@spid;", connection))
                    {
                        command.CommandTimeout = 10;
                        command.CommandType = CommandType.Text;

                        command.Connection.Open();
                        int _spid = Convert.ToInt32(command.ExecuteScalar());
                        command.Connection.Close();

                        return true;
                    }
                }
            }
            catch (Exception e) { WriteToLog(server, database, "Error", e); return false; throw; }
        }
        private static bool TestServerConnection(string connectionString, string server, string database)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = new SqlCommand("SELECT @@spid;", connection))
                    {
                        command.CommandTimeout = 7;
                        command.CommandType = CommandType.Text;

                        command.Connection.Open();
                        int _spid = Convert.ToInt32(command.ExecuteScalar());
                        command.Connection.Close();

                        return true;
                    }
                }
            }
            catch (Exception e) { WriteToLog(server, database, "Error", e); return false; throw; }

        }
        private static string FixServerName(string server)
        {
            // Replace the back slash for a named instance so that the name can be used as a valid name for a folder name
            string _serverFixedName = server.Trim();
            if (_serverFixedName.Contains("\\")) _serverFixedName = _serverFixedName.Replace("\\", "$");

            // Remove the qualification
            _serverFixedName = _serverFixedName.Replace("[", string.Empty);
            _serverFixedName = _serverFixedName.Replace("]", string.Empty);
            return _serverFixedName;
        }
        private static string FixDatabaseName(string database)
        {
            // Remove the qualification
            string _databaseFixedName = database.Trim();
            _databaseFixedName = _databaseFixedName.Replace("[", string.Empty);
            _databaseFixedName = _databaseFixedName.Replace("]", string.Empty);
            return _databaseFixedName;
        }
        private static string GetObjectNameByType(string object_type)
        {
            string object_name = string.Empty;
            switch (object_type)
            {
                case "U": { object_name = "Tables"; } break;
                case "V": { object_name = "Views"; } break;
                case "P": { object_name = "Procedures"; } break;
                case "C": { object_name = "Checks"; } break;
                case "FK": { object_name = "ForeignKeys"; } break;
                case "TR": { object_name = "Triggers"; } break;
                case "PS": { object_name = "PartitionSchemas"; } break;
                case "PF": { object_name = "PartitionFunctions"; } break;
                case "I": { object_name = "Indexes"; } break;
                case "A": { object_name = "Assemblies"; } break;
                case "FN": { object_name = "Functions"; } break;
                case "UDDT": { object_name = "UserDefinedDataTypes"; } break;
                case "UDTT": { object_name = "UserDefinedTableTypes"; } break;
                case "UDT": { object_name = "UserDefinedTypes"; } break;
                case "SN": { object_name = "Synonyms"; } break;
                case "FG": { object_name = "FileGroups"; } break;
                case "PG": { object_name = "PlanGuides"; } break;
                case "RL": { object_name = "Roles"; } break;
                case "SC": { object_name = "Schemas"; } break;
                case "US": { object_name = "Users"; } break;

                case "DT": { object_name = "DDLDatabaseTriggers"; } break;

                case "J": { object_name = "Jobs"; } break;
                case "LS": { object_name = "LinkedServers"; } break;
                case "PA": { object_name = "ProxyAccounts"; } break;
                case "ST": { object_name = "DDLServerTriggers"; } break;
                case "CR": { object_name = "Credentials"; } break;
                case "L": { object_name = "Logins"; } break;

            }
            return object_name;
        }
        private static void Script(string server_padded, string database_padded, Server server, Database database, string[] object_types, string server_path, bool script_server_level_objects, string connection_string)
        {
            try
            {
                foreach (string type in object_types)
                {
                    string object_type = type.Trim().ToUpper();
                    if (!String.IsNullOrWhiteSpace(object_type))
                    {
                        switch (object_type)
                        {
                            case "ALL":
                                {
                                    // Database level objects
                                    object_type = "U";
                                    ScriptTables(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "V";
                                    ScriptViews(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "P";
                                    ScriptProcedures(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                    object_type = "TR";
                                    ScriptTriggers(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "PS";
                                    ScriptPartitionScemas(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "PF";
                                    ScriptPartitionFunctions(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                    object_type = "A";
                                    ScriptAssemblies(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "FN";
                                    ScriptFunctions(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                    object_type = "SN";
                                    ScriptSynonyms(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "FG";
                                    ScriptFileGroups(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type), connection_string);
                                    object_type = "PG";
                                    ScriptPlanGuides(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                    object_type = "RL";
                                    ScriptRoles(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "SC";
                                    ScriptSchemas(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "US";
                                    ScriptUsers(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                    object_type = "UDDT";
                                    ScriptUserDefinedDataTypes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "UDTT";
                                    ScriptUserDefinedTableTypes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                    object_type = "UDT";
                                    ScriptUserDefinedTypes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                    object_type = "DT";
                                    ScriptDDLDatabaseTriggers(server_padded, database_padded, server, database, server_path, GetObjectNameByType(object_type));

                                    if (script_server_level_objects)
                                    {
                                        // Server level objects
                                        object_type = "J";
                                        ScriptJobs(server_padded, msdb, server, database, server_path, GetObjectNameByType(object_type));
                                        object_type = "LS";
                                        ScriptLinkedServers(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                        object_type = "PA";
                                        ScriptProxyAccounts(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                        object_type = "ST";
                                        ScriptDDLServerTriggers(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                        object_type = "L";
                                        ScriptLogins(server_padded, master, server_path, GetObjectNameByType(object_type), connection_string);
                                        object_type = "CR";
                                        ScriptCredentials(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                        object_type = "ST";
                                        ScriptDDLServerTriggers(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                    }
                                }
                                break;
                            case "U":
                                {
                                    ScriptTables(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "V":
                                {
                                    ScriptViews(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "P":
                                {
                                    ScriptProcedures(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "C":
                                {
                                    ScriptChecks(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "FK":
                                {
                                    ScriptForeignKeys(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "TR":
                                {
                                    ScriptTriggers(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "PS":
                                {
                                    ScriptPartitionScemas(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "PF":
                                {
                                    ScriptPartitionFunctions(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "I":
                                {
                                    ScriptIndexes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "A":
                                {
                                    ScriptAssemblies(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "FN":
                                {
                                    ScriptFunctions(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));

                                }
                                break;
                            case "UDDT":
                                {
                                    ScriptUserDefinedDataTypes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "UDTT":
                                {
                                    ScriptUserDefinedTableTypes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "UDT":
                                {
                                    ScriptUserDefinedTypes(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "SN":
                                {
                                    ScriptSynonyms(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "FG":
                                {
                                    ScriptFileGroups(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type), connection_string);
                                }
                                break;
                            case "PG":
                                {
                                    ScriptPlanGuides(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "RL":
                                {
                                    ScriptRoles(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "SC":
                                {
                                    ScriptSchemas(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;
                            case "US":
                                {
                                    ScriptUsers(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;


                            case "DT":
                                {
                                    ScriptDDLDatabaseTriggers(server_padded, database_padded, server, database, server_path, GetObjectNameByType(object_type));
                                }
                                break;

                            case "NFR":
                                {
                                    NFR_Triggers(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type), true);
                                    NFR_Foreignkeys(server_padded, database_padded, database, server_path, GetObjectNameByType(object_type), true);
                                }
                                break;




                            // Server level objects
                            case "J":
                                {
                                    if (script_server_level_objects)
                                    {
                                        ScriptJobs(server_padded, msdb, server, database, server_path, GetObjectNameByType(object_type));
                                    }
                                }
                                break;
                            case "LS":
                                {
                                    if (script_server_level_objects)
                                    {
                                        ScriptLinkedServers(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                    }
                                }
                                break;
                            case "PA":
                                {
                                    if (script_server_level_objects)
                                    {
                                        ScriptProxyAccounts(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                    }
                                }
                                break;
                            case "ST":
                                {
                                    if (script_server_level_objects)
                                    {
                                        ScriptDDLServerTriggers(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                    }
                                }
                                break;
                            case "CR":
                                {
                                    if (script_server_level_objects)
                                    {
                                        ScriptCredentials(server_padded, master, server, database, server_path, GetObjectNameByType(object_type));
                                    }
                                }
                                break;
                            case "L":
                                {
                                    if (script_server_level_objects)
                                    {
                                        ScriptLogins(server_padded, master, server_path, GetObjectNameByType(object_type), connection_string);
                                    }
                                }
                                break;

                            default:
                                {
                                    WriteToLog(server_padded, database_padded, "Error", "Unsupported object type");
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
        }
        private static void ScriptTables(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Tables(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Tables(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptViews(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Views(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Views(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptProcedures(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Procedures(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Procedures(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptChecks(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Checks(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Checks(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptForeignKeys(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                ForeignKeys(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                ForeignKeys(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptPartitionScemas(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                PartitionScemas(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                PartitionScemas(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptTriggers(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Triggers(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Triggers(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptPartitionFunctions(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                PartitionFunctions(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                PartitionFunctions(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptIndexes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Indexes(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Indexes(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptAssemblies(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Assemblies(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Assemblies(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptUserDefinedDataTypes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                UserDefinedDataTypes(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                UserDefinedDataTypes(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptUserDefinedTableTypes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                UserDefinedTableTypes(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                UserDefinedTableTypes(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptFunctions(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Functions(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Functions(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptUserDefinedTypes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                UserDefinedTypes(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                UserDefinedTypes(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptSynonyms(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Synonyms(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Synonyms(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptFileGroups(string server_padded, string database_padded, Database database, string server_path, string object_type, string connection_string)
        {
            if (ScriptOneFilePerObjectType)
            {
                FileGroups(server_padded, database_padded, server_path, object_type, connection_string, true);
            }
            else
            {
                FileGroups(server_padded, database_padded, server_path, object_type, connection_string);
            }
        }
        private static void ScriptPlanGuides(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                PlanGuides(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                PlanGuides(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptSchemas(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Schemas(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Schemas(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptRoles(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Roles(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Roles(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptUsers(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Users(server_padded, database_padded, database, server_path, object_type, true);
            }
            else
            {
                Users(server_padded, database_padded, database, server_path, object_type);
            }
        }
        private static void ScriptDDLDatabaseTriggers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                DDLDatabaseTriggers(server_padded, database_padded, server, database, server_path, object_type, true);
            }
            else
            {
                DDLDatabaseTriggers(server_padded, database_padded, server, database, server_path, object_type);
            }
        }
        private static void ScriptJobs(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Jobs(server_padded, database_padded, server, database, server_path, object_type, true);
            }
            else
            {
                Jobs(server_padded, database_padded, server, database, server_path, object_type);
            }
        }
        private static void ScriptLinkedServers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                LinkedServers(server_padded, master, server, database, server_path, object_type, true);
            }
            else
            {
                LinkedServers(server_padded, master, server, database, server_path, object_type);
            }
        }
        private static void ScriptDDLServerTriggers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                DDLServerTriggers(server_padded, master, server, database, server_path, object_type, true);
            }
            else
            {
                DDLServerTriggers(server_padded, master, server, database, server_path, object_type);
            }
        }
        private static void ScriptProxyAccounts(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                ProxyAccounts(server_padded, master, server, database, server_path, object_type, true);
            }
            else
            {
                ProxyAccounts(server_padded, master, server, database, server_path, object_type);
            }
        }
        private static void ScriptCredentials(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            if (ScriptOneFilePerObjectType)
            {
                Credentials(server_padded, master, server, database, server_path, object_type, true);
            }
            else
            {
                Credentials(server_padded, master, server, database, server_path, object_type);
            }
        }
        private static void ScriptLogins(string server_padded, string database_padded, string server_path, string object_type, string connection_string)
        {
            object_type = "L";

            // If the 2 Microsoft procedures do not exist on the server then we create them
            string _procedure = "sp_hexadecimal";
            if (!IsProcedureExists(server_padded, database_padded, server_path, GetObjectNameByType(object_type), connection_string, _procedure))
            {
                string _command = "CREATE PROCEDURE sp_hexadecimal @binvalue varbinary(256), @hexvalue varchar (514) OUTPUT AS DECLARE @charvalue varchar (514),@i int, @length int, @hexstring char(16) SELECT @charvalue = '0x', @i = 1, @length = DATALENGTH (@binvalue), @hexstring = '0123456789ABCDEF' WHILE (@i <= @length) BEGIN; DECLARE @tempint int, @firstint int, @secondint int; SELECT @tempint = CONVERT(int, SUBSTRING(@binvalue,@i,1)), @firstint = FLOOR(@tempint/16), @secondint = @tempint - (@firstint*16), @charvalue = @charvalue +  SUBSTRING(@hexstring, @firstint+1, 1) +  SUBSTRING(@hexstring, @secondint+1, 1), @i = @i + 1 END SELECT @hexvalue = @charvalue;";
                CreateProcedure(server_padded, database_padded, server_path, GetObjectNameByType(object_type), connection_string, _command);
            }

            _procedure = "sp_help_revlogin_SQLScripter";
            if (!IsProcedureExists(server_padded, database_padded, server_path, GetObjectNameByType(object_type), connection_string, _procedure))
            {
                string _command = "CREATE PROCEDURE sp_help_revlogin_SQLScripter @login_name sysname = NULL AS DECLARE @name sysname, @type varchar (1), @hasaccess int, @denylogin int, @is_disabled int, @PWD_varbinary  varbinary (256), @PWD_string  varchar (514), @SID_varbinary varbinary (85), @SID_string varchar (514), @tmpstr  varchar (1024), @is_policy_checked varchar (3), @is_expiration_checked varchar (3),@defaultdb sysname; CREATE TABLE #Data([text] varchar(max)); INSERT #Data([text]) SELECT ''; IF (@login_name IS NULL) DECLARE login_curs CURSOR FOR SELECT p.sid, p.name, p.type, p.is_disabled, p.default_database_name, l.hasaccess, l.denylogin FROM sys.server_principals p LEFT JOIN sys.syslogins l ON ( l.name = p.name ) WHERE p.type IN ('S', 'G', 'U') AND p.name <> 'sa' ELSE DECLARE login_curs CURSOR FOR SELECT p.sid, p.name, p.type, p.is_disabled, p.default_database_name, l.hasaccess, l.denylogin FROM sys.server_principals p LEFT JOIN sys.syslogins l ON ( l.name = p.name ) WHERE p.type IN ( 'S', 'G', 'U' ) AND p.name = @login_name OPEN login_curs; FETCH NEXT FROM login_curs INTO @SID_varbinary, @name, @type, @is_disabled, @defaultdb, @hasaccess, @denylogin; IF (@@fetch_status = -1) BEGIN;  PRINT 'No login(s) found.'; CLOSE login_curs; DEALLOCATE login_curs; RETURN -1;END; SET @tmpstr = '/* sp_help_revlogin script '; UPDATE #Data SET [Text] = [Text] + ' ' + @tmpstr; SET @tmpstr = '** Generated ' + CONVERT (varchar, GETDATE()) + ' on ' + @@SERVERNAME + ' */'; UPDATE #Data SET [Text] = [Text] + ' ' + @tmpstr; PRINT ''; WHILE (@@fetch_status <> -1) BEGIN; IF (@@fetch_status <> -2)  BEGIN;  PRINT ''; SET @tmpstr = '-- Login: ' + @name; UPDATE #Data SET [Text] = [Text] + ' ' + @tmpstr; IF (@type IN ( 'G', 'U')) BEGIN /* NT authenticated account/group */ SET @tmpstr = 'CREATE LOGIN ' + QUOTENAME( @name ) + ' FROM WINDOWS WITH DEFAULT_DATABASE = [' + @defaultdb + ']'; END; ELSE BEGIN /* SQL Server authentication obtain password and sid */ SET @PWD_varbinary = CAST( LOGINPROPERTY( @name, 'PasswordHash' ) AS varbinary (256) ); EXEC sp_hexadecimal @PWD_varbinary, @PWD_string OUT; EXEC sp_hexadecimal @SID_varbinary,@SID_string OUT; /*obtain password policy state */ SELECT @is_policy_checked = CASE is_policy_checked WHEN 1 THEN 'ON' WHEN 0 THEN 'OFF' ELSE NULL END FROM sys.sql_logins WHERE name = @name; SELECT @is_expiration_checked = CASE is_expiration_checked WHEN 1 THEN 'ON' WHEN 0 THEN 'OFF' ELSE NULL END FROM sys.sql_logins WHERE name = @name; SET @tmpstr = 'CREATE LOGIN ' + QUOTENAME( @name ) + ' WITH PASSWORD = ' + @PWD_string + ' HASHED, SID = ' + @SID_string + ', DEFAULT_DATABASE = [' + @defaultdb + ']'; IF ( @is_policy_checked IS NOT NULL ) BEGIN; SET @tmpstr = @tmpstr + ', CHECK_POLICY = ' + @is_policy_checked; END; IF ( @is_expiration_checked IS NOT NULL ) BEGIN; SET @tmpstr = @tmpstr + ', CHECK_EXPIRATION = ' + @is_expiration_checked; END; END;  IF (@denylogin = 1) BEGIN; /* login is denied access */ SET @tmpstr = @tmpstr + '; DENY CONNECT SQL TO ' + QUOTENAME( @name ); END; ELSE IF (@hasaccess = 0) BEGIN; /* login exists but does not have access */ SET @tmpstr = @tmpstr + '; REVOKE CONNECT SQL TO ' + QUOTENAME( @name ); END;    IF (@is_disabled = 1)  BEGIN; /* login is disabled */ SET @tmpstr = @tmpstr + '; ALTER LOGIN ' + QUOTENAME( @name ) + ' DISABLE'; END;    UPDATE #Data SET [Text] = [Text] + ' ' + @tmpstr; END; FETCH NEXT FROM login_curs INTO @SID_varbinary, @name, @type, @is_disabled, @defaultdb, @hasaccess, @denylogin;  END; CLOSE login_curs; DEALLOCATE login_curs; select * from #Data; RETURN 0;";
                CreateProcedure(server_padded, database_padded, server_path, GetObjectNameByType(object_type), connection_string, _command);
            }

            Logins(server_padded, master, server_path, GetObjectNameByType(object_type), connection_string);
        }
        private static void Views(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (View v in database.Views)
            {
                try
                {
                    if (!v.IsSystemObject)
                    {
                        StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, v.Name);
                        writer.Write(GetGenericInformation(server_padded, database_padded));
                        StringCollection sc = v.Script(so);
                        bool create_database = true;

                        foreach (string s in sc)
                        {
                            if (create_database)
                            {
                                writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                create_database = false;
                            }
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                        }
                        writer.Close();
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Views(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Views");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (View v in database.Views)
            {
                if (!v.IsSystemObject)
                {
                    try
                    {
                        StringCollection sc = v.Script(so);
                        foreach (string s in sc)
                        {
                            if (!s.StartsWith("SET"))
                            {
                                writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                                writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                                i++;
                            }
                        }
                        writer.Flush();
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                        {
                            WriteToLog(server_padded, database_padded, "Error", e.Message);
                        }
                        else
                        {
                            WriteToLog(server_padded, database_padded, "Error", e);
                        }
                    }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Procedures(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (StoredProcedure p in database.StoredProcedures)
            {
                try
                {
                    if (!p.IsSystemObject)
                    {
                        if (p.IsEncrypted)
                        {
                            WriteToLog(server_padded, database_padded, "Info", string.Format("{0} is Encrypted.", p.Name));
                        }
                        else
                        {
                            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, p.Name);
                            writer.Write(GetGenericInformation(server_padded, database_padded));
                            StringCollection sc = p.Script(so);
                            bool create_database = true;

                            foreach (string s in sc)
                            {
                                if (create_database)
                                {
                                    writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                    create_database = false;
                                }
                                writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                            }
                            writer.Close();
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Procedures(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Procedures");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (StoredProcedure p in database.StoredProcedures)
            {
                try
                {
                    if (!p.IsSystemObject)
                    {
                        if (p.IsEncrypted)
                        {
                            WriteToLog(server_padded, database_padded, "Info", string.Format("{0} is Encrypted.", p.Name));
                        }
                        else
                        {
                            StringCollection sc = p.Script(so);
                            foreach (string s in sc)
                            {
                                if (!s.StartsWith("SET"))
                                {
                                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                                    writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                                    i++;
                                }
                            }
                            writer.Flush();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void FileGroups(string server_padded, string database_padded, string server_path, string object_type, string connection_string)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));
            try
            {
                string _command = string.Format("USE [{0}]; SELECT groupname FROM sys.sysfilegroups WHERE groupname NOT LIKE 'PRIMARY';", database_padded.Trim());
                DataSet _ds = SqlClient.ExecuteDataset(connection_string, _command);

                foreach (DataRow row in _ds.Tables[0].Rows)
                {
                    string _file_group = row["groupname"].ToString();
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, _file_group);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));

                    string s = (string.Format("IF NOT EXISTS (SELECT groupname FROM sys.sysfilegroups WHERE groupname = '{0}') "
                        + Environment.NewLine + " ALTER DATABASE [{1}] ADD FILEGROUP [{0}];", _file_group, database_padded.Trim()));

                    writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    writer.Close();
                }
            }
            catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void FileGroups(string server_padded, string database_padded, string server_path, string object_type, string connection_string, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));
            try
            {
                StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "FileGroups");
                writer.Write(GetGenericInformation(server_padded, database_padded));
                writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));

                string _command = string.Format("USE [{0}]; SELECT groupname FROM sys.sysfilegroups WHERE groupname NOT LIKE 'PRIMARY';", database_padded.Trim());
                DataSet _ds = SqlClient.ExecuteDataset(connection_string, _command);

                foreach (DataRow row in _ds.Tables[0].Rows)
                {
                    string _file_group = row["groupname"].ToString();

                    string _fg = (string.Format("IF NOT EXISTS (SELECT groupname FROM sys.sysfilegroups WHERE groupname = '{0}') "
                        + Environment.NewLine + " ALTER DATABASE [{1}] ADD FILEGROUP [{0}];", _file_group, database_padded.Trim()));

                    writer.WriteLine(_fg.TrimEnd() + Environment.NewLine + "GO");
                    writer.Flush();
                }
                writer.Close();
            }
            catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Roles(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (DatabaseRole r in database.Roles)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, r.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = r.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Roles(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Roles");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (DatabaseRole r in database.Roles)
            {
                try
                {
                    StringCollection sc = r.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET") /* && ! s.Contains("[public]") */ )
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Schemas(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (Schema ch in database.Schemas)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, ch.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = ch.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Schemas(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Schemas");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Schema ch in database.Schemas)
            {
                try
                {
                    StringCollection sc = ch.Script(so);

                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                            i++;
                        }
                    }
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Users(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (User u in database.Users)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, u.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = u.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Users(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Users");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (User u in database.Users)
            {
                try
                {
                    StringCollection sc = u.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Functions(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (UserDefinedFunction f in database.UserDefinedFunctions)
            {
                try
                {
                    if (!f.IsSystemObject)
                    {
                        if (f.IsEncrypted)
                        {
                            WriteToLog(server_padded, database_padded, "Info", string.Format("{0} is Encrypted.", f.Name));
                        }
                        else
                        {
                            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, f.Name);
                            writer.Write(GetGenericInformation(server_padded, database_padded));
                            StringCollection sc = f.Script(so);
                            bool use_database = true;

                            foreach (string s in sc)
                            {
                                if (use_database)
                                {
                                    writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                    use_database = false;
                                }
                                writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                            }
                            writer.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Functions(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Functions");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (UserDefinedFunction f in database.UserDefinedFunctions)
            {
                try
                {
                    if (!f.IsSystemObject)
                    {
                        if (f.IsEncrypted)
                        {
                            WriteToLog(server_padded, database_padded, "Info", string.Format("{0} is Encrypted.", f.Name));
                        }
                        else
                        {
                            StringCollection sc = f.Script(so);
                            writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                            foreach (string s in sc)
                            {
                                if (!s.StartsWith("SET"))
                                {
                                    writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                                }
                            }
                            i++;
                            writer.Flush();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void PlanGuides(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (PlanGuide p in database.PlanGuides)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, p.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = p.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void PlanGuides(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "PlanGuides");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (PlanGuide p in database.PlanGuides)
            {
                try
                {
                    StringCollection sc = p.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Logins(string server_padded, string database_padded, string server_path, string object_type, string connection_string)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            string _command = "sp_help_revlogin_SQLScripter";
            string _text = string.Empty;
            string _replacment_text = string.Empty;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Logins");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);

            try
            {
                SqlDataReader _reader = SqlClient.ExecuteReader(connection_string, CommandType.StoredProcedure, _command);
                _reader.Read();

                if (_reader.HasRows)
                {
                    _text = _reader.GetString(0);

                    // Finding the initial multi comment placed by the sql method
                    int endPos1 = _text.IndexOf(@"*/");
                    _replacment_text = _text.Substring(endPos1 + 3);

                    // Format the string so that each CREATE LOGIN statment is a new line.
                    _replacment_text = _replacment_text.Replace("CREATE", Environment.NewLine + "CREATE");
                    _replacment_text = _replacment_text.Replace("--", Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine + "--");

                    writer.WriteLine(_replacment_text);
                    //writer.Flush();
                    //fs.Seek(Pos, SeekOrigin.Begin);
                    //writer.WriteLine("                                  " + "\r\n\r\n");
                    writer.Close();
                }
                _reader.Close();
                WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
            }
            catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
        }
        private static bool IsProcedureExists(string server_padded, string database_padded, string server_path, string object_type, string connection_string, string procedure)
        {
            try
            {
                string _command = string.Format("SET NOCOUNT ON; IF EXISTS(SELECT * FROM master.sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{0}]') AND type in (N'P', N'PC')) SELECT 1 ELSE SELECT 0;", procedure);

                int _i = (int)SqlClient.ExecuteScalar(connection_string, CommandType.Text, _command);

                if (_i == 1) { return true; } else { return false; }
            }
            catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); return false; }
        }
        private static void CreateProcedure(string server_padded, string database_padded, string server_path, string object_type, string connection_string, string command)
        {
            try
            {
                string _command = command;
                SqlClient.ExecuteNonQuery(connection_string, CommandType.Text, _command);
            }
            catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); throw; }
        }
        private static void UserDefinedTableTypes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (UserDefinedTableType f in database.UserDefinedTableTypes)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, f.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = f.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void UserDefinedTableTypes(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "UserDefinedTableTypes");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (UserDefinedTableType t in database.UserDefinedTableTypes)
            {
                try
                {
                    StringCollection sc = t.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void UserDefinedTypes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            foreach (UserDefinedType t in database.UserDefinedTypes)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = t.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void UserDefinedTypes(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "UserDefinedTypes");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (UserDefinedType t in database.UserDefinedTypes)
            {
                try
                {
                    StringCollection sc = t.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Synonyms(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (Synonym sn in database.Synonyms)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, sn.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = sn.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Synonyms(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Synonyms");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Synonym sn in database.Synonyms)
            {
                try
                {
                    StringCollection sc = sn.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void UserDefinedDataTypes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (UserDefinedDataType t in database.UserDefinedDataTypes)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = t.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void UserDefinedDataTypes(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "UserDefinedDataTypes");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (UserDefinedDataType t in database.UserDefinedDataTypes)
            {
                try
                {
                    StringCollection sc = t.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Assemblies(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (SqlAssembly a in database.Assemblies)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, a.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = a.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Assemblies(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Assemblies");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (SqlAssembly a in database.Assemblies)
            {
                try
                {
                    StringCollection sc = a.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void UserDefinedAggregates(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (UserDefinedAggregate a in database.UserDefinedAggregates)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, a.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = a.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Jobs(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (Job j in server.JobServer.Jobs)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, j.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = j.Script(so);
                    bool create_database = true;

                    foreach (string s in sc)
                    {
                        if (create_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            create_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();

                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Jobs(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Jobs");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Job j in server.JobServer.Jobs)
            {
                try
                {
                    StringCollection sc = j.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("cannot be scripted as its data is not accessible"))
                    {
                        WriteToLog(server_padded, database_padded, "Error", e.Message);
                    }
                    else
                    {
                        WriteToLog(server_padded, database_padded, "Error", e);
                    }
                }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void LinkedServers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (LinkedServer ls in server.LinkedServers)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, ls.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = ls.Script(so);
                    bool create_database = true;

                    foreach (string s in sc)
                    {
                        if (create_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            create_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void LinkedServers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "LinkedServers");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (LinkedServer ls in server.LinkedServers)
            {
                try
                {
                    StringCollection sc = ls.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Credentials(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Credentials");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Credential c in server.Credentials)
            {
                try
                {
                    StringCollection sc = c.EnumLogins(); // Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Credentials(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Credentials");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Credential c in server.Credentials)
            {
                try
                {
                    StringCollection sc = c.EnumLogins(); // Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void DDLServerTriggers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (ServerDdlTrigger tr in server.Triggers)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, tr.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = tr.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void DDLServerTriggers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "DDLServerTriggers");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (ServerDdlTrigger tr in server.Triggers)
            {
                try
                {
                    StringCollection sc = tr.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void ProxyAccounts(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (ProxyAccount pa in server.JobServer.ProxyAccounts /* Credential ls in server.Credentials */ )
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, pa.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = pa.Script(so);
                    bool use_database = true;

                    foreach (string s in sc)
                    {
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void ProxyAccounts(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "ProxyAccounts");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (ProxyAccount pa in server.JobServer.ProxyAccounts /* Credential ls in server.Credentials */ )
            {
                try
                {
                    StringCollection sc = pa.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void PartitionScemas(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (PartitionScheme ps in database.PartitionSchemes)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, ps.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = ps.Script(so);
                    bool create_database = true;

                    foreach (string s in sc)
                    {
                        if (create_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            create_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void PartitionScemas(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "PartitionScemas");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (PartitionScheme ps in database.PartitionSchemes)
            {
                try
                {
                    StringCollection sc = ps.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void PartitionFunctions(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (PartitionFunction pf in database.PartitionFunctions)
            {
                try
                {
                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, pf.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    StringCollection sc = pf.Script(so);
                    bool create_database = true;

                    foreach (string s in sc)
                    {
                        if (create_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            create_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO");
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void PartitionFunctions(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "PartitionFunctions");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (PartitionFunction pf in database.PartitionFunctions)
            {
                try
                {
                    StringCollection sc = pf.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Tables(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AllowSystemObjects = false;
            so.AnsiFile = true;
            so.ExtendedProperties = true;

            so.DriForeignKeys = false;
            so.DriDefaults = true;
            so.DriChecks = true;

            so.DriPrimaryKey = true;
            so.DriUniqueKeys = true;
            so.ClusteredIndexes = true;
            so.NonClusteredIndexes = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Tables");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                if (!t.IsSystemObject)
                {
                    try
                    {
                        StringCollection sc = t.Script(so);
                        writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                        foreach (string s in sc)
                        {
                            if (!s.StartsWith("SET"))
                            {
                                writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                            }
                        }
                        i++;
                        writer.Flush();
                    }
                    catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Tables(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AllowSystemObjects = false;
            so.AnsiFile = true;
            so.DriAll = true;
            //so.DriIndexes = true;
            so.DriPrimaryKey = true;
            so.DriUniqueKeys = true;
            so.ClusteredIndexes = true;
            so.NonClusteredIndexes = true;

            foreach (Table t in database.Tables)
            {
                if (!t.IsSystemObject)
                {
                    try
                    {
                        StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name);
                        writer.Write(GetGenericInformation(server_padded, database_padded));
                        StringCollection sc = t.Script(so);
                        bool use_database = true;

                        foreach (string s in sc)
                        {
                            if (use_database)
                            {
                                writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                use_database = false;
                            }
                            writer.WriteLine(s.TrimEnd() + ";" + Environment.NewLine + "GO");
                        }
                        writer.Close();
                    }
                    catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
                }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Checks(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;
            so.DriChecks = true;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Checks.Count > 0)
                    {
                        StringCollection sc = t.Script(so);
                        bool create_database = true;

                        foreach (string s in sc)
                        {
                            // Cut the ALTER TABLE ADD CONSTRAINT command from the entire table script
                            if (s.Contains("ADD"))
                            {
                                // Get the word that follows CONSTRAINT which is the constraint name so that we add it to the file name for uniqunes
                                string name = GetNextWord(s, "CONSTRAINT");
                                StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name + "--" + name);
                                writer.Write(GetGenericInformation(server_padded, database_padded));
                                if (create_database)
                                {
                                    writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                    create_database = false;
                                }
                                writer.WriteLine(s.TrimEnd() + ";" + Environment.NewLine + "GO");
                                writer.Close();
                            }
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Checks(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;
            so.DriChecks = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Checks");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Checks.Count > 0)
                    {
                        StringCollection sc = t.Script(so);

                        foreach (string s in sc)
                        {
                            // Cut the ALTER TABLE ADD CONSTRAINT command from the entire table script
                            if (s.Contains("ADD"))
                            {
                                writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                                if (!s.StartsWith("SET"))
                                {
                                    writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                                }
                                i++;
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void DDLDatabaseTriggers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            foreach (DatabaseDdlTrigger dt in database.Triggers)
            {
                try
                {
                    StringCollection sc = dt.Script(so);
                    bool use_database = true;

                    StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, dt.Name);
                    writer.Write(GetGenericInformation(server_padded, database_padded));
                    foreach (string s in sc)
                    {
                        //if (s.Contains("TRIGGER"))
                        //{
                        // Get the word that follows CONSTRAINT which is the constraint name so that we add it to the file name for uniqunes
                        //string name = GetNextWord(s, "TRIGGER");
                        //StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name + "--" + name);
                        if (use_database)
                        {
                            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                            use_database = false;
                        }
                        writer.WriteLine(s.TrimEnd() + ";" + Environment.NewLine + "GO");
                        //}
                    }
                    writer.Close();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void DDLDatabaseTriggers(string server_padded, string database_padded, Server server, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "DDLDatabaseTriggers");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (DatabaseDdlTrigger dt in database.Triggers)
            {
                try
                {
                    StringCollection sc = dt.Script(so);
                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                    foreach (string s in sc)
                    {
                        if (!s.StartsWith("SET"))
                        {
                            writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                        }
                    }
                    i++;
                    writer.Flush();
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void ForeignKeys(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;
            so.DriForeignKeys = true;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.ForeignKeys.Count > 0)
                    {
                        StringCollection sc = t.Script(so);
                        bool create_database = true;

                        foreach (string s in sc)
                        {
                            // Cut the ALTER TABLE ADD CONSTRAINT command from the entire table script
                            if (s.Contains("ADD"))
                            {
                                // Get the word that follows CONSTRAINT which is the constraint name so that we add it to the file name for uniqunes
                                string name = GetNextWord(s, "CONSTRAINT");
                                StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name + "--" + name);
                                writer.Write(GetGenericInformation(server_padded, database_padded));
                                if (create_database)
                                {
                                    writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                    //create_database = false;
                                }
                                writer.WriteLine(s.TrimEnd() + ";" + Environment.NewLine + "GO");
                                writer.Close();
                            }
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void ForeignKeys(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AllowSystemObjects = false;
            so.AnsiFile = true;
            so.DriForeignKeys = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Foreignkeys");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                if (!t.IsSystemObject && t.ForeignKeys.Count > 0)
                {
                    try
                    {
                        StringCollection sc = t.Script(so);
                        writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                        foreach (string s in sc)
                        {
                            if (!s.StartsWith("SET") && !s.Contains("CREATE TABLE"))
                            {
                                writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                            }
                        }
                        i++;
                        writer.Flush();
                    }
                    catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Triggers(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;
            so.Triggers = true;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Triggers.Count > 0)
                    {
                        StringCollection sc = t.Script(so);
                        foreach (string s in sc)
                        {
                            if (s.Contains("TRIGGER"))
                            {
                                // Get the word that follows CONSTRAINT which is the constraint name so that we add it to the file name for uniqunes
                                string name = GetNextWord(s, "TRIGGER");
                                StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name + "--" + name);
                                writer.Write(GetGenericInformation(server_padded, database_padded));
                                writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                writer.WriteLine(s.TrimEnd() + ";" + Environment.NewLine + "GO");
                                writer.Close();
                            }
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void Triggers(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;
            so.Triggers = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Triggers");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Triggers.Count > 0)
                    {
                        StringCollection sc = t.Script(so);
                        foreach (string s in sc)
                        {
                            if (s.Contains("TRIGGER"))
                            {
                                writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                                if (!s.StartsWith("SET"))
                                {
                                    writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                                }
                                i++;
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static void NFR_Triggers(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", "NFR_Triggers"));

            ScriptingOptions so = new ScriptingOptions();
            so.AnsiFile = true;
            so.AllowSystemObjects = false;
            so.Triggers = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Triggers");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Triggers.Count > 0)
                    {
                        StringCollection sc = t.Script(so);
                        foreach (string s in sc)
                        {
                            if (s.Contains("TRIGGER"))
                            {
                                writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                                if (!s.StartsWith("SET"))
                                {
                                    string temp_string = s.ToUpper();
                                    int idx1 = temp_string.IndexOf("CREATE");
                                    // Check if we have the NOT FOR REPLICATION definition in the body after the CREATE
                                    int pos = temp_string.IndexOf("NOT FOR REPLICATION", idx1);
                                    if (pos == -1)
                                    {
                                        // Get the position where we ned to insert the "NOT FOR REPLICATION"
                                        string str = s.Substring(0, s.Length - idx1);
                                        int idx2 = str.IndexOf("AS");

                                        // Cut the first part and the second part
                                        string first_string = s.Substring(0, idx2);
                                        string second_string = s.Substring(idx2, s.Length - idx2);

                                        //writer.WriteLine(string.Format("{0} NOT FOR REPLICATION {1}"), first_string, second_string);
                                        writer.WriteLine(first_string + "NOT FOR REPLICATION " + Environment.NewLine + second_string);
                                    }
                                    else
                                    {
                                        // Trigger is already marked as "NOT FOR REPLICATION" 
                                        writer.WriteLine(s.Trim());
                                    }
                                    writer.WriteLine("GO" + Environment.NewLine + Environment.NewLine);
                                }
                                i++;
                                writer.Flush();
                            }
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", "NFR_Triggers"));
        }
        private static void NFR_Foreignkeys(string server_padded, string database_padded, Database database, string server_path, string object_type, bool single_file)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", "NFR_Foreignkeys"));

            ScriptingOptions so = new ScriptingOptions();
            so.AllowSystemObjects = false;
            so.AnsiFile = true;
            so.DriForeignKeys = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Foreignkeys");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                if (!t.IsSystemObject && t.ForeignKeys.Count > 0)
                {
                    try
                    {
                        StringCollection sc = t.Script(so);
                        foreach (string s in sc)
                        {
                            if (!s.StartsWith("SET") && !s.StartsWith("CREATE"))
                            {
                                if (s.Contains("ADD"))
                                {
                                    writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");
                                    i++;
                                    if (s.Contains("REPLICATION"))
                                    {
                                        // FK is already marked as "NOT FOR REPLICATION" 
                                        writer.WriteLine(s.Trim());
                                    }
                                    else
                                    {
                                        // Add "NOT FOR REPLICATION" 
                                        writer.WriteLine(s + " NOT FOR REPLICATION;");
                                    }
                                }
                                else
                                {
                                    // Check the constraint
                                    writer.WriteLine(s.Trim());
                                }
                                writer.WriteLine("GO" + Environment.NewLine);
                            }
                        }

                        writer.Flush();
                    }
                    catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
                }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", "NFR_Foreignkeys"));
        }
        private static void Indexes(string server_padded, string database_padded, Database database, string server_path, string object_type, bool file_per_index)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AllowSystemObjects = false;
            so.AnsiFile = true;

            so.DriPrimaryKey = true;
            so.DriUniqueKeys = true;
            so.ClusteredIndexes = true;
            so.NonClusteredIndexes = true;

            StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, "Indexes");
            writer.Write(GetGenericInformation(server_padded, database_padded));
            writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO" + Environment.NewLine));
            writer.WriteLine("SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;" + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
            int i = 1;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Indexes.Count > 0)
                    {
                        foreach (Index idx in t.Indexes)
                        {
                            StringCollection sc = idx.Script(so);
                            writer.WriteLine("-- " + i.ToString() + Environment.NewLine + "GO");

                            foreach (string s in sc)
                            {
                                if (!s.StartsWith("SET"))
                                {
                                    writer.WriteLine(s.TrimEnd() + Environment.NewLine + "GO" + Environment.NewLine + Environment.NewLine);
                                }
                            }
                            i++;
                            writer.Flush();
                        }
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            writer.Close();
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        // Script all table indexes to a single file named under the table name
        private static void Indexes(string server_padded, string database_padded, Database database, string server_path, string object_type)
        {
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0}...", object_type));

            ScriptingOptions so = new ScriptingOptions();
            so.AllowSystemObjects = false;
            so.AnsiFile = true;
            //so.DriIndexes = true;
            //so.DriNonClustered = true;
            so.DriPrimaryKey = true;
            so.DriUniqueKeys = true;
            so.ClusteredIndexes = true;
            so.NonClusteredIndexes = true;

            foreach (Table t in database.Tables)
            {
                try
                {
                    if (!t.IsSystemObject && t.Indexes.Count > 0)
                    {
                        StreamWriter writer = CreateStreamWriter(server_path, server_padded, database_padded, object_type, t.Name);
                        writer.Write(GetGenericInformation(server_padded, database_padded));
                        foreach (Index i in t.Indexes)
                        {
                            StringCollection sc = i.Script(so);
                            foreach (string s in sc)
                            {
                                if (s.Contains("CREATE NONCLUSTERED") || s.Contains("CREATE CLUSTERED") || s.Contains("ADD  CONSTRAINT"))
                                {
                                    writer.WriteLine(string.Format("USE {0}; {1}", database_padded.TrimEnd(), Environment.NewLine + "GO"));
                                    writer.WriteLine(s.TrimEnd() + ";" + Environment.NewLine + "GO");
                                }
                            }
                        }
                        writer.Close();
                    }
                }
                catch (Exception e) { WriteToLog(server_padded, database_padded, "Error", e); }
            }
            WriteToLog(server_padded, database_padded, "Info", string.Format("Scripting {0} completed", object_type));
        }
        private static string GetNextWord(string search_string, string string_to_find)
        {
            string results = string.Empty;
            string[] words = search_string.Split(new char[] { ' ' });

            for (int i = 0; i < words.Length - 1; i++)
            {
                if (words[i].Equals(string_to_find))
                {
                    results = (words[i + 1]);
                    // Remove dbo. + CHAR 10, 13 + TAB
                    results = RemoveSpecialCharacters(results);
                }
            }
            return results;
        }
        private static string RemoveSpecialCharacters(string input)
        {
            Regex r = new Regex(@"\s|(dbo)|\[|\]|\.|:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            return r.Replace(input, String.Empty);
        }
        private static StreamWriter CreateStreamWriter(string server_path, string server, string database, string object_type, string file_name)
        {
            string _server = FixServerName(server);
            string _database = FixDatabaseName(database);

            try
            {
                string _current_path = server_path + @"\" + _database + @"\" + object_type;

                if (!Directory.Exists(_current_path))
                    Directory.CreateDirectory(_current_path);

                // Replace the back slash with a dollar on named instances
                if (file_name.Contains(@"\"))
                {
                    file_name = file_name.Replace(@"\", "$");
                }
                // Replace ilegeal char
                if (file_name.Contains(":"))
                {
                    file_name = file_name.Replace(":", "");
                }

                //file_name = RemoveSpecialCharacters(file_name);

                string _file_full_name = Path.Combine(_current_path, file_name + ".sql");


                StreamWriter writer = new StreamWriter(_file_full_name);
                return writer;
            }
            catch (Exception e) { WriteToLog(_server, _database, "Error", e); return null; throw; }
        }
        private static string GetGenericInformation(string server, string database)
        {
            try
            {
                StringBuilder _sb = new StringBuilder();
                _sb.AppendLine("/*");
                _sb.AppendLine(string.Format("  {0} {1}" + " Edition Version {2}", ApplicationName, Edition, MyAssembly));
                _sb.AppendLine(string.Format("  Scripted on SQL Server instance {0} at {1}", server.TrimEnd(), DateTime.Now.ToString()));
                _sb.AppendLine("*/");
                _sb.AppendLine("");

                return _sb.ToString();
            }
            catch (Exception e) { WriteToLog(server, database, "Error", e); return null; }
        }
        private static string TimeStamp()
        {
            // Format the date and time part for the script file name
            DateTimeFormatInfo dtf = new DateTimeFormatInfo();
            string _ts = DateTime.Now.ToString(dtf.SortableDateTimePattern);
            _ts = _ts.Replace("-", string.Empty);
            _ts = _ts.Replace(":", string.Empty);
            _ts = _ts.Replace("T", string.Empty);

            return _ts;
        }
        private static string GetConnectionString(string server, bool windows_authentication, string sql_user, string sql_passowrd)
        {
            string connection_string = string.Empty;

            try
            {
                if (!windows_authentication)
                {
                    connection_string = string.Format("data source={0}; initial catalog=master; Application Name={3}; User ID={1};Password={2}; persist security info=False;", server, sql_user, sql_passowrd, ApplicationName);
                }
                else
                {
                    connection_string = string.Format("data source={0}; initial catalog=master; Application Name={1}; integrated security=SSPI; persist security info=False;", server, ApplicationName);
                }
            }
            catch (Exception e) { WriteToLog("", "", "Error", e); }
            return connection_string;
        }
        private static void SetConsole()
        {
            System.Console.Title = ApplicationName;

            if (Environment.UserInteractive)
            {
                //System.Console.WindowHeight = 45;
                //System.Console.WindowWidth = 140;
                System.Console.BufferHeight = 9999;
            }
        }
        public static void WriteToLog(string server, string database, string severity, string message)
        {
            //, %-10message
            string method = string.Empty;

            if (severity == "Error")
            {
                StackTrace stackTrace = new StackTrace();
                method = stackTrace.GetFrame(1).GetMethod().Name.ToString().Trim();

                log.Error(server + " " + database + "  " + method + " - " + message);
            }

            if (severity == "Info")
            {
                log.Info(server + " " + database + " " + method + " " + message);
            }

            if (severity == "Debug")
            {
                log.Debug(server + " " + database + " " + method + " " + message);
            }
        }

        public static void WriteToLog(string server, string database, string severity, Exception e)
        {
            //, %-10message
            string method = string.Empty;

            if (severity == "Error")
            {
                StackTrace stackTrace = new StackTrace();
                method = stackTrace.GetFrame(1).GetMethod().Name.ToString().Trim();

                log.Error(server + " " + database + "  " + method + " - " + e.ToString());
            }

            if (severity == "Info")
            {
                log.Info(server + " " + database + " " + method + " " + e.ToString());
            }

            if (severity == "Debug")
            {
                log.Debug(server + " " + database + " " + method + " " + e.ToString());
            }
        }
        private static string GetAssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }
        private static void GetAppConfig()
        {
            try
            {
                ZipFolder = Convert.ToBoolean(ConfigurationManager.AppSettings["ZipFolder"]);
                ZipPassword = ConfigurationManager.AppSettings["ZipPassword"].ToString();
                OutputFolder = ConfigurationManager.AppSettings["OutputFolder"].ToString();
                DeleteOutputFolderAfterZip = Convert.ToBoolean(ConfigurationManager.AppSettings["DeleteOutputFolderAfterZip"]);
                DaysToKeepFilesInOutputFolder = Convert.ToInt32(ConfigurationManager.AppSettings["DaysToKeepFilesInOutputFolder"]);
                ScriptOneFilePerObjectType = Convert.ToBoolean(ConfigurationManager.AppSettings["ScriptOneFilePerObjectType"]);

            }
            catch (Exception e) { WriteToLog("", master, "Error", e); throw; }
        }
        private static void ExitApplication(int exit_code)
        {
            if (Environment.UserInteractive)
            {
                Console.WriteLine("Press any key to exit...");
                Console.Read();
            }
            Environment.Exit(exit_code);
        }
        // Pad empty spaces to the right of the string so all strings are aligned
        private static string AlignString(string str, int len)
        {
            string s = string.Empty;
            try
            {
                s = str.PadRight(len, ' ');
                return s;
            }
            catch (Exception e) { throw e; };
        }
        //private static string GetEdition()
        //{
        //    string edition = string.Empty;

        //    if (!(!IsFreeWare || IsStandard)) { return (edition = "Freeware"); }
        //    if (!(IsFreeWare || !IsStandard)) { return (edition = "Standard"); }
        //    if (!(IsFreeWare || IsStandard))  { return (edition = "Enterprise");}
        //    if (IsFreeWare && IsStandard)
        //    {
        //        WriteToLog("", "", "Info", "Invalid edition. Please contact us at support@sqlserverutilities.com");
        //        ExitApplication(1);
        //    }
        //    return edition;
        //}
        public static bool ZipIt(string Path, string outPathAndZipFile, string password)
        {
            //bool rc = true;
            try
            {
                string OutPath = outPathAndZipFile;
                ArrayList ar = GenerateFileList(Path); // generate file list

                // find number of chars to remove from orginal file path
                int TrimLength = (Directory.GetParent(Path)).ToString().Length;
                //TrimLength += 1; //remove '\'
                FileStream ostream;
                byte[] obuffer;

                ZipOutputStream oZipStream = new ZipOutputStream(System.IO.File.Create(OutPath)); // create zip stream

                if (password != String.Empty) oZipStream.Password = password;
                oZipStream.SetLevel(9); // 9 = maximum compression level
                ZipEntry oZipEntry;
                foreach (string Fil in ar) // for each file, generate a zipentry
                {
                    oZipEntry = new ZipEntry(Fil.Remove(0, TrimLength));
                    oZipStream.PutNextEntry(oZipEntry);

                    if (!Fil.EndsWith(@"/"))  // if a file ends with '/' its a directory
                    {
                        ostream = File.OpenRead(Fil);
                        obuffer = new byte[ostream.Length]; // byte buffer
                        ostream.Read(obuffer, 0, obuffer.Length);
                        oZipStream.Write(obuffer, 0, obuffer.Length);
                        //Console.Write(".");
                        ostream.Close();
                    }
                }
                oZipStream.Finish();
                oZipStream.Close();
            }
            catch (Exception e) { WriteToLog("", "", "Error", e); }
            return true;
        }
        private static ArrayList GenerateFileList(string Dir)
        {
            System.Collections.ArrayList mid = new ArrayList();
            bool Empty = true;
            foreach (string file in Directory.GetFiles(Dir)) // add each file in directory
            {
                mid.Add(file);
                Empty = false;
            }

            if (Empty)
            {
                if (Directory.GetDirectories(Dir).Length == 0) // if directory is completely empty, add it
                {
                    mid.Add(Dir + @"/");
                }
            }
            foreach (string dirs in Directory.GetDirectories(Dir)) // do this recursively
            {
                // set up the excludeDir test
                string testDir = dirs.Substring(dirs.LastIndexOf(@"\") + 1).ToUpper();
                foreach (object obj in GenerateFileList(dirs))
                {
                    mid.Add(obj);
                }
            }
            return mid; // return file list          
        }
        private static void DeleteOutputFolder(string path, string server)
        {
            try
            {
                Directory.Delete(path, true);
                //WriteToLog(server, master, "Info", string.Format("Deleting folder {0}...", path));
            }
            catch (Exception e) { WriteToLog(server, master, "Error", e); }
        }
        public static string ProductName
        {
            get
            {
                AssemblyProductAttribute myProduct = (AssemblyProductAttribute)AssemblyProductAttribute.GetCustomAttribute(Assembly.GetExecutingAssembly(),
                                 typeof(AssemblyProductAttribute));
                return myProduct.Product;
            }
        }
        private static bool IsTrailExpired(string value)
        {
            try
            {
                string s = value.Substring(12, value.Length - 12);
                s = s.Substring(0, s.Length - 6);

                DateTime _trial_value = Number2Date(s);
                DateTime _now = DateTime.Now;
                DateTime _trial_trial_expiration_value = _trial_value.AddDays(30);

                TimeSpan ts = _trial_trial_expiration_value - _now;
                int _remaining_days_ = Convert.ToInt32(ts.TotalDays);

                if (_remaining_days_ < 0)
                {
                    WriteToLog("", "", "Info", string.Format("{0} Trial has expired ", ApplicationName));
                    return true;
                }
                WriteToLog("", "", "Info", string.Format("{0} Trial will expire in {1} days", ApplicationName, _remaining_days_.ToString()));
            }
            catch (Exception e) { WriteToLog("", master, "Error", e); }
            return false;
        }
        private static DateTime Number2Date(string strNum)
        {
            int iDay, iMon, iYear;
            DateTime ValidDate = DateTime.Now;

            //string day, month, year;

            //month = strNum.Substring(0, 2);
            //day = strNum.Substring(2, 2);
            //year = strNum.Substring(4, 2);

            try
            {
                iMon = Convert.ToInt32(strNum.Substring(0, 2));
                iDay = Convert.ToInt32(strNum.Substring(2, 2));
                iYear = Convert.ToInt32(strNum.Substring(4, 4));
                ValidDate = new DateTime(iYear, iMon, iDay);
            }
            catch (Exception e) { WriteToLog("", "", "Error", e); }
            return ValidDate;
        }

        private static void WriteHeader()
        {
            WriteToLog("", "", "Info", "");
            WriteToLog("", "", "Info", "---------------------------------------");
            WriteToLog("", "", "Info", string.Format("{0} {1} Edition Version {2}", ApplicationName, Edition, MyAssembly));
            WriteToLog("", "", "Info", "Copyright (C) 2006-2024 www.sqlserverutilities.com. All rights reserved.");
        }



    }


}
