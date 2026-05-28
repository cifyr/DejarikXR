#!/usr/bin/env bash
# Headless APK build for Galaxy XR / Android XR via Unity batchmode. Mirror of build-apk.sh but
# invokes BuildGalaxyXrApk, which flips the XR loader to OpenXR + Android XR features, sets the
# DEJARIK_ANDROID_XR scripting define (gates WorldDeck on, phone HoloGui off), and writes a separate
# APK file so it doesn't collide with the XREAL build.
#
# NOT verified on Galaxy XR hardware. See README "Install (Galaxy XR)" and the VERIFY comments in
# unity/Assets/Editor/AndroidXrSetup.cs and unity/Assets/Editor/AndroidXrManifestPatch.cs.
set -euo pipefail

UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity}"
PROJ="$(cd "$(dirname "$0")/../unity" && pwd)"
LOG="${LOG:-/tmp/dejarikxr_galaxyxr_build.log}"

if [[ ! -x "$UNITY" ]]; then
  echo "Unity not found at $UNITY (set UNITY_BIN to override)" >&2
  exit 1
fi

command -v adb >/dev/null && adb kill-server 2>/dev/null || true

echo "Building (Galaxy XR, log: $LOG) ..."
"$UNITY" -batchmode -nographics -buildTarget Android -projectPath "$PROJ" \
  -executeMethod XrealAR.EditorTools.XrealBuild.BuildGalaxyXrApk -quit -logFile "$LOG"

apk="$PROJ/build/DejarikXR-galaxyxr.apk"
if [[ -f "$apk" ]]; then
  echo "OK: $(ls -lh "$apk" | awk '{print $5}') -> $apk"
else
  echo "Build did not produce an APK; see $LOG" >&2
  grep -E "class layout|\[XrealBuild\] result|\[AndroidXrSetup\]|Build Finished" "$LOG" | tail -10 >&2 || true
  exit 1
fi
