#!/usr/bin/env bash
# Headless APK build via Unity batchmode. The -buildTarget Android flag is REQUIRED: without it the
# editor compiles XREALSettings without UNITY_ANDROID while the player compiles it with, and the build
# aborts with "script class layout is incompatible between the editor and the player".
set -euo pipefail

UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity}"
PROJ="$(cd "$(dirname "$0")/../unity" && pwd)"
LOG="${LOG:-/tmp/dejarikxr_build.log}"

if [[ ! -x "$UNITY" ]]; then
  echo "Unity not found at $UNITY (set UNITY_BIN to override)" >&2
  exit 1
fi

# Avoid the brew-adb vs Unity-bundled-adb port 5037 version clash during the build's device probe.
command -v adb >/dev/null && adb kill-server 2>/dev/null || true

echo "Building (log: $LOG) ..."
"$UNITY" -batchmode -nographics -buildTarget Android -projectPath "$PROJ" \
  -executeMethod XrealAR.EditorTools.XrealBuild.BuildApk -quit -logFile "$LOG"

apk="$PROJ/build/DejarikXR.apk"
if [[ -f "$apk" ]]; then
  echo "OK: $(ls -lh "$apk" | awk '{print $5}') -> $apk"
else
  echo "Build did not produce an APK; see $LOG" >&2
  grep -E "class layout|\[XrealBuild\] result|Build Finished" "$LOG" | tail -5 >&2 || true
  exit 1
fi