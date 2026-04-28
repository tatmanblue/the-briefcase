# The Briefcase

An MCP server that gives AI agents controlled access to files outside their working directory.

## What is The Briefcase?

![The Briefcase](docs/logo.svg)  

AI agents — such as Claude Code, GitHub Copilot, or any MCP-compatible client — typically only see files inside their current working directory. The Briefcase solves this by acting as a secure file courier: it exposes a curated set of directories to agents through a structured API, without ever revealing the underlying file origins.

You configure which directories to share. The Briefcase assigns each file a stable ID (a GUID). Agents work entirely through those IDs — they never see real paths, cannot traverse the directory tree, and cannot access anything outside what you explicitly shared.


## Video Demo

[![The Briefcase Demo](https://img.youtube.com/vi/29OXo1NEDLc/0.jpg)](https://youtu.be/29OXo1NEDLc)

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

Version 1.2 — Prototype. Local file system only.

## Setup

- [Windows](docs/setup-windows.md)
- [macOS](docs/setup-macos.md)

## Available Tools

### `list_files`

Returns all files currently known to The Briefcase. No file system paths are included — only the information the agent needs.

**Parameters (all optional):**
- `limit` — max results; omit to use server default; `0` or negative = all
- `sort` — `modified_desc` (default), `modified_asc`, `name_asc`, `name_desc`, `default`
- `project` — filter by project ID (GUID) or project name
- `unassigned` — `true` to return only files not in any project (cannot combine with `project`)

**Returns:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "notes.txt",
    "size": 1024,
    "lastModified": "2026-04-24T10:00:00Z",
    "projectId": "a1b2c3d4-...",
    "projectName": "My Project"
  }
]
```

`projectId` and `projectName` are `null` when the file is not associated with any project.

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

Creates a new file in the Briefcase. The file is written to `BRIEFCASE_NEW_PATH` and made immediately available to all agents.

**Parameters:**
- `name` — filename including extension (e.g. `notes.txt`). Path separators are stripped.
- `content` — full content of the new file.
- `projectId` *(optional)* — GUID of a project to associate the file with. The call fails if the ID does not exist.

**Returns:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "notes.txt",
  "size": 42,
  "lastModified": "2026-04-27T10:00:00Z",
  "projectId": null
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

### `search_files`

Searches for files by name, content, or both. Content search covers `.md` and `.txt` files only; other file types are matched by name only.

**Parameters:**
- `query` — text to search for (case-insensitive, required)
- `searchIn` — `"both"` (default), `"name"`, or `"content"`
- `matchMode` — `"substring"` (default, matches anywhere within a word) or `"word"` (whole-word only)
- `limit` — max results; omit to use the server default (`BRIEFCASE_SEARCH_DEFAULT_LIMIT`)
- `sort` — same options as `list_files`: `modified_desc` (default), `modified_asc`, `name_asc`, `name_desc`, `default`
- `project` — filter by project ID (GUID) or project name
- `unassigned` — `true` to search only files not in any project (cannot combine with `project`)

**Returns:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "quarterly-report.md",
    "size": 4096,
    "lastModified": "2026-04-15T10:30:00Z",
    "matchedIn": "name",
    "projectId": "a1b2c3d4-...",
    "projectName": "My Project"
  }
]
```

`matchedIn` is `"name"`, `"content"`, or `"both"` — tells the agent why the file was returned. `projectId` and `projectName` are `null` when the file has no project association.

---

## Project Tools

Projects let you group related files together. A file can belong to at most one project. Files with no project are still fully accessible. Project metadata is stored in `BRIEFCASE_DATA_PATH` as `project-{guid}.json` files and never appears in `list_files` or `search_files` results.

### `create_project`

Creates a new project. Project names must be unique.

**Parameters:**
- `name` — project name (required, must be unique)
- `description` — short description of the project

**Returns:** project ID, name, description, createdDate

---

### `list_projects`

Lists all projects with their file counts.

**Parameters (all optional):**
- `limit` — max results; omit to return all
- `sort` — `name_asc` (default) or `name_desc`

---

### `get_project`

Returns a project's metadata and the list of files it contains. Accepts a project ID (GUID) or project name.

**Parameters:**
- `idOrName` — project ID (GUID) or project name

**Returns:** id, name, description, createdDate, files (list of `{ id, name }`)

---

### `add_file_to_project`

Associates a file with a project. If the file is already in another project it is moved to this one.

**Parameters:**
- `projectId` — project GUID
- `fileId` — file GUID (from `list_files`)

---

### `remove_file_from_project`

Removes a file from a project. The file remains in the Briefcase but loses its project association.

**Parameters:**
- `projectId` — project GUID
- `fileId` — file GUID

---

### `update_project`

Updates a project's name and/or description. Omit either parameter to leave it unchanged.

**Parameters:**
- `projectId` — project GUID
- `name` *(optional)* — new name
- `description` *(optional)* — new description

---

### `delete_project`

Deletes a project. All member files remain in the Briefcase but lose their project association.

**Parameters:**
- `projectId` — project GUID

---

### `reindex_files`

Rebuilds the file registry and, if the search cache is enabled, the search cache. Scans all configured directories to add newly discovered files and prune stale entries. Blocks until complete.

If a reindex is already in progress, returns immediately without waiting.

**Parameters:** none

**Returns:**
```json
{
  "status": "completed",
  "registryAdded": 3,
  "registryPruned": 1,
  "cacheEntriesBuilt": 42
}
```

If already running:
```json
{
  "status": "already_running"
}
```

`cacheEntriesBuilt` is `0` when `BRIEFCASE_SEARCH_CACHE_ENABLED` is `false` or unset.

## File Change Notifications

The Briefcase watches configured directories in real time using `FileSystemWatcher`. When files change, it sends standard MCP resource notifications to connected agents:

| Event | MCP Notification |
|---|---|
| File created, deleted, or renamed | `notifications/resources/list_changed` |
| File content modified | `notifications/resources/updated` |

Agents that support MCP resource subscriptions can react immediately when a file they care about changes, without polling.

## Roadmap

- [x] Search files by name and content
- [x] Create file
- [x] Update file content
- [x] Projects — group files and filter by project
- [ ] Cloud storage backends (e.g. OneDrive, Google Drive)
- [ ] File locking and edit conflict resolution
- [ ] HTTP with Server-Sent Events

## License

Copyright 2026 Matthew Raffel. Licensed under the [Apache License 2.0](LICENSE).

## File Version
1.2.0
