# SQLScripter v4.2.0.0

A powerful, secure, and zero-config SQL Server database scripting tool for .NET 8

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.txt)

---

## 📖 Overview

SQLScripter is a professional-grade command-line tool that generates SQL scripts for SQL Server databases. It supports scripting tables, views, stored procedures, functions, indexes, and many other database objects across multiple servers concurrently.

### ✨ Key Features

- 🔒 **Zero-Config Authentication** - Automatic resolution of SQL, Windows, or Impersonated identities
- 🛡️ **Secure Credential Storage** - Windows DPAPI encryption for all account types
- ⚡ **Multi-threaded Processing** - Process multiple servers concurrently (up to 100 threads)
- 📦 **ZIP Compression** - Optional password-protected ZIP archives
- 🎯 **Selective Scripting** - Choose specific databases and object types
- 📝 **Comprehensive Logging** - Dual output to console and log file
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
| `DaysToKeepFilesInOutputFolder` | `180` | Retention period for old files |

### Server Settings

| Setting | Default | Description |
| :--- | :--- | :--- |
| `SQLServer` | - | Server name or instance (e.g., `SERVER01\INST`) |
| `Databases` | `all` | Databases to script (comma-separated or `all`) |
| `ObjectTypes` | `all` | Object types to script (comma-separated or `all`) |
| `WriteToConsole` | `false` | Print progress for this server to console |
| `ConsoleForeGroundColour` | `White` | Color for this server's console output |

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
|------|-------------|------|-------------|
| `TABLES` | Tables (U) | `INDEXES` | Indexes (I) |
| `VIEWS` | Views (V) | `FOREIGNKEYS` | Foreign Keys (F) |
| `PROCEDURES` | Stored Procedures (P) | `TRIGGERS` | Triggers (TR) |
| `FUNCTIONS` | Functions (FN) | `JOBS` | SQL Agent Jobs |

*(And many more... type `all` to script everything)*

---

## 🏗️ Architecture

- **.NET 8.0** - Modern cross-platform framework core
- **SMO** - Latest SQL Server Management Objects
- **Zero-Config** - Identity-aware service architecture
- **DPAPI** - Enterprise-grade encryption

---

## 📚 Further Documentation

- **[Zero-Config Security Guide](Docs/CREDENTIALS_README.md)** - Detailed look at DPAPI and Impersonation.
- **[Quick Command Reference](Docs/QUICK_REFERENCE.md)** - Fast lookup for build and run commands.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

---

**Version:** 4.2.0.0 (2026-01-30)  
**Made with ❤️ for SQL Server DBAs**
