# SQLScripter v4.3.0.0

A powerful, secure, and zero-config SQL Server database scripting tool for .NET 8

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)

---

## 📖 Overview

SQLScripter is a professional-grade command-line tool that generates SQL Server schema scripts for SQL Server databases. It supports scripting tables, views, stored procedures, functions, indexes, logins, credentials jobs and many other database objects across multiple servers concurrently.

### ✨ Key Features

- 🔒 **Zero-Config Authentication** - Automatic resolution of SQL, Windows, or Impersonated identities
- 🛡️ **Secure Credential Storage** - Windows DPAPI encryption for all account types
- ⚡ **Multi-threaded Processing** - Process multiple servers concurrently (up to 100 threads)
- 📦 **ZIP Compression** - Optional password-protected ZIP archives
- 🎯 **Selective Scripting** - Choose specific databases and object types
- 🧹 **Automatic Cleanup** - Automatic retention management for old scripts and archives
- 📝 **Advanced Logging** - Global and per-server console output & color configuration
- 🔄 **Modern Architecture** - Built on .NET 8 with async/await patterns

---

## 🚀 Quick Start

### Basic Usage

1. **Configure servers** in `appsettings.json` (just the server name!):

   ```json
   {
     "Servers": [{ "SQLServer": "SQLSERVER01" }]
   }
   ```

2. **Add credentials** (optional - only if current user doesn't have access):

   ```bash
   # For SQL Login
   SQLScripter.exe add SQLSERVER01 sa YourPassword
   
   # For Domain Service Account (Impersonation)
   SQLScripter.exe add SQLSERVER01 DOMAIN\svcacct YourPassword win
   ```

3. **Run** the tool:

   ```bash
   SQLScripter.exe
   ```

---

## 🔧 Configuration

Configuration is now incredibly streamlined.

### Simple Configuration Example

```json
{
  "SQLScripter": {
    "OutputFolder": "c:\\sqlscripter_out",
    "ZipFolder": true,
    "MaxConcurrentThreads": 10
  },
  "Servers": [
    {
      "SQLServer": "SQLSERVER01",
      "Databases": "all"
    }
  ]
}
```

### Application Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `OutputFolder` | `c:\sqlscripter_out` | Output directory for scripts |
| `ScriptOneFilePerObjectType` | `false` | One file per object type vs. per object |
| `ZipFolder` | `true` | Create ZIP archive of output |
| `ZipPassword` | - | Password for ZIP archive (optional) |
| `DeleteOutputFolderAfterZip` | `false` | Delete folder after creating ZIP |
| `MaxConcurrentThreads` | `25` | Max concurrent server connections (1-100) |
| `DaysToKeepFilesInOutputFolder` | `180` | Automatic cleanup of folders and ZIPs older than X days |

### Server Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `SQLServer` | - | Server name or instance (e.g., `SERVER01\INST`) |
| `Databases` | `all` | Databases to script (semicolon-separated or `all`) |
| `ObjectTypes` | `all` | Object types to script (semicolon-separated or `all`) |
| `WriteToConsole` | `false` | Print progress for this server to console |
| `ConsoleForeGroundColour` | `White` | Color for this server's console output |

### 🛠️ Advanced CLI Overrides

SQLScripter provides a robust CLI for ad-hoc tasks. You can override any `appsettings.json` configuration directly:

```bash
# 1. Target a specific server and database (ignores config list)
SQLScripter.exe --server PROD-SQL01 --database MarketingDB

# 2. Script only specific object types to a custom folder
SQLScripter.exe --types "Tables;Views;Procedures" --output "D:\SQL_Exports"

# 3. High-speed parallel run (50 threads) with ZIP disabled
SQLScripter.exe --threads 50 --zip false

# 4. Target multiple databases on one server
SQLScripter.exe --server "DEV-SQL" --database "Northwind;Pubs;AppLogs"

# 5. Legacy Mode: Quick script of specific types for ALL configured servers
SQLScripter.exe Tables Views Procedures
```

### 🔐 Credential Management

Manage encrypted credentials safely from the CLI:

```bash
# Add a SQL login
SQLScripter.exe add SERVER01 sa MyPassword

# Add a Windows Account for Impersonation
SQLScripter.exe add SERVER01 "DOMAIN\svc_sql" "SecretPass" win

# List all stored credentials
SQLScripter.exe list

# Remove a specific credential
SQLScripter.exe remove SERVER01
```

Type `SQLScripter.exe --help` for the full list of flags.

## 💡 Common Scenarios

### Automated Nightly Backup

Run via Windows Task Scheduler to back up all databases to a central share:
`SQLScripter.exe --output "\\BackupServer\SQL_Scripts" --zip true`

### Quick Table Export for Developer

Just grab tables for a specific project:
`SQLScripter.exe --server PROD-DB --database "ProjectAlpha" --types "Tables" --zip false`

---

## 🔒 Automated Identity Resolution

SQLScripter follows a "Smart Choice" logic for every server:

1. **Stored SQL Account?** ➡️ Connect using SQL Auth.
2. **Stored Windows Account?** ➡️ Automatically impersonate domain user ➡️ Connect.
3. **No Stored Account?** ➡️ Connect using your current identity.

📖 **Full Security Guide:** [Docs/CREDENTIALS_README.md](Docs/CREDENTIALS_README.md)

---

## 📊 Supported Object Types

| Code | Object Type | Code | Object Type |
| :--- | :--- | :--- | :--- |
| `TABLES` | Tables (U) | `INDEXES` | Indexes (I) |
| `VIEWS` | Views (V) | `FOREIGNKEYS` | Foreign Keys (F) |
| `PROCEDURES` | Stored Procedures (P) | `TRIGGERS` | Triggers (TR) |
| `FUNCTIONS` | Functions (FN) | `JOBS` | SQL Agent Jobs |

*(And many more... type `all` to script everything)*

---

## 🛠️ Windows Event Log Setup

SQLScripter logs its lifecycle (Starts, Success, and Fatal Errors) to the Windows Application Event Log under the source `SQLScripter`.

Because Windows requires Administrative privileges to register a new Event Source for the first time, you have two options for deployment:

### Option 1: One-Time Admin Run

Run the application as **Administrator** once on the target machine. It will automatically detect the missing source and register it.

```cmd
# Run as Admin
SQLScripter.exe --help
```

### Option 2: DevOps / PowerShell (Recommended)

Use the included `Register-EventSource.ps1` script during your deployment or server setup phase. This script must be run with elevated permissions.

```powershell
.\Register-EventSource.ps1
```

Once registered, the application will log events even when run by non-administrative service accounts.

---

## 🏗️ Architecture

- **.NET 8.0** - Modern cross-platform framework core
- **SMO** - Latest SQL Server Management Objects
- **Zero-Config** - Identity-aware service architecture
- **DPAPI** - Enterprise-grade encryption

---

## 📚 Further Documentation

- **[Zero-Config Security Guide](Docs/CREDENTIALS_README.md)** - Detailed look at DPAPI and Impersonation.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

---

**Version:** 4.3.0.0 (2026-02-03)  
**Made with ❤️ for SQL Server DBAs**
