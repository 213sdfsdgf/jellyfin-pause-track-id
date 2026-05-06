#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <public_zip_url> <target_abi> [version]" >&2
  echo "Example: $0 https://github.com/USER/REPO/releases/download/v1.0.0.0/pause-track-id_1.0.0.0.zip 10.11.0.0 1.0.0.0" >&2
  exit 1
fi

PUBLIC_ZIP_URL="$1"
TARGET_ABI="$2"
VERSION="${3:-1.0.2.1}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_DIR="$ROOT_DIR/Jellyfin.Plugin.PauseTrackId"
OUT_DIR="$ROOT_DIR/artifacts/repository"
PUBLISH_DIR="$OUT_DIR/publish"
ZIP_BASENAME="pause-track-id_${VERSION}.zip"
ZIP_PATH="$OUT_DIR/$ZIP_BASENAME"
MANIFEST_PATH="$OUT_DIR/manifest.json"
TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

rm -rf "$OUT_DIR"
mkdir -p "$PUBLISH_DIR"

dotnet publish "$PROJECT_DIR/Jellyfin.Plugin.PauseTrackId.csproj" -c Release -o "$PUBLISH_DIR"

(
  cd "$PUBLISH_DIR"
  zip -r "$ZIP_PATH" .
)

CHECKSUM="$(md5sum "$ZIP_PATH" | awk '{print $1}')"

cat > "$MANIFEST_PATH" <<JSON
[
  {
    "guid": "3277e5e5-0e25-420e-9653-ad5219e01e69",
    "name": "Pause Track ID",
    "description": "Recognizes the last few seconds of paused playback with Chromaprint + AcoustID and shows the track title on screen.",
    "overview": "Shazam-like pause recognition for Jellyfin",
    "owner": "213sdfsdgf",
    "category": "General",
    "versions": [
      {
        "version": "$VERSION",
        "changelog": "Hotfix Jellyfin startup: optional web-button injection is now guarded so File Transformation integration errors no longer prevent the server from starting.",
        "targetAbi": "$TARGET_ABI",
        "sourceUrl": "$PUBLIC_ZIP_URL",
        "checksum": "$CHECKSUM",
        "timestamp": "$TIMESTAMP"
      }
    ]
  }
]
JSON

echo "Built plugin zip: $ZIP_PATH"
echo "Built manifest:  $MANIFEST_PATH"
echo "MD5 checksum:     $CHECKSUM"
