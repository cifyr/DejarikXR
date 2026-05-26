#!/usr/bin/env bash
# Tails device logs filtered to XREAL + our app tags. Ctrl-C to stop.
set -euo pipefail

echo "Streaming logcat (XREAL/NRSDK + Unity + our [App]/[Anchor]/[RuntimeSceneLoader] tags). Ctrl-C to stop."
adb logcat -v time \
  Unity:V NRSDK:V XREAL:V NRDevice:V \
  ActivityManager:I AndroidRuntime:E '*:S'
