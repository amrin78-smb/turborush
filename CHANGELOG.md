# Changelog

## 0.2.0 (versionCode 2) — 2026-11-10, M9: slopes, jumps and a fourth track
- **Hilltop Hops** (unlock at 6 ★): 1503 m rolling-hills loop with 17 m of climb, an S-bend over the top, windmills, and two jumps.
- **Jumps**: `JumpRamp` data on `TrackDefinition`. Cars ride up the ramp and fly a ballistic arc from the lip (`RaceTuning`: gravity 14 m/s²,
  35 % steering in the air, 98.5 % speed kept on landing). Airborne cars do not collide with cars below. Jump and landing sounds,
  a landing vibration, and a "BIG JUMP!" banner for long jumps. The car pitches with its flight path and its shadow stays on the road.
- **Slopes** now matter: the target speed drops a little uphill and rises a little downhill (`slopeSpeedEffect`, same for every car).
- The race simulation splits slow frames into ≤ 1/60 s sub-steps, so jumps and contacts behave the same at any frame rate.
- Track-select cards shrink to fit four tracks at every aspect ratio. Track previews mark the jumps.
- Validator: jumps must be in range and on straight road from the ramp to 60 m past the lip.
- Tests: 46 EditMode (10 new: ramp profile, take-off and landing, slow hop, shoulder bypass, air steering, no contact when flying over,
  respawn clears air state, slopes, AI flies every shipped jump) + 3 PlayMode.
- Kimi/Blender prompts for 8 original car models (`Docs/ArtBriefs/`, generator `Tools/gen_kimi_car_prompts.py`).

## 0.1.0 (versionCode 1) — 2026-11-09, first release candidate

### M0: Audit and platform foundation
- New Unity 6000.3.27f2 URP project with a lean package manifest (no analytics, ads, IAP or networking packages).
- `ProjectConfigurator`: landscape only, IL2CPP, ARMv7 + ARM64, min SDK 25, Vulkan then GLES3, and a `VulkanDeviceFilterLists`
  asset (`Assets/Settings/VulkanDeviceFilters.asset`) denying `.*PowerVR.*`. Applied before every Android build by `AndroidBuildGuard`,
  which fails the build if any setting is wrong.
- `BuildScript` menu items, `BuildValidator` (aapt ABI check + scan of `assets/bin/Data/` for the filter), `Tools/verify_apk.sh`.
- `SafeAreaFitter` and `AdaptiveCanvasScaler` (width-match below 16:9, height-match otherwise).
- Release builds strip the INTERNET permission.

### M1–M2: Driving prototype and race loop
- Arc-length `TrackPath` sampler, shared `RacerMotor` (auto-acceleration, smoothed steering, corner drift, surface slowdown, barriers).
- `RaceSimulation`: grid, AI lanes/overtaking/catch-up, car contacts, checkpoints, laps, recovery/respawn, finish order, stable positions.
- `GameStateMachine` with legal-transition whitelist; `RaceManager` drives countdown, racing, pause, finishing, results and restart.
- Hold-to-steer touch input (multi-touch, pause-button blocker) and editor mouse/keyboard; chase camera.

### M3: Visuals and audio
- Runtime track builder: textured asphalt, curbs, verges, coloured barriers, safety fencing, decks and pillars, tunnels, chevrons,
  start gantry, checkpoint flags. Seeded scenery: ground, sea, mountains/mesas, clouds, trees, palms, cacti, rocks, houses.
- Original procedural cars (3 silhouettes × 8 colour schemes) with emissive lights and blob shadows.
- Custom URP shaders (vertex-lit, gradient sky, blob shadow). Synthesised engine, rumble, cues, UI sounds and two music loops.
- HUD (speed, position, lap, progress bar with every racer, pause), countdown/lap/overtake/finish pop-ups, first-race steering hint.

### M4: Menus and progression
- Main menu with turntable showroom, garage, track select with generated map previews, settings
  (music, sound, vibration, units, steering sensitivity, graphics quality).
- Versioned JSON save (atomic write, backup, corruption recovery, v1→v2 migration). Stars, bests, unlocks.
- Low/Medium/High quality presets; Auto picks Low on PowerVR/old Mali/old Adreno or ≤ 4 GB RAM.

### M5: Additional tracks
- Desert Dash and Coastal Loop, authored purely as `TrackDefinition` data.

### M6: QA
- 36 EditMode + 3 PlayMode tests (all passing). Linux autopilot play-through at 16:9, 20:9 (+notch), 16:10 and 4:3.
- Development and release APKs built and validated (both ABIs, PowerVR filter packaged).
- On-device Redmi A3 testing: **pending** (no device attached).
