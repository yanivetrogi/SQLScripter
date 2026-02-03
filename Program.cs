using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SQLScripter.Models;
using SQLScripter.Services;
using SQLScripter.Security;
using System.Runtime.Versioning;

namespace SQLScripter
{
    [SupportedOSPlatform("windows")]
    class Program
    {
        public static int SqlServerMaxNameLength;
        public static int SqlDatabaseMaxNameLength = 30;
        private static IServiceProvider _serviceProvider = null!;

        static async Task Main(string[] args)
        {
            // Register encoding provider for .NET Core/.NET 8 compatibility
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // Initialize log4net
            var logRepository = log4net.LogManager.GetRepository(Assembly.GetEntryAssembly()!);
            log4net.Config.XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            // Check for credential management commands
            if (args.Length > 0)
            {
                string cmd = args[0].TrimStart('-').ToLower();
                
                if (cmd == "add" || cmd == "remove" || cmd == "list")
                {
                    HandleCredentialCommand(args);
                    return;
                }

                if (cmd == "h" || cmd == "help" || cmd == "?")
                {
                    ShowHelp();
                    return;
                }
            }

            try
            {
                // Parse command line arguments
                var options = ParseArguments(args);
                if (options.Errors.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    foreach (var err in options.Errors) Console.WriteLine($"Error: {err}");
                    Console.ResetColor();
                    Console.WriteLine("Use --help for usage information.");
                    return;
                }

                // Setup services
                ConfigureServices();

                var logger = _serviceProvider.GetRequiredService<ILoggerService>();
                var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
                
                var sqlScripterSection = configuration.GetSection("SQLScripter");
                var appSettings = sqlScripterSection.Get<AppSettings>() ?? new AppSettings();

                // Apply global overrides from CLI
                if (!string.IsNullOrEmpty(options.OutputFolder)) appSettings.OutputFolder = options.OutputFolder;
                if (options.Threads.HasValue) appSettings.MaxConcurrentThreads = options.Threads.Value;
                if (options.Zip.HasValue) appSettings.ZipFolder = options.Zip.Value;

                string applicationName = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "SQLScripter";
                string version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                                 ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() 
                                 ?? "4.3.0.0";

                Console.Title = $"{applicationName} v{version}";
                
                // Print header with effective configuration
                PrintHeader(applicationName, version, appSettings, logger);

                // Load servers from appsettings.json
                var serversList = configuration.GetSection("Servers").Get<List<ServerSettings>>() ?? new List<ServerSettings>();
                
                // Logic for server filtering or addition
                if (!string.IsNullOrEmpty(options.Server))
                {
                    var existing = serversList.FirstOrDefault(s => s.SQLServer.Equals(options.Server, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        serversList = new List<ServerSettings> { existing };
                        logger.Info("", "", $"Targeting specific server from config: {options.Server}");
                    }
                    else
                    {
                        var newServer = new ServerSettings { SQLServer = options.Server };
                        serversList = new List<ServerSettings> { newServer };
                        logger.Info("", "", $"Targeting ad-hoc server: {options.Server}");
                    }
                }

                // Apply database and object type overrides
                foreach (var server in serversList)
                {
                    if (!string.IsNullOrEmpty(options.Database))
                    {
                        server.Databases = options.Database;
                    }
                    if (!string.IsNullOrEmpty(options.ObjectTypes))
                    {
                        server.ObjectTypes = options.ObjectTypes;
                        logger.Info(server.SQLServer, "", $"Overriding object types: {server.ObjectTypes}");
                    }
                }

                if (serversList.Count == 0)
                {
                    logger.Error("", "", "No servers to process. Check appsettings.json or command line arguments.", new Exception("Server list is empty"));
                    return;
                }

                logger.Info("", "", $"Processing {serversList.Count} server(s)...");

                // Start processing
                var orchestrator = _serviceProvider.GetRequiredService<IOrchestrationService>();
                await orchestrator.ProcessServersAsync(serversList, appSettings, applicationName);

                logger.Info("", "", "Application finished successfully.");
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider?.GetService<ILoggerService>();
                if (logger != null)
                {
                    logger.WriteToLog("", "", "Error", ex);
                }
                else
                {
                    Console.WriteLine($"Fatal Error: {ex.Message}");
                }
                Environment.Exit(1);
            }
        }

        private static void ConfigureServices()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();

            // Add configuration
            services.AddSingleton<IConfiguration>(configuration);

            // Add services
            services.AddSingleton<ILoggerService>(sp => {
                var config = sp.GetRequiredService<IConfiguration>();
                var appSettings = config.GetSection("SQLScripter").Get<AppSettings>() ?? new AppSettings();
                return new LoggerService(appSettings.WriteToConsole, appSettings.ConsoleForeGroundColour);
            });
            services.AddSingleton<IConnectionService, ConnectionService>();
            services.AddSingleton<IFileManagementService, FileManagementService>();
            
            // ScriptingService needs the boolean from config
            services.AddSingleton<IScriptingService>(sp => {
                var config = sp.GetRequiredService<IConfiguration>();
                var appSettings = config.GetSection("SQLScripter").Get<AppSettings>() ?? new AppSettings();
                return new ScriptingService(sp.GetRequiredService<ILoggerService>(), appSettings.ScriptOneFilePerObjectType);
            });
            
            services.AddSingleton<IOrchestrationService, OrchestrationService>();

            _serviceProvider = services.BuildServiceProvider();
        }

        private static void PrintHeader(string appName, string version, AppSettings settings, ILoggerService logger)
        {
            // Print visual header to console only
            if (settings.WriteToConsole)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("===================================================================================");
                Console.WriteLine($"                                {appName} v{version}");            
                Console.WriteLine("===================================================================================");
                Console.ResetColor();
                Console.WriteLine();
            }

            // Print and log configuration
            logger.Info("", "", "Configuration:");
            logger.Info("", "", $"{"Output Folder:",-30} {settings.OutputFolder}");
            logger.Info("", "", $"{"Script One File Per Type:",-30} {settings.ScriptOneFilePerObjectType}");
            logger.Info("", "", $"{"Max Concurrent Threads:",-30} {settings.MaxConcurrentThreads}");

            logger.Info("", "", $"{"ZIP Output:",-30} {settings.ZipFolder}");
            if (settings.ZipFolder)
            {
                logger.Info("", "", $"{"ZIP Password Protected:",-30} {!string.IsNullOrEmpty(settings.ZipPassword)}");
                logger.Info("", "", $"{"Delete Folder After ZIP:",-30} {settings.DeleteOutputFolderAfterZip}");
            }
            logger.Info("", "", $"{"Days to Keep Files:",-30} {settings.DaysToKeepFilesInOutputFolder}");
            logger.Info("", "", "");
        }

        private static void HandleCredentialCommand(string[] args)
        {
            var storage = new CredentialsStorage();
            string command = args[0].TrimStart('-').ToLower();

            try
            {
                switch (command)
                {
                    case "add":
                        if (args.Length < 4 || args.Length > 5)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: Invalid arguments for 'add' command.");
                            Console.ResetColor();
                            Console.WriteLine();
                            Console.WriteLine("Usage: SQLScripter.exe add <server> <username> <password> [sql|win]");
                            Console.WriteLine("Example (SQL):     SQLScripter.exe add SQLSERVER01 sa MyPassword");
                            Console.WriteLine("Example (Windows): SQLScripter.exe add PROD-SQL01 DOMAIN\\user MyPassword win");
                            return;
                        }

                        AuthenticationType authType = AuthenticationType.Sql;
                        if (args.Length == 5)
                        {
                            string type = args[4].ToLower();
                            if (type == "win" || type == "windows")
                                authType = AuthenticationType.Windows;
                            else if (type != "sql")
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Error: Invalid authentication type '{type}'. Use 'sql' or 'win'.");
                                Console.ResetColor();
                                return;
                            }
                        }

                        storage.AddOrUpdateCredential(args[1], args[2], args[3], authType);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Credentials ({authType}) for server '{args[1]}' have been encrypted and saved successfully.");
                        Console.ResetColor();
                        break;

                    case "remove":
                        if (args.Length != 2)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Error: Invalid arguments for 'remove' command.");
                            Console.ResetColor();
                            Console.WriteLine();
                            Console.WriteLine("Usage: SQLScripter.exe remove <server>");
                            Console.WriteLine("Example: SQLScripter.exe remove SQLSERVER01");
                            return;
                        }
                        if (storage.RemoveCredential(args[1]))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✓ Credentials for server '{args[1]}' have been removed successfully.");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠ No credentials found for server '{args[1]}'.");
                            Console.ResetColor();
                        }
                        break;

                    case "list":
                        var credentials = storage.LoadCredentials();
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Stored Credentials:");
                        Console.WriteLine("===================");
                        Console.ResetColor();
                        Console.WriteLine();
                        
                        if (credentials.Count == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("No credentials stored.");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($"{"Server",-35} {"Username",-20} {"Type",-10}");
                            Console.WriteLine(new string('-', 70));
                            Console.ResetColor();
                            
                            foreach (var cred in credentials)
                            {
                                Console.WriteLine($"{cred.Server,-35} {cred.Username,-20} {cred.AuthType,-10}");
                            }
                            
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"Total: {credentials.Count} credential(s)");
                            Console.ResetColor();
                        }
                        Console.WriteLine();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void ShowHelp()
        {
            string appName = Assembly.GetExecutingAssembly().GetName().Name ?? "SQLScripter";
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{appName} - SQL Server Scripting Utility");
            Console.ResetColor();
            Console.WriteLine("Usage:");
            Console.WriteLine($"  {appName}.exe [options]");
            Console.WriteLine($"  {appName}.exe <command> [args]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -s, --server <name>      Target a specific SQL Server");
            Console.WriteLine("  -d, --database <name>    Target a specific database (can be a list: db1;db2)");
            Console.WriteLine("  -t, --types <list>       Object types to script (e.g. Tables;Views;Procedures;Functions;Triggers;Indexes)");
            Console.WriteLine("  -o, --output <path>      Override output folder path");
            Console.WriteLine("  -n, --threads <num>      Max concurrent threads (1-100)");
            Console.WriteLine("  -z, --zip <true|false>   Enable or disable ZIP output");
            Console.WriteLine("  -h, --help               Show this help information");
            Console.WriteLine();
            Console.WriteLine("Legacy Usage (Object Types only):");
            Console.WriteLine($"  {appName}.exe Tables Views");
            Console.WriteLine();
            Console.WriteLine("Credential Commands:");
            Console.WriteLine($"  {appName}.exe add <server> <user> <pass> [sql|win]");
            Console.WriteLine($"  {appName}.exe remove <server>");
            Console.WriteLine($"  {appName}.exe list");
            Console.WriteLine();
        }

        private static CommandLineOptions ParseArguments(string[] args)
        {
            var options = new CommandLineOptions();
            if (args.Length == 0) return options;

            // If the first argument doesn't start with a dash, assume legacy mode (all args are object types)
            if (!args[0].StartsWith("-") && !args[0].StartsWith("/"))
            {
                options.ObjectTypes = string.Join(";", args);
                return options;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                string? value = (i + 1 < args.Length) ? args[i + 1] : null;

                switch (arg)
                {
                    case "-s": case "--server":
                        if (value == null) options.Errors.Add("Server name missing");
                        else { options.Server = value; i++; }
                        break;
                    case "-d": case "--database":
                        if (value == null) options.Errors.Add("Database name missing");
                        else { options.Database = value; i++; }
                        break;
                    case "-t": case "--types":
                        if (value == null) options.Errors.Add("Object types missing");
                        else { options.ObjectTypes = value; i++; }
                        break;
                    case "-o": case "--output":
                        if (value == null) options.Errors.Add("Output path missing");
                        else { options.OutputFolder = value; i++; }
                        break;
                    case "-n": case "--threads":
                        if (int.TryParse(value, out int t)) { options.Threads = t; i++; }
                        else options.Errors.Add("Invalid thread count");
                        break;
                    case "-z": case "--zip":
                        if (bool.TryParse(value, out bool z)) { options.Zip = z; i++; }
                        else options.Errors.Add("Invalid zip value (use true/false)");
                        break;
                    case "-h": case "--help": case "/?":
                        options.ShowHelp = true;
                        break;
                }
            }

            return options;
        }
    }

    public class CommandLineOptions
    {
        public string? Server { get; set; }
        public string? Database { get; set; }
        public string? ObjectTypes { get; set; }
        public string? OutputFolder { get; set; }
        public int? Threads { get; set; }
        public bool? Zip { get; set; }
        public bool ShowHelp { get; set; }
        public List<string> Errors { get; } = new();
    }
}
