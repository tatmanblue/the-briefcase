# Goal

The goal is to create a MCP server for agents to read, request, search and update files not in the agents working directory. Version 1 only supports local file system.

The server will find files based on environment variable that contains one or more paths (local to the MCP server not the agent).  

The server will support the following operations:
- Read file
- List Files available
- send agents updates on file changes (File Change Notifications)

These features will be saved for a future version:
- Search for files (searches for files based on name and content)
- Update file

The MCP server will not expose direct file system access to agents. Instead, it will provide an API for agents to interact with files in a controlled manner. 

The MCP server will be designed to be extensible, allowing for future support of additional file systems (e.g., cloud storage) and additional operations.

The MCP server will not leak any information about the underlying file system to agents. Agents will only be able to interact with files through the API provided by the MCP server.  
So it may need to maintain a mapping of file identifiers to actual file paths on the server. This data should persist across restarts (so agents can hold onto IDs).  The path for storing this 
information will be found from an environment varaible.

MCP servers typically run over stdio (for local CLI/IDE tools).  Version 1 will support stdio.  

# Directories
- `src/`: Source code for the project.
- `docs/`: Documentation for the project.

# Technology

C#, latest

use Microsoft.Extensions.AI.Templates for project templates and structure.  eg: `dotnet new mcpserver -n Briefcase` to create the project structure.

use DotNetEnv for environment variable management.

use dotnet file watchers for file change notifications.

do not use _ as a prefix for class level variables.  

constants should be in all caps with underscores between words.

# Instructions for Claude
All changes must be approved before creating these changes. Please prepare a plan of proposed changes and get confirmation before proceeding.


