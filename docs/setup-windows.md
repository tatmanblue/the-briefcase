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
