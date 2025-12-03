#!/usr/bin/env bash
set -euo pipefail

# Simple helper to compile the mod and produce a distributable zip alongside the DLL.
# Usage: GAME_PATH="/path/to/Stardew Valley" ./build-release.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="Release"
GAME_PATH_ARG=""

if [[ -n "${GAME_PATH:-}" ]]; then
  GAME_PATH_ARG="-p:GamePath=${GAME_PATH}"
fi

pushd "$SCRIPT_DIR" >/dev/null

echo "Restoring packages and building PetBondMod.csproj (configuration: ${CONFIG})"
dotnet build PetBondMod.csproj -c "$CONFIG" ${GAME_PATH_ARG}

OUTPUT_DIR="$SCRIPT_DIR/bin/${CONFIG}/net6.0"
PUBLISH_DIR="$SCRIPT_DIR/publish"
ZIP_PATH="$PUBLISH_DIR/PetBondMod-${CONFIG}.zip"

mkdir -p "$PUBLISH_DIR"

if [[ -d "$OUTPUT_DIR" ]]; then
  echo "Zipping build output to $ZIP_PATH"
  rm -f "$ZIP_PATH"
  (cd "$OUTPUT_DIR" && zip -r "$ZIP_PATH" .)
  echo "Done. DLL and content are in $OUTPUT_DIR, packaged zip at $ZIP_PATH"
else
  echo "Expected build output at $OUTPUT_DIR was not found" >&2
  exit 1
fi

popd >/dev/null
