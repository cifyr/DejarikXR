#!/usr/bin/env bash
# Drops a Blender-exported .glb onto the device where the app's SceneCatalog looks for it,
# with no rebuild. Usage: scripts/push-scene.sh <scene.glb> [package-name]
set -euo pipefail

glb="${1:-}"
pkg="${2:-${DEJARIK_PACKAGE:-com.cadenwarren.dejarik}}"

if [[ -z "$glb" || ! -f "$glb" ]]; then
  echo "usage: $0 <scene.glb> [package-name]   (default package: $pkg)" >&2
  exit 1
fi

dest="/sdcard/Android/data/${pkg}/files/scenes"
echo "Ensuring $dest exists ..."
adb shell "mkdir -p '$dest'"
echo "Pushing $glb -> $dest/"
adb push "$glb" "$dest/"
echo "OK. Pick it in-app via the scene menu."
