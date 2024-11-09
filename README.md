**General**

SQLScripter is a utility that scripts SQL Server databases, jobs,  logins and any other sql server object. 
The utility is designed to automate the process of schema generation making the schema available on the file system for instant usage and in addition have the schema available at different points in time like a "version history".

You can script a single server or multiple servers from a single location.
Add an sql server agent job with a job step type of cmd exec and provide the full path to _SQLScripter.exe_

**Configuration**

There are 2 configuration files to edit:
1. SQLScripter.exe.config
2. Servers.config (located under the Configuration folder)

_SQLScripter.exe.config_ defines configuration at the global scope (for all servers).

_Servers.config_ provides configuration per each server that you script. 

You can copy and **duplicate** the _ServerSettings_ section where each _ServerSettings_ section represents a server to be scripted (so if you have 3 _ServerSettings_ sections you are about to script 3 servers).


For additional information please visit the product page at:  https://sqlserverutilities.com/sqlscripter/

**History**

I have coded the first version of _SQLScripter.exe_ in 2006 to meet a requirement that when we failover from the main sql server which was a transactional replication publisher to the subscriber all required objects (jobs, logins etc.) exists on the file system of the subscriber server.

In 2009 I have packed it and sold it on my website.

In Nov. 2024 I have changed the repo to be Public.






