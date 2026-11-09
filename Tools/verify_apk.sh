#!/usr/bin/env bash
# Independent (non-Unity) APK check: both ABIs, PowerVR deny filter packaged, landscape, no INTERNET in release.
# Usage: Tools/verify_apk.sh <apk>
set -u
APK="$1"
[ -f "$APK" ] || { echo "No APK at $APK"; exit 1; }
SDK="$HOME/Unity/Hub/Editor/6000.3.27f2/Editor/Data/PlaybackEngines/AndroidPlayer/SDK"
AAPT=$(ls -d "$SDK"/build-tools/*/aapt "$HOME"/Android/Sdk/build-tools/*/aapt 2>/dev/null | sort -V | tail -1)
[ -x "$AAPT" ] || { echo "FAIL aapt not found (install Android SDK Build Tools)"; exit 1; }
fail=0
echo "APK: $APK ($(du -h "$APK" | cut -f1))  aapt: $AAPT"
BADGING=$("$AAPT" dump badging "$APK")
echo "\$ aapt dump badging <apk> | grep native-code"
echo "$BADGING" | grep native-code
echo "$BADGING" | grep -E "^package:|sdkVersion|targetSdkVersion|uses-permission|application-label:"
NC=$(echo "$BADGING" | grep native-code)
if echo "$NC" | grep -q "'arm64-v8a'" && echo "$NC" | grep -q "'armeabi-v7a'"; then
  echo "OK   native code: arm64-v8a + armeabi-v7a"
else echo "FAIL native code must list both arm64-v8a and armeabi-v7a"; fail=1; fi
TMP=$(mktemp -d); unzip -q -o "$APK" 'assets/bin/Data/*' -d "$TMP" 2>/dev/null
echo "\$ grep -rlaF '.*PowerVR.*' assets/bin/Data/"
HITS=$(grep -rlaF ".*PowerVR.*" "$TMP/assets/bin/Data/" 2>/dev/null | sed "s|$TMP/||")
if [ -n "$HITS" ]; then echo "$HITS"; echo "OK   PowerVR Vulkan deny filter present in packaged player data"
else echo "FAIL PowerVR filter not found under assets/bin/Data"; fail=1; fi
ls "$TMP/assets/bin/Data/" | grep -E "globalgamemanagers" | sed 's/^/     file: /'
rm -rf "$TMP"
OR=$("$AAPT" dump xmltree "$APK" AndroidManifest.xml | grep -m1 screenOrientation)
echo "$OR" | grep -qE '0x6$|0xb$|0x0$' && echo "OK   landscape orientation ($OR)" || { echo "FAIL orientation: $OR"; fail=1; }
if echo "$BADGING" | grep -q "android.permission.INTERNET"; then
  case "$APK" in *-dev.apk) echo "INFO INTERNET permission present (development build: Unity profiler)";;
                 *) echo "FAIL release APK must not request INTERNET"; fail=1;; esac
else echo "OK   no INTERNET permission"; fi
[ $fail -eq 0 ] && echo "VERIFY: PASS" || echo "VERIFY: FAIL"
exit $fail
