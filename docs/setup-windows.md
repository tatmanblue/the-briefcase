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

# Port the local web interface listens on, bound to 127.0.0.1 only (optional, default 5289)
BRIEFCASE_WEB_PORT=5289
```

## 3. Web interface

Once the server is running, open `http://127.0.0.1:5289` (or your configured `BRIEFCASE_WEB_PORT`) in a browser on the same machine. It lets you list and view files, and move or delete them — these actions are only available through the web UI, not to agents. Deleted files go to the Recycle Bin, not permanent deletion.

## 4. Build for use

### Recommended: install to run in the background

From the repo root, in any terminal (cmd, Git Bash, Windows Terminal — no PowerShell needed):

```
dotnet run scripts/install-windows.cs
```

This publishes a self-contained build, installs it to `%LOCALAPPDATA%\Briefcase\app` together with a copy of
`src\Briefcase\.env`, and registers a Scheduled Task named `Briefcase` that starts it at logon with no console
window and restarts it if it ever exits. It then checks that `/mcp` responds. No admin rights are needed, and it
runs in your own desktop session, so "Open in default app", "Show in folder" and the Recycle Bin work normally.

| Command | What it does |
|---|---|
| `dotnet run scripts/install-windows.cs` | Publish, install or update, and start. Re-run after pulling changes. |
| `dotnet run scripts/install-windows.cs -- restart` | Copy `src\Briefcase\.env` again and restart, without rebuilding. Use after editing `.env`. |
| `dotnet run scripts/install-windows.cs -- stop` | Stop the server (it starts again at next logon). |
| `dotnet run scripts/install-windows.cs -- uninstall` | Stop it, remove the task and `%LOCALAPPDATA%\Briefcase`. |

`src\Briefcase\.env` stays the only file you edit. Server output goes to `%LOCALAPPDATA%\Briefcase\logs\briefcase.log`.
Your files and `BRIEFCASE_DATA_PATH` are never touched by install, restart or uninstall. Because the running copy lives
outside the repo, you can keep building in the repo while it runs.

### Manual

```
dotnet publish src/Briefcase/Briefcase.csproj -r win-x64 -o publish
```

Then run `publish\Briefcase.exe` in a terminal and leave it open.

> **Why publish, not `dotnet run`?** The web interface's static assets (its JS/CSS) are only
> guaranteed available in a published build. `dotnet run` and a plain `dotnet build` output run in
> Production mode by default, where those assets aren't served — the page loads but nothing is
> interactive (buttons/dropdowns silently do nothing). Always point your MCP client at a published
> `Briefcase.exe`, not a `bin\Debug\...` or `bin\Release\...` build output.

## 5. Wire it into your MCP client

The server speaks HTTP, not stdio — it's a persistent process, not something your MCP client spawns on demand. Start it first (install it with `scripts/install-windows.cs`, or run `publish\Briefcase.exe` in a terminal) and leave it running, then point your client at its `/mcp` endpoint.

**Claude Code** — add to your `claude_mcp_config.json` (or project-level `.mcp.json`):

```json
{
  "servers": {
    "briefcase": {
      "type": "http",
      "url": "http://127.0.0.1:5289/mcp"
    }
  }
}
```

**VS Code** — create `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "briefcase": {
      "type": "http",
      "url": "http://127.0.0.1:5289/mcp"
    }
  }
}
```

Adjust the port if you set `BRIEFCASE_WEB_PORT` to something other than the default 5289.

After pulling changes, re-run `dotnet run scripts/install-windows.cs` (or, for a manual setup, the `dotnet publish` command above and restart the server), then reconnect your MCP client.
