#!/usr/bin/env bash
# Regenerates all content (settings, art, audio, cars, tracks, scenes) and validates. Log: Logs/setup.log
set -u
cd "$(dirname "$0")/.."
# Car models: Art/Cars/<id>/Car_<id>.fbx (Blender sources) -> Unity import folder.
mkdir -p Assets/Art/Models/Vehicles/Source
cp Art/Cars/*/Car_*.fbx Assets/Art/Models/Vehicles/Source/
Tools/unity_batch.sh TurboRush.EditorTools.ProjectSetup.RunBatch setup
code=$?
grep -E "\[TR|error CS|Exception" Logs/setup.log | grep -v "^UnityEngine\|^  at " | head -80
echo "SETUP exit=$code"
exit $code
