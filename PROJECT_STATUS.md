# Turbo Rush 3D: project status

_Last updated: 2026-11-10. Version 0.3.1 (versionCode 4): app icon and splash logo. 0.3.0: M7 proper 3D car models. Earlier: 0.2.0 (M9), 0.1.0 (RC)._

Owner feedback 2026-11-10: "It plays well" (informal; the Redmi A3 checklist below has not been filled in yet).

## Starting state (audit)
- The repository was empty (new project; no commits, no unrelated work to preserve).
- Unity **6000.3.27f2** was installed with Android support (SDK build-tools 36, NDK, OpenJDK, aapt) and Linux standalone support.
- No Android device was connected (`adb devices` empty), so all physical-device items are **Pending**.
- The sibling project `littlebuildersworld` showed a proven Redmi A3 setup (IL2CPP, ARMv7 + ARM64, `.*PowerVR.*` Vulkan
  deny filter, release INTERNET stripping). Its approach and its OFL font (Fredoka) were reused. No game content was copied.

## Decisions and assumptions
| Topic | Decision |
|---|---|
| Render pipeline | URP 17.3, seeded from Unity's own 3D template. Three lean URP assets (Low/Medium/High): no HDR, no shadows, no depth/opaque textures, no post-processing. All geometry uses one custom vertex-colour shader (SRP-Batcher compatible). |
| Movement | Track-relative arcade simulation (distance + lateral offset) on an arc-length-resampled centripetal Catmull-Rom loop. No Rigidbody physics. A small outward "corner drift" gives the player something to steer against. |
| Track geometry | Generated at load time from `TrackDefinition` data in 100 m chunks (2 draw calls each), so a new track is pure data. Scenery is seeded, deterministic and merged into a few meshes. |
| Shadows | Soft blob shadows under cars; no realtime shadow maps on any preset (GDD: "limited shadows"). |
| Cars | Cosmetic only (identical tuning) for fairness and no pay-to-win. The player starts 6th of 6 so overtakes happen. |
| Recovery | Barriers line most of each track, so scraping one only slows the car. "Open runoff" sections (Coastal Loop's sandy straight) let the car leave the recovery area; more than 3 s there, or stuck below 3 m/s for 4 s, respawns at the last checkpoint (8 per lap), facing along the track, ghosted for 2 s. |
| Rubber-banding | Subtle and bounded: AI up to 6 % slower when far ahead, 4 % faster when far behind (`AIProfile`). |
| Unit | km/h by default; mph selectable in Settings. |
| Package id | `com.studio.turborush3d` (as suggested in the brief; no prior identity existed). |
| Audio | Original, synthesised in the editor (`AudioSynth`): placeholder-quality but functional and gently mixed. Replace clips in `Assets/ScriptableObjects/AudioLibrary.asset`. |
| UI | Built in code from a generated skin (`UISkin`): original SDF-painted icons and the Fredoka font. Reference resolution 1920×1080; the canvas matches width when the screen is squarer than 16:9, otherwise height. |

**GDD/brief conflicts:** none blocking. The brief's API sketch places Vulkan first with GLES3 as fallback; that order is used,
because Unity's deny list makes PowerVR devices skip Vulkan and fall through to GLES3.

## Implementation vs placeholder
| Area | State |
|---|---|
| Race loop, state machine, countdown, pause/resume/restart, finishing, results | **Implemented** |
| Touch steering (multi-touch, UI-blocker aware), editor mouse/keys, first-race hint | **Implemented** |
| Chase camera (track-based follow, look-ahead, gentle FOV, no shake) | **Implemented** |
| 5 AI opponents (configurable 2–8 racers in total), lanes, overtaking, recovery, positions | **Implemented** |
| Skyway Circuit | **Implemented** (generated geometry; strongest visual polish of the three) |
| Desert Dash, Coastal Loop | **Implemented and playable**, with less art polish than Skyway (functional content milestone) |
| Hilltop Hops (M9) | **Implemented and playable**: rolling hills, windmills, two jumps; same art level as Desert/Coastal |
| Slopes and jumps (M9) | **Implemented**: slope speed effect, kicker ramps, ballistic flight, air steering, safe landing; verified in automated tests and the Linux autopilot. Feel on a phone: **Pending physical-device validation** |
| Menus, garage, track select, settings, save/progression/unlocks | **Implemented** |
| Cars, icons, textures | Original, procedurally generated; stylised low-poly placeholder art |
| Audio | Functional synthesised placeholders |
| Haptics | Implemented (Android vibrator, settings toggle); not verified on hardware |
| Quality presets + auto Low on Redmi-class devices | Implemented; the device behaviour is **Pending** |

## Acceptance checklist
| Item | Status |
|---|---|
| Unity project opens without compilation errors | **Verified in Editor** (batch compile, setup, tests) |
| Landscape orientation works | **Verified in Android build** (manifest `screenOrientation=0xb`, userLandscape) · device **Pending** |
| Player car accelerates automatically | **Verified in automated test** + Linux player autopilot |
| Touch-and-hold left / right steers | Logic **verified in automated test** (screen-half mapping, steering bounds); real touch **Pending physical device** |
| Releasing touch returns steering smoothly | **Verified in automated test** (returns within 1 s, never snaps) |
| Third-person camera behaves correctly | **Verified in Editor/Linux player** screenshots on all 3 tracks |
| Track boundaries and recovery work | **Verified in automated test** |
| AI racers complete the race | **Verified in automated test** (all 3 tracks, 0 respawns) |
| Race position is correct | **Verified in automated test** + HUD PlayMode test |
| Speed and progress HUD are correct | **Verified in automated test** (PlayMode) |
| Countdown, pause, resume, restart, results work | **Verified in automated test** (PlayMode) + Linux autopilot |
| APK installs on 32-bit Redmi A3 | **Pending physical-device validation** |
| APK contains `armeabi-v7a` and `arm64-v8a` | **Verified in Android build** (aapt), see BUILD_VALIDATION.md |
| PowerVR Vulkan deny filter present in packaged player data | **Verified in Android build** (`globalgamemanagers.assets.splitN`) |
| Redmi A3 second-launch regression passes | **Pending physical-device validation** (checklist: `Docs/REDMI_A3_TEST_CHECKLIST.md`) |
| Redmi A3 performance measured and documented | **Pending physical-device validation** |
| UI passes 16:9, 20:9, 16:10 and 4:3 | **Verified in Linux player** screenshots (`Docs/screenshots/`); notch simulated at 20:9 |
| Offline play from launch to results | **Verified in Linux player**; release APK has no INTERNET permission |
| No critical defects remain | None known; on-device behaviour unverified |

## Risks
1. **No on-device validation yet.** The second-launch regression, FPS, thermals and touch feel must be measured on the Redmi A3.
2. Performance is estimated, not measured. The Low preset gives roughly 60–90 draw calls on screen
   (track chunks + 3×3 prop chunks + ground + far scenery + 6 cars), about 30–60k vertices, 0.75 render scale and no MSAA.
   If the A3 falls below 30 FPS, reduce `QualityProfile.For(0)` scenery density or far clip first.
3. The first IL2CPP build for two ABIs takes about 25 minutes on this machine; incremental builds are faster.
4. The release APK is debug-signed (fine for sideload testing).

## Owner actions (need a physical device)
1. Install `Builds/Android/TurboRush3D.apk` on the Redmi A3 and run `Docs/REDMI_A3_TEST_CHECKLIST.md`.
2. Report FPS, thermals and the relaunch results, then update this file and BUILD_VALIDATION.md.
3. Tune steering feel (Settings ▸ Steering, or the `CarTuning` defaults in `CarDefinition.cs`) after children have played it.

## Next milestones (proposed after RC 0.1.0; the GDD's milestones are complete)
| Milestone | Scope | State |
|---|---|---|
| M7: Proper car models | 8 original Blender models (`Art/Cars/`): Blaze, Zoom, Sunny, Mint by Kimi; Grape, Tango, Frost, Bolt by Claude (`Tools/Blender/`). `CarBuilder` converts the FBX material slots into vertex colour, paint mask and emission (one material per car), corrects the importer's axes from the wheel names, and builds prefabs with spinning and steering wheels and a LODGroup (LOD1 for distant cars). All 8 pass `Tools/Blender/check_car_fbx.py` and `CarModelTests`. | **Done** in 0.3.0; on-device look and FPS pending |
| M8: Device validation and performance | Fill in `Docs/REDMI_A3_TEST_CHECKLIST.md` with measured FPS and thermals, and tune the Low preset | Needs the owner's Redmi A3 |
| M9: More content | GDD "later content": slopes and gentle jumps, plus a 4th track (Hilltop Hops) | **Done** in 0.2.0; device feel pending. Correction: the 0.2.0 record of 46/46 EditMode tests came from a run before the sub-step change; one test (`ShippedJumpsAreOnStraightsAndAITakesThemCleanly`) then failed because it stepped at 1/30 s and missed the one-sub-step landing flag. Fixed in 0.3.0 (test-only; game events were correct). |
| M10: Polish | Better engine and music audio, a release keystore and store-ready signing | Not started |
