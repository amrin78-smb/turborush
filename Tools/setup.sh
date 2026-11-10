#!/usr/bin/env bash
# Regenerates all content (settings, art, audio, cars, tracks, scenes) and validates. Log: Logs/setup.log
set -u
cd "$(dirname "$0")/.."
# Car models: Art/Cars/<id>/Car_<id>.fbx (Blender sources) -> Unity import folder.
mkdir -p Assets/Art/Models/Vehicles/Source
cp Art/Cars/*/Car_*.fbx Assets/Art/Models/Vehicles/Source/
Tools/unity_batch.sh TurboRush.EditorTools.ProjectSetup.RunBatch setup
code=$?
# Launcher icon + splash logo are rendered in-engine in a separate Unity run (rendering needs a fresh graphics setup).
if [ $code -eq 0 ]; then
  Tools/unity_batch.sh TurboRush.EditorTools.AppIconBuilder.BuildBatch icon; code=$?
  grep -E "\[TR\]|error CS|Exception" Logs/icon.log | head -5
fi
grep -E "\[TR|error CS|Exception" Logs/setup.log | grep -v "^UnityEngine\|^  at " | head -80
echo "SETUP exit=$code"
exit $code
