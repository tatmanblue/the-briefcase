# Setup — Windows

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
BRIEFCASE_PATHS=C:\Users\you\Documents;D:\projects\notes

# Where to store the persistent file ID registry
BRIEFCASE_DATA_PATH=C:\Users\you\.briefcase

# Where agent-created files are stored (optional, defaults to {first BRIEFCASE_PATHS entry}\new)
BRIEFCASE_NEW_PATH=C:\Users\you\Documents\new

# Optional: path to a .gitignore-style file listing patterns to exclude
# Defaults to {BRIEFCASE_DATA_PATH}\.briefcase-ignore if not set
BRIEFCASE_IGNORE_FILE=C:\Users\you\.briefcase\my-ignore

# Default max results for list_files (optional; negative or unset = no limit)
BRIEFCASE_LIST_DEFAULT_LIMIT=100

# Default max results for search_files (optional, default 25; negative = no limit)
BRIEFCASE_SEARCH_DEFAULT_LIMIT=25

# Files larger than this (in KB) are skipped during content search (optional, default 512)
BRIEFCASE_SEARCH_MAX_FILE_SIZE_KB=512

# Enables the word-set cache for content search (optional, default false)
# When true, reindex_files rebuilds this cache so subsequent searches are faster
BRIEFCASE_SEARCH_CACHE_ENABLED=false
```

## 3. Wire it into your MCP client

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
