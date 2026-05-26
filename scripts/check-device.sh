#!/usr/bin/env bash
# Confirms the Beam Pro is reachable over adb. Phase 0 definition-of-done.
set -euo pipefail

echo "adb: $(command -v adb)"
adb version | head -1

echo "--- devices ---"
adb devices -l

count="$(adb devices | sed '1d' | grep -c -E '\sdevice$' || true)"
if [[ "$count" -eq 0 ]]; then
  echo "No authorized device. Plug the Beam Pro into the Mac (USB), accept the 'Allow USB debugging' prompt on the Beam Pro, and re-run." >&2
  exit 1
fi
echo "OK: $count authorized device(s)."
