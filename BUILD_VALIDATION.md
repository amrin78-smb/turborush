# Build validation

Evidence files: `Docs/validation/`. Labels: **Verified in Android build**, **Verified in automated test**,
**Verified in Linux player**, **Pending physical-device validation**.

## Release candidate 0.1.0 (versionCode 1): `Builds/Android/TurboRush3D.apk`
Built 2026-11-09 with Unity 6000.3.27f2 (`Tools/build_android.sh release`), 50.6 MB, SHA-256 in `RC_0.1.0_release_apk.sha256`.
Full log: `Docs/validation/RC_0.1.0_release_apk.txt`.

| Check | Command | Result |
|---|---|---|
| Both ABIs | `aapt dump badging TurboRush3D.apk \| grep native-code` | `native-code: 'arm64-v8a' 'armeabi-v7a'`: **PASS** |
| PowerVR Vulkan deny filter packaged | `unzip` `assets/bin/Data/*` then `grep -rlaF '.*PowerVR.*'` (also `BuildValidator`, 184 files scanned) | found in `assets/bin/Data/globalgamemanagers.assets.split5`: **PASS** |
| Graphics APIs / filter configured | `AndroidBuildGuard` pre-build check | `apis=Vulkan,OpenGLES3`, filter `Assets/Settings/VulkanDeviceFilters.asset` (`deviceName: .*PowerVR.*`): **PASS** |
| Scripting backend | pre-build check | IL2CPP: **PASS** |
| Orientation | `aapt dump xmltree … AndroidManifest.xml` | `screenOrientation=0xb` (userLandscape): **PASS** |
| Offline | `aapt dump badging` | no INTERNET; only `VIBRATE`: **PASS** |
| SDK levels | `aapt dump badging` | min 25, target 36 |

### API note (Unity 6000.3.27f2)
The brief's reference pattern compiles unchanged in 6000.3.27f2: `PlayerSettings.SetUseDefaultGraphicsAPIs`,
`SetGraphicsAPIs(BuildTarget.Android, {Vulkan, OpenGLES3})`, `new VulkanDeviceFilterLists { vulkanDeviceDenyFilters = … }`,
and `PlayerSettings.Android.androidVulkanDeviceFilterListAsset`. The asset is saved with `AssetDatabase.CreateAsset` and
serialises as `m_VulkanDenyFilterList: - deviceName: .*PowerVR.*`.

## M0 development APK (platform probe, empty scene)
`Docs/validation/M0_dev_apk.txt` covered both ABIs and the filter in `globalgamemanagers.assets.split5`. It **PASSED**.
That APK predates gameplay; rebuild it with `Tools/build_android.sh` before profiling. The fix to set
`EditorUserBuildSettings.development` came after it, so new development builds keep INTERNET for the profiler.

## Automated tests (Editor, batch)
| Suite | Result | File |
|---|---|---|
| EditMode | 36/36 passed | `Docs/validation/editmode-results.xml` |
| PlayMode | 3/3 passed | `Docs/validation/playmode-results.xml` |

## Linux test player autopilot (software GL under xvfb; FPS is NOT representative of phones)
All runs **PASS** (menu → garage → tracks → settings → race → pause/resume → finish → results → restart → menu, no errors).
Reports: `Docs/validation/autopilot_reports.txt`.

| Resolution | Aspect | Canvas match | Notch simulation | Result |
|---|---|---|---|---|
| 1920×1080 | 16:9 | height | – | PASS (Skyway, Desert, Coastal) |
| 2400×1080 | 20:9 | height | 90 px left/right inset | PASS |
| 1920×1200 | 16:10 | width | – | PASS |
| 2048×1536 | 4:3 | width | – | PASS |

Screenshots: `Docs/screenshots/` (contact sheet: `layout_contact_sheet.png`).

## Redmi A3 physical device
**Pending physical-device validation.** No device was attached to the build machine.
Second-launch regression, FPS, thermals, selected graphics API: follow `Docs/REDMI_A3_TEST_CHECKLIST.md` and record the results here.

## 0.3.2 (versionCode 5): louder engine, release APK, 2026-11-10
`Tools/verify_apk.sh` **PASS** (`Docs/validation/RC_0.3.2_release_apk.txt`): `arm64-v8a` + `armeabi-v7a`, PowerVR filter packaged,
landscape, VIBRATE only (no INTERNET). Size 54,307,909 bytes · SHA-256 `930498db4cfba3dd04f83d5f9dc42c0aca7bba875fcece14eda85c20a2d5f53a`.
Tests: EditMode 51/51, PlayMode 3/3 (`Docs/validation/*-results.xml`). Engine loop measured in the editor: RMS 0.620, peak 0.837
(old loop RMS about 0.16). Whether the new level sounds right on the Redmi A3: **Pending physical-device validation**.

Owner report on 0.3.1 (informal, 2026-11-10): installed on the Redmi A3, "works fine, very smooth play"; engine too soft (fixed above).
No FPS or thermal numbers were recorded, so the checklist items stay open.

## 0.3.1 (versionCode 4): app icon and splash, release APK, 2026-11-10
Same checks as 0.3.0, all **PASS** (`Docs/validation/RC_0.3.1_release_apk.txt`): both ABIs, PowerVR filter, landscape, no INTERNET.
Size 54,307,889 bytes · SHA-256 `88cfb1853fd52da408a7d761c272cc9d3484a562e098a0b52dd940eef1cc8228`. The APK's launcher icon is an adaptive icon XML; the packaged 432 px background,
432 px transparent foreground and 192 px legacy PNGs were extracted from the APK and checked visually (new car icon, not Unity's).
Tests were not re-run: only editor tooling and Player settings changed (no runtime code).
How the icon and splash look on the Redmi A3 launcher: **Pending physical-device validation**.

## 0.3.0 (versionCode 3), M7: release APK, 2026-11-10
Built with `Tools/build_android.sh release` (3 min 36 s). Evidence: `Docs/validation/RC_0.3.0_release_apk.txt`.

| Check | Result |
|---|---|
| `aapt dump badging … \| grep native-code` | `native-code: 'arm64-v8a' 'armeabi-v7a'` **PASS** |
| PowerVR filter `.*PowerVR.*` in `assets/bin/Data/` | found in `globalgamemanagers.assets.split5` **PASS** |
| Package / version | `com.studio.turborush3d`, versionName 0.3.0, versionCode 3, min SDK 25, target 36 |
| Orientation | `screenOrientation 0xb` (landscape) **PASS** |
| Permissions | VIBRATE only, no INTERNET **PASS** |
| Size / SHA-256 | 53,586,879 bytes (51.1 MB) · `0d75da14fcb73e2d7ffa11dc9cba44834b702b24312989a6ddd60b6699b4b9dc` |
| EditMode / PlayMode tests | 50/50 and 3/3 passed (`Docs/validation/*-results.xml`) |
| Car models | all 8 FBX pass `check_car_fbx.py`; in-game triangles 1,492–3,500 per car (LOD0), LOD1 528–648 |
| Linux autopilot | Hilltop Hops full race at 1920×1080 PASS with the new cars (4 take-offs, 4 landings, player 1st in 76.0 s, 0 errors) (`Docs/validation/autopilot_report_m7_hills.txt`) |
| Redmi A3 install, relaunch regression, FPS with the new cars | **Pending physical-device validation** |

## 0.2.0 (versionCode 2), M9: release APK, 2026-11-10
Built with `Tools/build_android.sh release` (incremental IL2CPP, 4 min 37 s). Evidence: `Docs/validation/RC_0.2.0_release_apk.txt`.

| Check | Result |
|---|---|
| `aapt dump badging … \| grep native-code` | `native-code: 'arm64-v8a' 'armeabi-v7a'` **PASS** |
| PowerVR filter `.*PowerVR.*` in `assets/bin/Data/` | found in `globalgamemanagers.assets.split5` **PASS** |
| Package / version | `com.studio.turborush3d`, versionName 0.2.0, versionCode 2, min SDK 25, target 36 |
| Orientation | `screenOrientation 0xb` (landscape) **PASS** |
| Permissions | VIBRATE only, no INTERNET **PASS** |
| Size / SHA-256 | 53,181,709 bytes (50.7 MB) · `2a172b7a100d7c61434bbb8923930573efb2325b7e52302d0a65ed4416fca62f` |
| EditMode / PlayMode tests | 46/46 and 3/3 recorded, but that run predates the sub-step change; one jump test failed afterwards (test-only bug, fixed in 0.3.0) |
| Linux autopilot | Hilltop Hops full race at 1920×1080 PASS (4 take-offs, 4 landings, longest air 0.67 s, max height 1.17 m); fast runs at 2048×1536, 2400×1080 + 90 px inset and 1920×1200 PASS (`Docs/validation/autopilot_reports_m9.txt`) |
| Redmi A3 install, relaunch regression, FPS | **Pending physical-device validation** |
