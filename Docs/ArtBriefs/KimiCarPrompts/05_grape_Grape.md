# Blender car model for "Turbo Rush 3D": Grape (car 5 of 8)

You are an expert low-poly 3D vehicle artist and Blender Python (bpy) developer. Write a Blender Python script that
builds an **original, stylised, child-friendly 3D race car** for a mobile arcade racing game called Turbo Rush 3D.
The game is for children: bright, friendly and offline, running on low-end Android phones in Unity (URP).

## The car
- **Name:** Grape (internal id `grape`), unlocked at 5 stars.
- **Main paint colour:** #8F4CEB · **Accent colour:** #FF99E6
- **Character:** A playful little off-road buggy. It is the tallest and most toy-like car.

### Design brief
- Silhouette: a high-riding buggy with a short chunky body, large ground clearance and wheels pushed out to the corners under round arches.
- Cabin: an open-sided cab under a pink (Accent) tubular roll cage made of rounded tubes. Do not model a driver. The seats are simple Trim blocks. The windscreen is Glass.
- Rear: a spare wheel mounted on the back (a full Tyre + Rim, not an animated wheel). Two small round tail lights.
- Front: a chunky Trim bumper with two round headlights and two small round spot lamps on top of the bumper.
- Accent (pink): the roll cage, the spare-wheel hub and the wheel-arch trims.
- Wheels: big knobbly-looking tyres. Use a low-poly bevelled tread band; do not add per-knob geometry.

All eight cars in the game have identical handling, so this car's personality comes only from its shape and colours.
It must be clearly distinguishable from the other seven cars by its silhouette alone:
Blaze (wedge coupé with a wing), Zoom (bubble hatch with a roof light bar), Sunny (muscle coupé with a bonnet scoop),
Mint (smooth electric teardrop), Grape (tall buggy with a roll cage), Tango (rally hatch with spot lamps),
Frost (arrow concept car with twin fins) and Bolt (wide hyper-racer with a big wing).

## Technical specification (must follow exactly)

### Tooling and deliverable
- Write **one self-contained Python script for Blender 5.2 LTS** (bpy). It is run with
  `blender -b --factory-startup --python-exit-code 1 -P car_grape.py` or from Blender's **Scripting** tab.
- The script builds the whole car procedurally from code: no external files, no add-ons, no downloads, no random seeds.
  The result must be identical every time it runs.
- Use only Blender 5.x APIs. Do not use `bgl`, `mesh.vertex_colors`, `material.use_nodes = True` (nodes are always on
  in 5.x), or operators that need a 3D viewport context. Prefer `bmesh` and the `bpy.data` APIs over `bpy.ops` for modelling.
- The script must start with `bpy.ops.wm.read_factory_settings(use_empty=True)` so it always runs on an empty scene.
- At the end, the script writes three files **into the same folder as the script** (`os.path.dirname(os.path.abspath(__file__))`,
  falling back to `~/TurboRush3D_Cars/grape/` when `__file__` is not a real file path, which happens when the script is run from
  Blender's Text Editor). Create the folder if needed and print the absolute output folder:
  1. `Car_grape.blend`: the saved scene.
  2. `Car_grape.fbx`: the export for Unity (export settings below).
  3. `Car_grape_preview.png`: a 1280×720 render from a 3/4 rear view, slightly above the car (the in-game chase camera angle),
     using the `BLENDER_WORKBENCH` engine with material colours and a white background.
     If that fails, fall back to `BLENDER_EEVEE`. A failed preview must not stop the FBX export.
- At the end, print one summary line per object: the object name, its triangle count and its bounding-box size in metres.
  Also print the total triangle count.
- Reply with the complete script in one code block, then the printed summary you expect.

### Units, axes and pivot (critical for the game import)
- Units are metres (scene unit scale 1.0). Real size, not scaled.
- **The front of the car points along Blender −Y.** Z is up, and the car's right side is +X.
- The root object is an Empty named `Car_grape` located at the world origin. Its origin is on the ground (Z = 0),
  centred left–right and centred between the front and rear axles.
- The bottoms of all four tyres touch Z = 0 exactly.
- Apply all transforms on mesh objects (scale 1, rotation 0) before export, except the wheel objects,
  whose origins must sit at the wheel centre (see below).

### Size (gameplay collision is fixed, so stay within these limits)
- Overall length 4.10 m (±0.05), overall width 2.00 m (±0.05, including mirrors), overall height 1.60 m (±0.08).
- Wheel radius 0.40 m (tyre outer radius). Tyre width 0.28–0.34 m.
- Wheelbase 2.55–2.75 m. Track (the distance between the left and right wheel centres) 1.55–1.65 m.
- The tyres sit fully inside the overall width.

### Object hierarchy and names (exact names, case-sensitive)
```
Car_grape            Empty at the origin (the parent of everything)
├── Body            one mesh: the whole body, cabin, glass, lights, wing, trim (everything that does not spin)
├── Wheel_FL        front-left wheel  (tyre + rim, one mesh), origin at the wheel centre
├── Wheel_FR        front-right wheel
├── Wheel_RL        rear-left wheel
├── Wheel_RR        rear-right wheel
└── Body_LOD1       a simplified Body for distant AI cars (wheels included in this mesh, does not need to spin)
```
- "Left" is the car's own left (Blender −X), with the car facing −Y.
- Each wheel's spin axis is the X axis. The game spins wheels around local X and steers the front wheels around local Z,
  so the wheel origin must be exactly at the wheel centre with rotation (0,0,0).
- Wheels must be separate objects, not joined to Body. Make one wheel, then duplicate it as linked or real copies;
  mirror the right-hand wheels so the rim face points outward.

### Polygon budget (low-end Android: a 32-bit phone with a PowerVR GE8320 GPU and 6 cars on screen)
- `Body`: at most **2,800 triangles**.
- Each `Wheel_*`: at most **220 triangles** (12–16 sides around the tyre).
- `Body_LOD1`: at most **900 triangles** (blocky but the same silhouette and colours, wheels included).
- Total for the car without LOD1: at most **3,700 triangles**.
- Low-poly stylised look with flat shading. Use bevels only where they make the silhouette friendlier.
  No subdivision-surface modifier left live (apply it, or don't use it).
- No hidden or inner faces, no faces inside other faces, no duplicate vertices, no zero-area faces, no n-gons with holes.
  Normals must face outward (recalculate them). The game uses back-face culling, so every visible surface must be one-sided and face out.

### Materials (colour comes from material slots, NOT textures)
Use **only** these material names, assigned per face. Use flat colours only: no image textures, no UV maps needed,
no procedural textures. The game recolours the paint at run time and turns each slot into vertex colours, so the
names matter more than the shader.

| Material name | Base colour | Used for |
|---|---|---|
| `Paint`     | #8F4CEB | the main body colour (the game recolours this slot, so all main paint must use it) |
| `Accent`    | #FF99E6 | stripes, wing, skirts, the car's signature details |
| `Trim`      | #26262E | grilles, bumper inserts, diffuser, mirrors' lower half, door gaps, seats |
| `Glass`     | #1F2B47 | all windows (opaque, dark blue; NOT transparent) |
| `Tyre`      | #1A1A1F | tyres |
| `Rim`       | #8C93A3 | wheel hubs (unless the car brief says rims use Accent; then use `Accent` on the rim faces) |
| `HeadLight` | #FFF7D6 | headlights; they glow in the game |
| `TailLight` | #FF2A1F | tail and brake lights; they glow in the game |
| `Glow`      | #33E6FF | optional extra glowing details (only if the car brief asks for them) |

- Use the Principled BSDF with only Base Color set; set Roughness 0.5 and Metallic 0. Set each material's
  `diffuse_color` to the same colour so the Workbench preview shows it.
- Do not create any other materials, and do not leave unused material slots.

### Design rules (original, child-friendly, readable at speed)
- **Completely original design.** It must not resemble any real car model or brand, and must have no badges, logos,
  emblems, text, numbers or licence plates. Do not copy the design of a car from any existing game, film or toy line.
- Stylised, toy-like proportions: a slightly oversized cabin and wheels, rounded friendly shapes, no sharp spikes
  or aggressive 'angry' faces. Headlights can read as friendly eyes.
- The game camera sits about 8.5 m **behind** the car and 3.4 m up. The rear and the top are what players see 95 % of the time,
  so put the most detail and the strongest silhouette there: tail lights, wing or spoiler, diffuser, rear window, roof stripes.
- The colours must stay readable when small on a 6-inch phone screen: big, simple colour blocks rather than fine detail.
- No interior detail beyond what the brief asks for. Windows are opaque dark glass.
- Do not model a driver, a ground plane, shadow planes, lights or cameras in the exported FBX (the preview camera and
  lights may exist in the .blend but must be excluded from the export).

### FBX export settings (use exactly)
Select only the `Car_grape` hierarchy (the root Empty and its 6 children) and call:
```python
bpy.ops.export_scene.fbx(
    filepath=fbx_path, use_selection=True, object_types={'EMPTY', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
    add_leaf_bones=False, bake_anim=False, use_custom_props=False)
```
(This export call has been verified to run in Blender 5.2.3. The game's importer turns and mirrors the model into its own axes using the wheel names, so the names and sides above matter.)

### Self-check the script must perform before exporting (fail loudly with an exception if any check fails)
1. All 7 objects exist with the exact names above, and the wheels and Body are children of `Car_grape`.
2. Triangle counts are within budget.
3. The overall bounding box (Body + wheels) is within the size limits above.
4. The lowest point of every wheel is at Z = 0 within 1 mm, and each wheel's origin is at its centre.
5. The front of the car is at −Y: the HeadLight faces have a mean Y below 0 and the TailLight faces have a mean Y above 0.
6. Only the allowed material names are used, and `Paint`, `Accent`, `Trim`, `Glass`, `Tyre`, `HeadLight` and `TailLight` are all used.
