# Quick Reference: SQLScripter v4.2.0.0 Commands & Operations

## 🛠️ Build Commands

### Clean and Build (Recommended)

```powershell
dotnet clean
dotnet build
```

### Build for Production

```powershell
dotnet build --configuration Release
```

### Self-Contained Deployment

Includes the .NET 8 runtime so it can run on machines without .NET installed:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

---

## 🔒 Security & Credentials

### Add/Update Credentials

```powershell
# For SQL Server Logins
.\SQLScripter.exe add SERVER_NAME username password

# For Windows Domain Accounts (Impersonation)
.\SQLScripter.exe add SERVER_NAME DOMAIN\user password win
```

### List Stored Credentials

```powershell
.\SQLScripter.exe list
```

### Remove Credentials

```powershell
.\SQLScripter.exe remove SERVER_NAME
```

---

## ⚙️ Configuration

### Key Files

- **`appsettings.json`**: Main configuration for servers and application flags.
- **`log4net.config`**: Advanced logging settings.
- **`Configuration\credentials.bin`**: Encrypted store (automatically managed).

### Simplified `appsettings.json`

```json
{
  "Servers": [
    {
      "SQLServer": "SERVER01",
      "Databases": "all",
      "ObjectTypes": "TABLES,PROCEDURES"
    }
  ]
}
```

---

## 📈 Supported Object Shortcuts

| Code | Type | Code | Type |
| :--- | :--- | :--- | :--- |
| `ALL` | Everything | `SYN` | Synonyms |
| `U` | Tables | `TR` | Triggers |
| `V` | Views | `I` | Indexes |
| `P` | Stored Procedures | `F` | Foreign Keys |
| `FN` | Functions | `SCH` | Schemas |
| `JOBS` | SQL Agent Jobs | `L` | Logins |

---

## 📂 Project Structure (v4.2)

```text
SQLScripterNetCore/
├── Configuration/          # Settings & Models
├── Docs/                  # Documentation
├── Models/                # Data Transfer Objects
├── Security/              # DPAPI & Impersonation logic
├── Services/              # Core Business Logic
│   ├── ConnectionService.cs
│   ├── OrchestrationService.cs
│   └── ScriptingService.cs
├── Program.cs             # Entry point
└── appsettings.json       # App Configuration
```

---

**Version:** 4.2.0.0  
**Last Updated:** 2026-01-30
