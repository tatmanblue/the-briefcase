# The Briefcase — Developer Guide

## Building

```bash
cd src/Briefcase
dotnet build
```

## Running locally

1. Copy `.env.example` to `.env` and fill in your values:

```env
BRIEFCASE_PATHS=C:\Users\you\Documents;D:\projects\notes
BRIEFCASE_DATA_PATH=C:\Users\you\.briefcase
```

2. Run:

```bash
dotnet run
```

The server communicates over stdio and will start scanning the configured paths immediately.

## Project structure

```
src/Briefcase/
  Configuration/        AppSettings — typed config from environment variables
  Registry/             FileRegistry — persistent GUID↔path mapping (registry.json)
  Watching/             FileWatcher — FileSystemWatcher wrapper, fires change events
  Notifications/        NotificationDispatcher — bridges change events to MCP notifications
  Tools/                ListFilesTool, ReadFileTool — the MCP tools exposed to agents
  Program.cs            Composition root — wires up DI and starts the host
  .env.example          Template for required environment variables
```

## Publishing

Build a release package:

```bash
dotnet pack -c Release
```

Publish to NuGet.org:

```bash
dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
```

Once published, clients can run it without cloning the repo using the `dnx` command:

```json
{
  "servers": {
    "briefcase": {
      "type": "stdio",
      "command": "dnx",
      "args": ["TheBriefcase", "--version", "0.1.0-alpha", "--yes"]
    }
  }
}
```

## Supported platforms

The project is configured to build self-contained executables for:

- `win-x64`, `win-arm64`
- `osx-arm64`
- `linux-x64`, `linux-arm64`, `linux-musl-x64`

To add more targets, update `<RuntimeIdentifiers>` in `Briefcase.csproj`.
