# Setup — macOS

## 1. Prerequisites

- .NET 10 SDK

## 2. Configure environment

Copy the example env file and fill in your values:

```
cp src/Briefcase/.env.example src/Briefcase/.env
```

Edit `src/Briefcase/.env`:

```env
# One or more directories to expose, separated by semicolons
BRIEFCASE_PATHS=~/Documents;~/projects/notes

# Where to store the persistent file ID registry
BRIEFCASE_DATA_PATH=~/.briefcase

# Where agent-created files are stored (optional, defaults to {first BRIEFCASE_PATHS entry}/new)
BRIEFCASE_NEW_PATH=~/Documents/new

# Optional: path to a .gitignore-style file listing patterns to exclude
# Defaults to {BRIEFCASE_DATA_PATH}/.briefcase-ignore if not set
BRIEFCASE_IGNORE_FILE=~/.briefcase/my-ignore

# Default max results for list_files (optional; negative or unset = no limit)
BRIEFCASE_LIST_DEFAULT_LIMIT=100

# Default max results for search_files (optional, default 25; negative = no limit)
BRIEFCASE_SEARCH_DEFAULT_LIMIT=25

# Files larger than this (in KB) are skipped during content search (optional, default 512)
BRIEFCASE_SEARCH_MAX_FILE_SIZE_KB=512

# Enables the word-set cache for content search (optional, default false)
# When true, reindex_files rebuilds this cache so subsequent searches are faster
BRIEFCASE_SEARCH_CACHE_ENABLED=false

# Port the local web interface listens on, bound to 127.0.0.1 only (optional, default 5289)
BRIEFCASE_WEB_PORT=5289
```

## 3. Web interface

Once the server is running, open `http://127.0.0.1:5289` (or your configured `BRIEFCASE_WEB_PORT`) in a browser on the same machine. It lets you list and view files, and move or delete them — these actions are only available through the web UI, not to agents. Deleted files go to the Trash, not permanent deletion.

## 4. Build for use

```
dotnet publish src/Briefcase/Briefcase.csproj -r osx-arm64 -o publish
```

> **Why publish, not `dotnet run`?** The web interface's static assets (its JS/CSS) are only
> guaranteed available in a published build. `dotnet run` and a plain `dotnet build` output run in
> Production mode by default, where those assets aren't served — the page loads but nothing is
> interactive (buttons/dropdowns silently do nothing). Always point your MCP client at a published
> `Briefcase` binary, not a `bin/Debug/...` or `bin/Release/...` build output.

## 5. Wire it into your MCP client

**Claude Code** — add to your `claude_mcp_config.json` (or project-level `.mcp.json`):

```json
{
  "servers": {
    "briefcase": {
      "type": "stdio",
      "command": "/path/to/the-briefcase/publish/Briefcase"
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
      "command": "/path/to/the-briefcase/publish/Briefcase"
    }
  }
}
```

Re-run the `dotnet publish` command above after pulling changes to pick up updates, then restart your MCP client's connection to the server.
