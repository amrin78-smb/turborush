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
