#!/usr/bin/env bash
# Automated play-through on the Linux test player at one or more resolutions.
# Usage: Tools/autopilot.sh [WxH[:inset] ...]   e.g. Tools/autopilot.sh 1920x1080 2400x1080:90 1920x1200 2048x1536
# Extra env: TR_TRACK=<id>, TR_FAST=1 (jump near the finish), SKIP_BUILD=1
set -u
cd "$(dirname "$0")/.."
[ "${SKIP_BUILD:-0}" = 1 ] || Tools/build_linux.sh || exit 1
OUT="$PWD/Logs/AutoPilot"; mkdir -p "$OUT"
RES=("$@"); [ ${#RES[@]} -eq 0 ] && RES=(1920x1080)
rc=0
for R in "${RES[@]}"; do
  WH=${R%%:*}; INSET=0; [[ "$R" == *:* ]] && INSET=${R##*:}
  W=${WH%x*}; H=${WH#*x}; TAG="${W}x${H}"; [ "$INSET" != 0 ] && TAG="${TAG}_inset${INSET}"
  ARGS=(-tr-autopilot -tr-shots "$OUT" -tr-tag "$TAG" -tr-safe-inset "$INSET")
  [ -n "${TR_TRACK:-}" ] && ARGS+=(-tr-track "$TR_TRACK")
  [ "${TR_FAST:-0}" = 1 ] && ARGS+=(-tr-fast)
  [ -n "${TR_QUALITY:-}" ] && ARGS+=(-tr-quality "$TR_QUALITY")
  rm -rf "$HOME/.config/unity3d/Nocvault/Turbo Rush 3D/turborush_save.json"*
  timeout 900 xvfb-run -a -s "-screen 0 ${W}x${H}x24" Builds/Linux/TurboRush3D.x86_64 -force-glcore -screen-fullscreen 0 \
    -screen-width "$W" -screen-height "$H" "${ARGS[@]}" -logFile "$PWD/Logs/autopilot_${TAG}.log"
  code=$?; [ $code -ne 0 ] && rc=$code
  echo "== $TAG exit=$code"; tail -4 "$OUT/${TAG}_autopilot_report.txt" 2>/dev/null || grep -E "Exception|Error" "Logs/autopilot_${TAG}.log" | head
done
exit $rc
