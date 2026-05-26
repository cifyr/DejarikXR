#!/usr/bin/env bash
# Sideloads (reinstall-in-place) an APK to the Beam Pro. Usage: scripts/install-apk.sh <path-to.apk>
set -euo pipefail

apk="${1:-}"
if [[ -z "$apk" || ! -f "$apk" ]]; then
  echo "usage: $0 <path-to.apk>" >&2
  exit 1
fi

echo "Installing $apk ..."
adb install -r "$apk"
echo "OK. Launch it from MyGlasses on the Beam Pro (developer mode: tap the glasses icon 10x)."
