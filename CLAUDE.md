# Goal

The goal is to create a MCP server for agents to read, request, search and update files not in the agents working directory. Version 1 only supports local file system.

The server will find files based on environment variable that contains one or more paths (local to the MCP server not the agent).

The server will support the following operations:
- Read file
- List Files available
- Send agents updates on file changes (File Change Notifications)

These features will be saved for a future version:
- Search for files (searches for files based on name and content)
- Update file

The MCP server will not expose direct file system access to agents. Instead, it will provide an API for agents to interact with files in a controlled manner.

The MCP server will be designed to be extensible, allowing for future support of additional file systems (e.g., cloud storage) and additional operations.

The MCP server will not leak any information about the underlying file system to agents. Agents will only be able to interact with files through the API provided by the MCP server.
Files are identified by stable GUIDs that persist across server restarts (stored in `registry.json`). The path for storing this information comes from the `BRIEFCASE_DATA_PATH` environment variable.

Transport: stdio.

# Current Status — V1 Complete (prototype)

All V1 features are implemented and the project builds cleanly.

## Implemented
- `list_files` MCP tool — returns file IDs, names, sizes, and last-modified timestamps. No file system paths are exposed.
- `read_file` MCP tool — returns file content plus metadata by GUID.
- `FileRegistry` — persistent GUID↔path mapping stored as `registry.json` in `BRIEFCASE_DATA_PATH`. Scans all configured paths recursively on startup; prunes stale entries; survives restarts.
- `FileWatcher` — wraps `FileSystemWatcher` on each configured path (recursive). Detects create, change, delete, rename events and updates the registry.
- `NotificationDispatcher` — hosted service that bridges watcher events to MCP protocol notifications:
  - Create / delete / rename → `notifications/resources/list_changed`
  - Content change → `notifications/resources/updated` with URI `briefcase://file/{path}`

## Environment Variables
| Variable | Required | Description |
|---|---|---|
| `BRIEFCASE_PATHS` | Yes | Semicolon-separated list of directories to expose (recursive) |
| `BRIEFCASE_DATA_PATH` | Yes | Directory where `registry.json` is stored |

Copy `src/Briefcase/.env.example` to `src/Briefcase/.env` to configure locally.

# Directories
- `src/Briefcase/`: C# MCP server source.
  - `Configuration/` — `AppSettings`
  - `Registry/` — `FileRegistry`, `RegistryEntry`
  - `Watching/` — `FileWatcher`, `FileChangedEventArgs`
  - `Notifications/` — `NotificationDispatcher`
  - `Tools/` — `ListFilesTool`, `ReadFileTool`
- `docs/`: Documentation for the project.

# Technology

- C# / .NET 10
- `ModelContextProtocol` 1.2.0 (Microsoft MCP C# SDK) — stdio transport
- `DotNetEnv` 3.1.1 — `.env` file loading
- `Microsoft.Extensions.Hosting` — DI and hosted services
- Project scaffolded with `Microsoft.McpServer.ProjectTemplates` (`dotnet new mcpserver`)

# Coding Conventions

- Do not use `_` as a prefix for class-level fields. Use plain camelCase (e.g., `registryFilePath`).
- When a constructor parameter name collides with a field name, disambiguate with `this.` (e.g., `this.registry = registry`).
- Constants should be ALL_CAPS_WITH_UNDERSCORES.

# Instructions for Claude
All changes must be approved before creating these changes. Please prepare a plan of proposed changes and get confirmation before proceeding.


