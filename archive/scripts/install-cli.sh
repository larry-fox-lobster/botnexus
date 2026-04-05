#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TOOLS_PATH="$REPO_ROOT/artifacts/tools"

dotnet pack "$REPO_ROOT/src/BotNexus.Cli" -c Release -o "$TOOLS_PATH"

if dotnet tool list --global | grep -Eq '^[[:space:]]*botnexus\.cli[[:space:]]'; then
  dotnet tool update --global --add-source "$TOOLS_PATH" BotNexus.Cli
else
  dotnet tool install --global --add-source "$TOOLS_PATH" BotNexus.Cli
fi

echo "✅ botnexus CLI installed. Run 'botnexus --help' to get started."
