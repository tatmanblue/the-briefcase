# The Briefcase

An MCP server that gives AI agents controlled access to files outside their working directory.

## What is The Briefcase?

![The Briefcase](docs/logo.svg)  

AI agents — such as Claude Code, GitHub Copilot, or any MCP-compatible client — typically only see files inside their current working directory. The Briefcase solves this by acting as a secure file courier: it exposes a curated set of directories to agents through a structured API, without ever revealing the underlying file origins.

You configure which directories to share. The Briefcase assigns each file a stable ID (a GUID). Agents work entirely through those IDs — they never see real paths, cannot traverse the directory tree, and cannot access anything outside what you explicitly shared.

## How It Works

```
  Your file system          The Briefcase MCP Server          AI Agent
  ──────────────           ───────────────────────────       ─────────
  C:\docs\notes.txt  ───►  briefcase://file/{guid}  ───►   list_files
  C:\docs\report.md  ───►  briefcase://file/{guid}  ───►   read_file
  D:\shared\data.csv ───►  briefcase://file/{guid}  ───►   (notifications)
                           briefcase://file/{guid}  ◄───   create_file / update_file
```

1. You tell The Briefcase which directories to watch via the `BRIEFCASE_PATHS` environment variable.
2. On startup, it scans those directories recursively and assigns each file a stable GUID.
3. The GUID-to-path mapping is saved to `registry.json` so IDs survive server restarts — agents can hold onto a file's ID indefinitely.
4. Agents call `list_files` to discover what's available and `read_file` to retrieve content.
5. When files change on disk, The Briefcase sends MCP notifications so agents can react in real time.

## Status

Version 1.1 — Prototype. Local file system only.

## Setup

### 1. Prerequisites

- .NET 10 SDK

### 2. Configure environment

Copy the example env file and fill in your values:

```
cp src/Briefcase/.env.example src/Briefcase/.env
```

Edit `src/Briefcase/.env`:

```env
# One or more directories to expose, separated by semicolons
BRIEFCASE_PATHS=C:\Users\you\Documents;D:\projects\notes

# Where to store the persistent file ID registry
BRIEFCASE_DATA_PATH=C:\Users\you\.briefcase

# Where agent-created files are stored (optional, defaults to {BRIEFCASE_DATA_PATH}\new)
BRIEFCASE_NEW_FILES_DATA_PATH=C:\Users\you\.briefcase\new
```

### 3. Wire it into your MCP client

**Claude Code** — add to your `claude_mcp_config.json` (or project-level `.mcp.json`):

```json
{
  "servers": {
    "briefcase": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\the-briefcase\\src\\Briefcase"]
    }
  }
}
```

**VS Code** — create `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "briefcase": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\the-briefcase\\src\\Briefcase"]
    }
  }
}
```

## Available Tools

### `list_files`

Returns all files currently known to The Briefcase. No file system paths are included — only the information the agent needs.

**Returns:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "notes.txt",
    "size": 1024,
    "lastModified": "2026-04-24T10:00:00Z"
  }
]
```

### `read_file`

Reads the content of a file by its ID.

**Parameters:**
- `id` — the GUID returned by `list_files`

**Returns:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "notes.txt",
  "size": 1024,
  "lastModified": "2026-04-24T10:00:00Z",
  "content": "..."
}
```

### `create_file`

Creates a new file in the Briefcase. The file is written to `BRIEFCASE_NEW_FILES_DATA_PATH` and made immediately available to all agents.

**Parameters:**
- `name` — filename including extension (e.g. `notes.txt`). Path separators are stripped.
- `content` — full content of the new file.

**Returns:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "notes.txt",
  "size": 42,
  "lastModified": "2026-04-27T10:00:00Z"
}
```

### `update_file`

Replaces the full content of an existing file. Works on any file in the Briefcase regardless of which directory it lives in.

**Parameters:**
- `id` — the GUID returned by `list_files` or `create_file`
- `content` — the new full content. The entire existing content is replaced.

**Returns:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "notes.txt",
  "size": 99,
  "lastModified": "2026-04-27T11:00:00Z"
}
```

## File Change Notifications

The Briefcase watches configured directories in real time using `FileSystemWatcher`. When files change, it sends standard MCP resource notifications to connected agents:

| Event | MCP Notification |
|---|---|
| File created, deleted, or renamed | `notifications/resources/list_changed` |
| File content modified | `notifications/resources/updated` |

Agents that support MCP resource subscriptions can react immediately when a file they care about changes, without polling.

## Roadmap

- [ ] Search files by name and content
- [x] Create file
- [x] Update file content
- [ ] Cloud storage backends (e.g. OneDrive, Google Drive)

## License

Copyright 2026 Matthew Raffel. Licensed under the [Apache License 2.0](LICENSE).

## File Version
1.0.0 
