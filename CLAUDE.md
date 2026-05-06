# Goal

The goal is to create a MCP server for agents to read, request, search and update files not in the agents working directory. Version 1 only supports local file system.

The server will find files based on environment variable that contains one or more paths (local to the MCP server not the agent).

The server will support the following operations:
- Read file
- List Files available
- Send agents updates on file changes (File Change Notifications)

These features will be saved for a future version:
- Search for files (searches for files based on name and content)

The MCP server will not expose direct file system access to agents. Instead, it will provide an API for agents to interact with files in a controlled manner.

The MCP server will be designed to be extensible, allowing for future support of additional file systems (e.g., cloud storage) and additional operations.

The MCP server will not leak any information about the underlying file system to agents. Agents will only be able to interact with files through the API provided by the MCP server.
Files are identified by stable GUIDs that persist across server restarts (stored in `registry.json`). The path for storing this information comes from the `BRIEFCASE_DATA_PATH` environment variable.

Transport: stdio.

# Current Status — V1.3 (prototype)

All V1, V1.1, V1.2, and V1.3 features are implemented and the project builds cleanly.

## Implemented
- `list_files` MCP tool — returns file IDs, names, sizes, last-modified timestamps, project association, and archive state. No file system paths are exposed. Supports optional `limit`, `sort`, `project`, `unassigned`, `includeArchived`, and `archivedOnly` filters. Server-side default limit is controlled by `BRIEFCASE_LIST_DEFAULT_LIMIT`. Archived files are excluded by default.
- `read_file` MCP tool — returns file content plus metadata by GUID. Works regardless of archive state.
- `create_file` MCP tool — agents supply a filename and content; the file is written to `BRIEFCASE_NEW_PATH` and registered immediately. Optionally associates with a project via `projectId`.
- `update_file` MCP tool — agents supply a GUID and new content; the entire file is replaced in-place. Works regardless of archive state.
- `search_files` MCP tool — searches file names and/or content (`.md`/`.txt` only). Supports `query`, `searchIn`, `matchMode`, `limit`, `sort`, `project`, `unassigned`, `includeArchived`, `archivedOnly`. Archived files are excluded by default.
- `archive_file` MCP tool — marks a file as archived; excluded from listings and searches by default. File remains readable and project association is preserved.
- `unarchive_file` MCP tool — restores an archived file to active status.
- `reindex_files` MCP tool — rescans all configured paths, prunes stale entries, optionally rebuilds the search cache. Returns counts of added/pruned/rebuilt entries.
- `create_project` / `list_projects` / `get_project` / `update_project` / `delete_project` — full project CRUD.
- `add_file_to_project` / `remove_file_from_project` — manage project membership (one project per file).
- `FileRegistry` — persistent GUID↔path mapping stored as `registry.json` in `BRIEFCASE_DATA_PATH`. Scans all configured paths recursively on startup; prunes stale entries; survives restarts. Stores `IsArchived` state per entry.
- `ProjectRegistry` — persistent project store (`project-{guid}.json` per project) with reverse file→project index.
- `FileWatcher` — wraps `FileSystemWatcher` on each configured path (recursive). Detects create, change, delete, rename events and updates the registry. Rename preserves archive state.
- `NotificationDispatcher` — hosted service that bridges watcher events to MCP protocol notifications:
  - Create / delete / rename / archive / unarchive → `notifications/resources/list_changed`
  - Content change → `notifications/resources/updated` with URI `briefcase://file/{path}`
  - Project mutations → `notifications/projects/list_changed`
- `SearchCache` — optional word-level cache for `.md`/`.txt` files; enabled via `BRIEFCASE_SEARCH_CACHE_ENABLED`.
- `IgnoreRules` — `.briefcase-ignore` file support (gitignore syntax) via `BRIEFCASE_IGNORE_FILE`.

## Environment Variables
| Variable | Required | Description |
|---|---|---|
| `BRIEFCASE_PATHS` | Yes | Semicolon-separated list of directories to expose (recursive) |
| `BRIEFCASE_DATA_PATH` | Yes | Directory where `registry.json` and project files are stored |
| `BRIEFCASE_NEW_PATH` | No | Where agent-created files are stored. Defaults to `{first BRIEFCASE_PATHS entry}\new` |
| `BRIEFCASE_LIST_DEFAULT_LIMIT` | No | Default max files returned by `list_files`. Negative or unset = no limit. |
| `BRIEFCASE_SEARCH_DEFAULT_LIMIT` | No | Default max results for `search_files`. Default = 25. |
| `BRIEFCASE_SEARCH_MAX_FILE_SIZE_KB` | No | Files larger than this are skipped during content search. Default = 512. |
| `BRIEFCASE_SEARCH_CACHE_ENABLED` | No | Set to `true` to enable word-level search cache. Default = false. |
| `BRIEFCASE_IGNORE_FILE` | No | Path to `.briefcase-ignore` file. Defaults to `{BRIEFCASE_DATA_PATH}/.briefcase-ignore`. |

Copy `src/Briefcase/.env.example` to `src/Briefcase/.env` to configure locally.

# Directories
- `src/Briefcase/`: C# MCP server source.
  - `Configuration/` — `AppSettings`, `StartupValidator`
  - `Exclusions/` — `IgnoreRules`
  - `Registry/` — `FileRegistry`, `RegistryEntry`, `ProjectRegistry`, `ProjectEntry`
  - `Watching/` — `FileWatcher`, `FileChangedEventArgs`
  - `Notifications/` — `NotificationDispatcher`
  - `Search/` — `SearchCache`, `SearchCacheEntry`
  - `Reindex/` — `ReindexService`
  - `Tools/` — `ListFilesTool`, `ReadFileTool`, `CreateFileTool`, `UpdateFileTool`, `SearchFilesTool`, `ReindexTool`, `ArchiveFileTool`, `UnarchiveFileTool`, `CreateProjectTool`, `ListProjectsTool`, `GetProjectTool`, `AddFileToProjectTool`, `RemoveFileFromProjectTool`, `UpdateProjectTool`, `DeleteProjectTool`
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
