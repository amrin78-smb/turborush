#!/usr/bin/env bash
# Android APK (development by default; "release" for release), then independent shell verification.
set -u
cd "$(dirname "$0")/.."
METHOD=AndroidDevelopmentBatch; APK=Builds/Android/TurboRush3D-dev.apk
if [ "${1:-}" = "release" ]; then METHOD=AndroidReleaseBatch; APK=Builds/Android/TurboRush3D.apk; fi
Tools/unity_batch.sh TurboRush.EditorTools.BuildScript.$METHOD android_build -buildTarget Android
code=$?
echo "ANDROID BUILD exit=$code"; grep -E "\[TR (Build|Validate|Config)\]" Logs/android_build.log | head -40
[ $code -ne 0 ] && { grep -E "error CS|BuildFailedException|Error building" Logs/android_build.log | head -20; exit $code; }
Tools/verify_apk.sh "$APK"
