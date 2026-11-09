# Redmi A3 physical-device test checklist

**Status: Pending physical-device validation.** No device was connected to the build machine (`adb devices` was empty).

Run this checklist for every release candidate and after any graphics or build-setting change.

## Setup
```bash
adb devices                                   # device listed, USB debugging authorised
adb uninstall com.studio.turborush3d          # first run of a new candidate: clean install
adb install -r Builds/Android/TurboRush3D.apk
adb logcat -c
adb logcat -v time Unity:V CRASH:V AndroidRuntime:E DEBUG:V libc:F '*:S' > redmi_a3_log.txt   # keep running
```
The game logs one line at start-up, for example:
`[TR Quality] Low (auto: low-end GPU (PowerVR Rogue GE8320)); GPU=…; API=OpenGLES3; RAM=…MB`
The **API must be `OpenGLES3`** on the Redmi A3. That confirms the PowerVR Vulkan deny filter took effect.

## Second-launch regression (mandatory)
| Step | Action | Pass? |
|---|---|---|
| 1 | Install the APK (above) | |
| 2 | Launch. Main menu appears in landscape | |
| 3 | Play, race at least 2 minutes (Race Again if the first race ends sooner) | |
| 4 | Swipe the app away in Android Recents (full close) | |
| 5 | Relaunch from the launcher | |
| 6 | Start a second race and play at least 2 minutes | |
| 7 | Repeat steps 4–6 again (at least two full close/relaunch cycles in total) | |

Any crash, black screen or freeze at a relaunch is a **release-blocking failure**. Attach `redmi_a3_log.txt`.

## Record
| Field | Value |
|---|---|
| Device model / RAM | Redmi A3 / |
| Android version | |
| APK file / versionName / versionCode | TurboRush3D.apk / 0.1.0 / 1 |
| Graphics API in the `[TR Quality]` log line | |
| Quality preset selected (Settings ▸ Graphics shows *Auto*; log shows the level) | |
| FPS while racing (`adb shell dumpsys gfxinfo com.studio.turborush3d`, or the dev APK with the Unity Profiler) | |
| Lowest FPS seen (6 cars on screen, start grid) | |
| Device temperature / throttling after 10 min | |
| Load time menu → race | |
| Launch 1 / relaunch 1 / relaunch 2 result | |
| Crashes or logcat errors | |

## Functional checks on the device
- [ ] Holding the left half steers left; holding the right half steers right; release returns to centre smoothly
- [ ] Touching the pause button does not steer; two fingers at once does not glitch
- [ ] The car starts moving only after GO; it does not move during the countdown
- [ ] Pause freezes everything; Resume, Restart and Menu all work
- [ ] Results show place, time and stars; Race Again and Continue work
- [ ] Unlocks persist after a full close/relaunch
- [ ] Settings (music, sound, vibration, units, steering, graphics) persist after a relaunch
- [ ] Vibration is felt on GO, bumps and the finish (when enabled), and none when disabled
- [ ] No UI is clipped by the notch, rounded corners or gesture bar
- [ ] Airplane mode: the whole flow works offline
