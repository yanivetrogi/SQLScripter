# Database Context for Server-Level Objects

## Summary

The SQLScripter correctly assigns the appropriate database context (`USE` statement) for all server-level objects:

## Database Assignments

### `USE [msdb]` - SQL Agent Objects

These objects are stored in the `msdb` system database:

1. **Jobs** (`ScriptJobs`)
   - SQL Server Agent jobs
   - Schedules, steps, and notifications

2. **ProxyAccounts** (`ScriptProxyAccounts`)
   - SQL Server Agent proxy accounts
   - Used for job step execution under different credentials

### `USE [master]` - Server-Level Objects

These objects are stored in the `master` system database:

1. **LinkedServers** (`ScriptLinkedServers`)
   - Linked server definitions
   - Remote server connections

2. **Credentials** (`ScriptCredentials`)
   - Server-level credentials
   - Authentication information for external resources

3. **ServerDdlTriggers** (`ScriptServerDdlTriggers`)
   - Server-scoped DDL triggers
   - Respond to server-level events

### `USE [DatabaseName]` - Database-Level Objects

These objects use the specific database they belong to:

1. **Tables** - User database
2. **Views** - User database
3. **Procedures** - User database
4. **Functions** - User database
5. **Triggers** (table triggers) - User database
6. **Indexes** - User database
7. **ForeignKeys** - User database
8. **Checks** - User database
9. **Synonyms** - User database
10. **UserDefinedTypes** - User database
11. **UserDefinedTableTypes** - User database
12. **UserDefinedDataTypes** - User database
13. **Assemblies** - User database
14. **FileGroups** - User database
15. **PartitionSchemes** - User database
16. **PartitionFunctions** - User database
17. **PlanGuides** - User database
18. **Schemas** - User database
19. **Roles** - User database
20. **Users** - User database
21. **DatabaseDdlTriggers** - User database

## Implementation Status

✅ **All objects correctly use the appropriate database context**

### Code Verification

- Jobs: Lines 1122, 1154 → `USE [msdb]`
- ProxyAccounts: Lines 2144, 2176 → `USE [msdb]`
- LinkedServers: Lines 2001, 2033 → `USE [master]`
- Credentials: Lines 2069, 2105 → `USE [master]`
- ServerDdlTriggers: Lines 2212, 2244 → `USE [master]`

## SQL Server System Database Reference

### msdb

- Purpose: SQL Server Agent database
- Contains: Jobs, alerts, operators, backup/restore history, maintenance plans, proxy accounts
- Objects scripted here: Jobs, ProxyAccounts

### master

- Purpose: System database for server-wide configuration
- Contains: Logins, linked servers, server configuration, system stored procedures
- Objects scripted here: LinkedServers, Credentials, ServerDdlTriggers

### User Databases

- Purpose: Application and user data
- Contains: Tables, views, procedures, functions, and all database-scoped objects
- Objects scripted here: All database-level objects (25 types)

## Best Practices

1. **Jobs and Proxy Accounts** → Always create in `msdb`
2. **Linked Servers and Credentials** → Always create in `master`
3. **Database Objects** → Always create in the target user database
4. **Deployment Scripts** → Ensure correct `USE` statement before object creation

## Notes

- The implementation correctly handles all database contexts
- No changes needed - the code already follows SQL Server best practices
- Each scripted file includes the appropriate `USE [database]` statement
- This ensures scripts can be executed safely without manual database switching
