#!/usr/bin/env bash
# Switches the (currently USB-connected) Beam Pro to wireless adb, then you can unplug it and
# tether the glasses to the Beam Pro's DP-Alt USB-C port. Usage: scripts/connect-wireless.sh <device-ip>
set -euo pipefail

ip="${1:-}"
if [[ -z "$ip" ]]; then
  echo "usage: $0 <device-ip>   (find it in Beam Pro Settings > About > Status, or Wi-Fi details)" >&2
  exit 1
fi

echo "Setting adb to tcpip mode on 5555 (device must be on USB now)..."
adb tcpip 5555
sleep 1
echo "Connecting to ${ip}:5555 ..."
adb connect "${ip}:5555"
adb devices -l
echo "OK. You can unplug USB and connect the glasses to the Beam Pro."
