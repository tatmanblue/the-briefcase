#!/usr/bin/env bash
#
# Publishes Briefcase and installs it as a macOS LaunchAgent so it runs as a
# persistent, standalone process — started at login, restarted automatically
# if it crashes or is killed — instead of being spawned per-session by an MCP
# client over stdio.
#
# Safe to re-run: publishes a fresh build and reloads the LaunchAgent each
# time (e.g. after pulling new changes).
#
# Usage:
#   scripts/install-macos.sh [--arch arm64|x64]
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$REPO_ROOT/src/Briefcase/Briefcase.csproj"
WORKING_DIR="$REPO_ROOT/src/Briefcase"   # so Program.cs's .env lookup (cwd first) finds the existing .env here
PUBLISH_DIR="$REPO_ROOT/publish"
BINARY="$PUBLISH_DIR/Briefcase"

LABEL="com.briefcase.mcp"
PLIST_PATH="$HOME/Library/LaunchAgents/$LABEL.plist"
LOG_DIR="$HOME/Library/Logs/Briefcase"

ARCH="$(uname -m)"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            ARCH="$2"
            shift 2
            ;;
        *)
            echo "Unknown argument: $1" >&2
            exit 1
            ;;
    esac
done

case "$ARCH" in
    arm64) RID="osx-arm64" ;;
    x64|x86_64) RID="osx-x64" ;;
    *)
        echo "Unsupported --arch '$ARCH' (expected arm64 or x64)" >&2
        exit 1
        ;;
esac

if [[ ! -f "$WORKING_DIR/.env" ]]; then
    echo "error: $WORKING_DIR/.env not found." >&2
    echo "       Copy .env.example to .env and fill in BRIEFCASE_PATHS / BRIEFCASE_DATA_PATH first" >&2
    echo "       (see docs/setup-macos.md)." >&2
    exit 1
fi

echo "==> Publishing self-contained build for $RID"
dotnet publish "$PROJECT" -r "$RID" -o "$PUBLISH_DIR"
chmod +x "$BINARY"

echo "==> Writing LaunchAgent plist: $PLIST_PATH"
mkdir -p "$HOME/Library/LaunchAgents" "$LOG_DIR"
cat > "$PLIST_PATH" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$LABEL</string>

    <key>ProgramArguments</key>
    <array>
        <string>$BINARY</string>
    </array>

    <!-- Picks up $WORKING_DIR/.env (Program.cs checks cwd before the executable's own directory). -->
    <key>WorkingDirectory</key>
    <string>$WORKING_DIR</string>

    <!-- Start at login, and keep it running: restart automatically on crash or if killed. -->
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>

    <key>StandardOutPath</key>
    <string>$LOG_DIR/stdout.log</string>
    <key>StandardErrorPath</key>
    <string>$LOG_DIR/stderr.log</string>

    <key>ProcessType</key>
    <string>Background</string>
</dict>
</plist>
PLIST

UID_GUI="gui/$(id -u)"
echo "==> Reloading LaunchAgent"
launchctl bootout "$UID_GUI/$LABEL" 2>/dev/null || true
launchctl bootstrap "$UID_GUI" "$PLIST_PATH"
launchctl enable "$UID_GUI/$LABEL"

echo "==> Done."
echo "    Status:  launchctl print $UID_GUI/$LABEL"
echo "    Logs:    $LOG_DIR/stdout.log , $LOG_DIR/stderr.log"
echo "    Verify:  curl -s -X POST http://127.0.0.1:5289/mcp -H 'Content-Type: application/json' -H 'Accept: application/json, text/event-stream' -d '{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"check\",\"version\":\"0\"}}}'"
echo ""
echo "    Note: if a stdio-spawned Briefcase from an existing MCP client session is still holding"
echo "    port 5289, this will fail to bind at first -- launchd will keep retrying (KeepAlive) and"
echo "    it'll come up on its own once that session ends and the port frees up."
