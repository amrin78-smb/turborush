# Turbo Rush 3D: Kimi prompts for the car models

One prompt per car. Each file is self-contained: paste one whole file into a **new** Kimi chat.

## Steps for each car
1. Open a new Kimi chat and paste the entire contents of one prompt file (for example `01_blaze_Blaze.md`).
2. Kimi replies with a Blender Python script. Save it as `car_<id>.py` (for example `car_blaze.py`) in an empty folder.
3. Run it, either:
   - from a terminal: `blender -b --factory-startup --python-exit-code 1 -P car_blaze.py`, or
   - in Blender: **Scripting** tab ▸ Open ▸ `car_blaze.py` ▸ **Run Script** (save the script to disk first, so it knows where to write files).
4. It writes `Car_<id>.blend`, `Car_<id>.fbx` and `Car_<id>_preview.png` next to the script.
   Open the preview and check the look. If it is wrong, tell Kimi what to change in the same chat.
5. If the script raises an error (its self-check is strict on purpose), paste the full error back into the same Kimi chat
   and ask it to fix the script.
6. When you are happy, copy the whole folder into the repo at `Art/Incoming/Cars/<id>/`
   (the `.py`, `.fbx`, `.blend` and `.png`), or send the files to Claude Code. The game import (fitting the paint colour,
   glowing lights, spinning and steering wheels, distance LOD) is handled on the Unity side.

## The eight cars
| # | File | Car | Unlock | Look |
|---|---|---|---|---|
| 1 | 01_blaze_Blaze.md | Blaze | start | red wedge coupé, white stripes, rear wing |
| 2 | 02_zoom_Zoom.md | Zoom | 1 ★ | blue bubble hatch, yellow roof light bar |
| 3 | 03_sunny_Sunny.md | Sunny | 2 ★ | yellow muscle coupé, black stripes, bonnet scoop |
| 4 | 04_mint_Mint.md | Mint | 4 ★ | green smooth electric teardrop |
| 5 | 05_grape_Grape.md | Grape | 5 ★ | purple off-road buggy, pink roll cage |
| 6 | 06_tango_Tango.md | Tango | 7 ★ | orange rally hatch, spot lamps |
| 7 | 07_frost_Frost.md | Frost | 8 ★ | ice-white arrow concept, blue fins |
| 8 | 08_bolt_Bolt.md | Bolt | 9 ★ | pink hyper-racer, cyan glow, big wing |

## Rules every prompt enforces (so the models drop straight into the game)
- Blender 5.2 LTS script, deterministic, no add-ons or external files.
- Metres, front of the car along Blender −Y, origin on the ground between the axles, tyres touching Z = 0.
- Exact object names: `Car_<id>`, `Body`, `Wheel_FL/FR/RL/RR` (origin at the wheel centre), `Body_LOD1`.
- Budget: Body ≤ 2,800 triangles, each wheel ≤ 220, LOD1 ≤ 900 (32-bit PowerVR phone, 6 cars on screen).
- Colour from named material slots only (`Paint`, `Accent`, `Trim`, `Glass`, `Tyre`, `Rim`, `HeadLight`, `TailLight`, `Glow`). No textures.
- Original designs only: no real car look-alikes, no logos, badges, text or plates.
- FBX export with fixed settings (verified to run in Blender 5.2.3).
