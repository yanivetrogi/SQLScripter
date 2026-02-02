# ScriptOneFilePerObjectType Feature - Complete Implementation

## Overview

The `ScriptOneFilePerObjectType` configuration option controls how database objects are organized into files.

## Configuration

### appsettings.json

```json
{
  "SQLScripter": {
    "ScriptOneFilePerObjectType": false  // or true
  }
}
```

## Behavior

### When `ScriptOneFilePerObjectType = false` (Default - Recommended)

**Tables:**

- Each table gets its own file: `Schema.TableName.sql`
- **Includes** indexes, foreign keys, check constraints, **and triggers** in the same file
- This is the standard SQL scripting pattern - everything related to a table is together

**Other Objects:**

- Each object gets its own file
- Views: `Schema.ViewName.sql`
- Procedures: `Schema.ProcedureName.sql`
- Functions: `Schema.FunctionName.sql`
- etc.

**Indexes, Foreign Keys, Checks, Triggers:**

- **NOT scripted separately** - they are included with their parent tables
- No separate `Indexes`, `ForeignKeys`, `Checks`, or `Triggers` folders

**Example Output Structure:**

```text
OutputFolder/
├── Tables/
│   ├── dbo.Customers.sql          (includes indexes, FKs, checks, triggers)
│   ├── dbo.Orders.sql             (includes indexes, FKs, checks, triggers)
│   └── dbo.Products.sql           (includes indexes, FKs, checks, triggers)
├── Views/
│   ├── dbo.CustomerOrders.sql
│   └── dbo.ProductSummary.sql
├── StoredProcedures/
│   ├── dbo.GetCustomer.sql
│   └── dbo.UpdateOrder.sql
└── Functions/
    ├── dbo.CalculateTotal.sql
    └── dbo.FormatDate.sql
```

### When `ScriptOneFilePerObjectType = true`

**All Objects:**

- All objects of the same type go into ONE file
- Tables: `Tables.sql` (WITHOUT indexes/FKs/checks/triggers)
- Views: `Views.sql`
- Procedures: `StoredProcedures.sql`
- Functions: `Functions.sql`
- Indexes: `Indexes.sql` (scripted separately)
- Foreign Keys: `ForeignKeys.sql` (scripted separately)
- Checks: `Checks.sql` (scripted separately)
- Triggers: `Triggers.sql` (scripted separately)
- Logins: `Logins.sql` (scripted separately)

**Example Output Structure:**

```text
OutputFolder/
├── Tables/
│   └── Tables.sql                 (all tables, NO indexes/FKs/checks/triggers)
├── Indexes/
│   └── Indexes.sql                (all indexes from all tables)
├── ForeignKeys/
│   └── ForeignKeys.sql            (all foreign keys from all tables)
├── Checks/
│   └── Checks.sql                 (all check constraints from all tables)
├── Triggers/
│   └── Triggers.sql               (all triggers from all tables)
├── Views/
│   └── Views.sql                  (all views)
├── StoredProcedures/
│   └── StoredProcedures.sql       (all procedures)
└── Functions/
    └── Functions.sql              (all functions)
```

## Supported Object Types (27 Total)

### 🔥 Core Database Objects

1. **Tables** - Table definitions
2. **Views** - Database views
3. **Procedures** - Stored procedures
4. **Functions** - User-defined functions
5. **Triggers** - Database triggers ✨ *Improved*

### 🔧 Table Constraints & Indexes

1. **Indexes** - Table indexes
2. **ForeignKeys** - Foreign key constraints
3. **Checks** - Check constraints

### 📦 Data Types & Objects

1. **UserDefinedTypes** - User Defined Types (UDT)
2. **UserDefinedTableTypes** - User Defined Table Types (UDTT)
3. **UserDefinedDataTypes** - User Defined Data Types (UDDT)
4. **Assemblies** - CLR Assemblies
5. **Synonyms** - Database Synonyms

### 🗄️ Storage & Partitioning

1. **FileGroups** - File Groups
2. **PartitionSchemes** - Partition Schemes
3. **PartitionFunctions** - Partition Functions

### 🔐 Security & Access

1. **Schemas** - Database Schemas
2. **Roles** - Database Roles
3. **Users** - Database Users
4. **Credentials** - Server Credentials

### 🌐 Server-Level Objects

1. **Jobs** - SQL Agent Jobs
2. **LinkedServers** - Linked Servers
3. **ProxyAccounts** - SQL Agent Proxy Accounts
4. **Logins** - Server logins ✨ *New*

### ⚡ Advanced Features

1. **PlanGuides** - Plan Guides
2. **ServerDdlTriggers** - Server DDL Triggers
3. **DatabaseDdlTriggers** - Database DDL Triggers

## Recommendations

### Use `ScriptOneFilePerObjectType = false` when

- ✅ You want standard SQL scripting behavior
- ✅ You're using version control (Git, etc.)
- ✅ You want to deploy individual objects
- ✅ Multiple developers work on different objects
- ✅ You want clear diffs in version control
- ✅ You want indexes/FKs/checks with their tables

### Use `ScriptOneFilePerObjectType = true` when

- ✅ You have many objects and want fewer files
- ✅ You're archiving or backing up
- ✅ You want to review all objects of one type together
- ✅ You're generating documentation
- ✅ You want to separate table structure from indexes/constraints

## Implementation Details

### Key Logic

- When `false`: `ScriptTable()` includes `Indexes = true, DriAll = true`
- When `true`: `ScriptTable()` uses `Indexes = false, DriAll = false`
- Separate index/FK/check scripting only runs when `true`

### Code Location

- Configuration: `Models/AppSettings.cs`, `appsettings.json`
- Main Logic: `Services/ScriptingService.cs`
- Entry Point: `Program.cs`

## Version History

- **2026-01-31**: Fixed behavior - indexes/FKs/checks now included with tables when `false`
- **2026-01-30**: Initial implementation of 25 object types
- **2026-01-30**: Added Tables, Views, Procedures, Functions support

## Build Status

✅ Build successful with 0 errors
✅ All 27 object types implemented
✅ Correct behavior for both modes
