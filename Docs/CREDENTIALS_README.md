# SQLScripter v4.3.0.0 Zero-Config Credentials Management

## Overview

SQLScripter now features a **Zero-Configuration Authentication** model. You no longer need to specify authentication modes in your configuration files—the tool automatically resolves the correct identity for every server.

## 🔒 Security Features

- **DPAPI Encryption**: Credentials are encrypted using Windows DPAPI.
- **Machine-Specific**: Credentials can only be decrypted on the same machine.
- **No Plain Text**: Passwords are never stored in config files.
- **Automatic Resolution**: The tool smart-detects whether to use SQL, Windows Impersonation, or your current identity.

## 🚀 Quick Start

### Step 1: Add Credentials (Optional)

If a server requires specific credentials, add them once to the secure store:

```powershell
# SQL Authentication
.\SQLScripter.exe add SQLSERVER01 sa MyPassword123

# Windows Authentication (Impersonation)
.\SQLScripter.exe add PROD-SQL01 DOMAIN\ServiceAccount MyPassword123 win
```

### Step 2: Configure Servers

Your `appsettings.json` is now incredibly simple. Just list the servers:

```json
{
  "Servers": [
    {
      "SQLServer": "SQLSERVER01",
      "Databases": "all"
    },
    {
      "SQLServer": "PROD-SQL01",
      "Databases": "master,msdb"
    }
  ]
}
```

### Step 3: Run SQLScripter

The tool executes the "Automatic Choice" logic:

```text
SQLSERVER01     Authenticating via stored SQL credentials
PROD-SQL01      Impersonating Windows user: DOMAIN\ServiceAccount
L1000124133     Authenticating via current Windows identity (Integrated Security)
```

## 📖 Credentials Manager Commands

### Add or Update Credentials

```powershell
.\SQLScripter.exe add <server> <username> <password> [sql|win]
```

- **sql**: For SQL Server Authentication logins.
- **win**: For Windows Domain identity swapping (Impersonation).

### List/Remove Credentials

```powershell
.\SQLScripter.exe list
.\SQLScripter.exe remove <server>
```

## ⚙️ How It Works: The Automatic Choice

For every server in your list, SQLScripter follows this logic:

1. **Check Secure Store**:
   - If a **SQL** credential is found ➡️ Connect using SQL Auth.
   - If a **Windows** credential is found ➡️ Automatically impersonate that domain user ➡️ Connect using Integrated Security.
2. **Default Fallback**:
   - If **NOTHING** is found ➡️ Connect using your current Windows logged-in identity.

## 🛡️ Security Best Practices

- **Zero Config**: Keep `SQLUser` and `SQLPassword` out of your JSON files entirely.
- **Service Accounts**: Use the `win` switch for service accounts to keep their passwords secure but automated.
- **Machine Specific**: Remember that the encrypted credential store is unique to your machine.

---

**Version:** 4.3.0.0  
**Last Updated:** 2026-02-03
