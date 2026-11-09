# Turbo Rush 3D: project rules

- Product source of truth: the Turbo Rush 3D GDD + master prompt (owner-supplied). Status lives in `PROJECT_STATUS.md`; keep it honest.
- Unity **6000.3.27f2**, URP 17.3, Input System, uGUI + TMP. Android landscape, offline, child-friendly. Project at repo root.
- Runtime code: `Assets/Scripts/Runtime` (asmdef `TurboRush.Runtime`). Editor/build/generators: `Assets/Editor`.
- Art, audio, cars, tracks, UI skin and scenes are **generated** by `Tools/setup.sh`. Edit the generator, not the generated asset.
- One ScriptableObject/MonoBehaviour class per file, with a matching file name (otherwise asset references break).
- Redmi A3 rules: every Android build is IL2CPP **ARMv7 + ARM64** with the Vulkan deny filter `.*PowerVR.*` (`ProjectConfigurator`).
  Verify each APK with `Tools/verify_apk.sh`. On-device tests must include a full close and relaunch.
- No ads, analytics, accounts, network, IAP, tracking or external links. Keep `Packages/manifest.json` minimal.
- Commands: `Tools/setup.sh`, `Tools/run_tests.sh`, `Tools/build_android.sh [release]`, `Tools/build_linux.sh`, `Tools/autopilot.sh`.
  Only one Unity process may open the project at a time.
- Never claim a build, test or device result without evidence.
