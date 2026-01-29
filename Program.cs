using System;
using System.Collections.Generic;
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
        public static int SqlDatabaseMaxNameLength;
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
                string command = args[0].ToLower();
                
                if (command == "add" || command == "remove" || command == "list")
                {
                    HandleCredentialCommand(args);
                    return;
                }
            }

            try
            {
                // Setup services
                ConfigureServices();

                var logger = _serviceProvider.GetRequiredService<ILoggerService>();
                var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
                
                var sqlScripterSection = configuration.GetSection("SQLScripter");
                var appSettings = sqlScripterSection.Get<AppSettings>() ?? new AppSettings();


                string applicationName = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "SQLScripter";
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

                Console.Title = $"{applicationName} v{version}";
                
                // Print header with configuration
                PrintHeader(applicationName, version, appSettings, logger);

                // Load servers from appsettings.json
                var serversList = configuration.GetSection("Servers").Get<List<ServerSettings>>() ?? new List<ServerSettings>();
                
                logger.Info("", "", $"Loaded {serversList.Count} server(s) from configuration");
                
                if (serversList.Count == 0)
                {
                    logger.Error("", "", "No servers configured in appsettings.json", new Exception("Server list is empty"));
                    return;
                }

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
            services.AddSingleton<ILoggerService, LoggerService>();
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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================================");
            Console.WriteLine($"                                {appName} v{version}");            
            Console.WriteLine("===================================================================================");
            Console.ResetColor();
            Console.WriteLine();

            // Log version to file and console
            //logger.Info("", "", $"===============================================================================");
            //logger.Info("", "", $"{appName} v{version} - Migration of Scripting Service - Core Library");
            //logger.Info("", "", $"===============================================================================");
            //logger.Info("", "", "");

            // Print and log configuration
            logger.Info("", "", "Configuration:");
            logger.Info("", "", $"Output Folder:              {settings.OutputFolder}");
            logger.Info("", "", $"Max Concurrent Threads:     {settings.MaxConcurrentThreads}");
            logger.Info("", "", $"Script One File Per Type:   {settings.ScriptOneFilePerObjectType}");
            logger.Info("", "", $"ZIP Output:                 {settings.ZipFolder}");
            if (settings.ZipFolder)
            {
                logger.Info("", "", $"ZIP Password Protected:     {!string.IsNullOrEmpty(settings.ZipPassword)}");
                logger.Info("", "", $"Delete Folder After ZIP:    {settings.DeleteOutputFolderAfterZip}");
            }
            logger.Info("", "", $"Days to Keep Files:         {settings.DaysToKeepFilesInOutputFolder}");
            logger.Info("", "", "");
        }

        private static void HandleCredentialCommand(string[] args)
        {
            var storage = new CredentialsStorage();
            string command = args[0].ToLower();

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
    }
}
