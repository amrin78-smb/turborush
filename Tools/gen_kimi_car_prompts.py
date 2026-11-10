import os, sys, zipfile
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)

CARS = [
 # id, name, order, stars, paint, accent, length, width, height, wheel radius, brief
 ("blaze","Blaze",1,0,"#ED291F","#FFFFFF",4.25,1.95,1.25,0.37,
  "The starter car and the game's mascot, so it should be the most friendly and readable of the eight.",
  """- Silhouette: low, sporty wedge coupé. The nose is low and rounded, and the roofline rises to a short cabin set slightly behind centre.
- Cabin: a small bubble canopy with big rounded windows. The rear window is clearly visible from the chase camera.
- Rear (the most important view): a wide rear wing on two short dark struts. Two large rounded tail-light pods and a dark diffuser panel with two round exhaust tips.
- Accent (white): two parallel racing stripes running from the bonnet, over the roof, to the boot, plus white side skirts.
- Front: two large round headlights that read like friendly eyes, and a small smiling air intake in Trim.
- Wheels: chunky, with 5-spoke Rim hubs. The wheel arches are slightly flared."""),
 ("zoom","Zoom",2,1,"#1A73F5","#FFD91A",4.0,1.95,1.5,0.40,
  "A cheerful, bouncy compact. It is the second car a child unlocks.",
  """- Silhouette: a rounded 'bubble' hatchback, short and tall, almost egg-shaped from the side, with big wheels at the corners.
- Cabin: a tall glasshouse with a large windscreen. The roof is painted.
- Roof: a yellow (Accent) roof light bar, a flat rounded box across the roof. It is cosmetic only, with no emergency markings.
- Rear: an almost vertical hatch with a big rear window. Two tall vertical tail-light strips, and a small yellow roof spoiler above the rear window.
- Front: big round headlights and a rounded bumper in Trim.
- Accent (yellow): the roof light bar, the roof spoiler and a thin stripe along the waistline on both sides.
- Wheels: the largest in the line-up (radius 0.40 m), with simple dished Rim hubs in Accent yellow."""),
 ("sunny","Sunny",3,2,"#FFCC14","#1F1F26",4.4,2.0,1.3,0.38,
  "A chunky, friendly muscle-style coupé. It is the 'strong' looking car.",
  """- Silhouette: long bonnet, short boot and a compact cabin set well back. The body is broad and boxy with soft bevelled edges.
- Bonnet: a raised scoop in the centre of the bonnet in Trim, and two broad near-black (Accent) stripes over the bonnet, roof and boot.
- Cabin: a low roof painted in Accent near-black, with slightly narrow windows.
- Rear: a full-width tail-light bar split into three segments. A small ducktail lip spoiler, and twin rectangular exhaust tips in Trim.
- Front: a wide grille in Trim (a simple inset panel, no logo) and two pairs of round headlights.
- Wheels: wide, with deep Rim hubs. The rear tyres are about 10 % wider than the front."""),
 ("mint","Mint",4,4,"#2ECC85","#FFFFFF",4.15,1.95,1.35,0.37,
  "A smooth, modern, quiet 'electric' runabout. Calm and clean rather than aggressive.",
  """- Silhouette: one smooth arc from the nose to the tail, with a teardrop cabin. There are no sharp creases; the panels are softly rounded.
- Front: no grille. A single thin HeadLight strip runs across the full width of the nose.
- Rear: a single thin TailLight strip across the full width of the tail, and a smooth tapered tail with a small integrated lip.
- Cabin: a panoramic glass roof made of the Glass material and framed by white (Accent) roof rails.
- Accent (white): the roof frame, the lower door panels (a two-tone effect) and the mirror caps.
- Wheels: aero-style covered Rim discs (flat, with 3 shallow slots) so the wheels read as rounded discs."""),
 ("grape","Grape",5,5,"#8F4CEB","#FF99E6",4.1,2.0,1.6,0.40,
  "A playful little off-road buggy. It is the tallest and most toy-like car.",
  """- Silhouette: a high-riding buggy with a short chunky body, large ground clearance and wheels pushed out to the corners under round arches.
- Cabin: an open-sided cab under a pink (Accent) tubular roll cage made of rounded tubes. Do not model a driver. The seats are simple Trim blocks. The windscreen is Glass.
- Rear: a spare wheel mounted on the back (a full Tyre + Rim, not an animated wheel). Two small round tail lights.
- Front: a chunky Trim bumper with two round headlights and two small round spot lamps on top of the bumper.
- Accent (pink): the roll cage, the spare-wheel hub and the wheel-arch trims.
- Wheels: big knobbly-looking tyres. Use a low-poly bevelled tread band; do not add per-knob geometry."""),
 ("tango","Tango",6,7,"#FF7A14","#FFFFFF",4.1,1.95,1.45,0.37,
  "A spirited rally hatchback that looks like it just came off a gravel stage.",
  """- Silhouette: a compact hatchback with boxy flared wheel arches, a slightly raised ride height and a short overhang at the front.
- Bonnet: a row of four round spot lamps (HeadLight) mounted on a Trim bar in front of the grille.
- Roof: a low flat roof scoop on the front of the roof and a large flat roof-edge rear spoiler in Accent white.
- Rear: square tail lights, Trim mud flaps behind every wheel, and a single round exhaust.
- Accent (white): the rear spoiler, a big plain white circle on each front door (a race-number roundel with no number or text in it) and thin side stripes.
- Wheels: white (Accent) 6-spoke Rim hubs."""),
 ("frost","Frost",7,8,"#E6F2FF","#26A6FF",4.35,1.95,1.2,0.37,
  "A sleek concept supercar with an icy look. It is the second-to-last unlock and should feel premium.",
  """- Silhouette: a very low, long arrow-shaped body with a pointed but rounded nose. The cabin is a jet-fighter style canopy far forward.
- Fins: two short vertical fins at the rear in Accent ice blue, joined by a thin rear wing.
- Front: two slim angled HeadLight blades. A blue (Accent) splitter at the bottom of the nose.
- Rear: a thin TailLight strip that wraps from side to side, and a large Trim diffuser with blue (Accent) vertical strakes.
- Accent (ice blue): the fins, the splitter, the diffuser strakes and two thin lines running along the sides.
- Wheels: low-profile tyres with blue (Accent) turbine-style Rim hubs (several curved blades)."""),
 ("bolt","Bolt",8,9,"#FF5CA8","#33E6FF",4.4,2.0,1.2,0.38,
  "The final, most special car (9 stars). It should look like the ultimate futuristic racer.",
  """- Silhouette: a wide, low hyper-racer with pronounced pontoon front wheel arches and a narrow central cabin, like a closed-wheel race car.
- Cabin: a compact teardrop canopy.
- Rear: a large two-element rear wing on a single central swan-neck pylon. A wide full-width TailLight bar under the wing, and a big Trim diffuser.
- Front: a low nose with a full-width cyan Glow strip (see the Glow material) and two small angled HeadLights.
- Accent (cyan): the wing end-plates, the edges of the side pods and lightning-bolt-shaped decals made of simple polygons (one on each door, no text).
- Glow: cyan glowing lines along the bottom edge of the side skirts and around the diffuser, using the extra Glow material.
- Wheels: low-profile tyres with cyan (Accent) Rim hubs in a 'Y' 3-spoke design."""),
]

SHARED = """## Technical specification (must follow exactly)

### Tooling and deliverable
- Write **one self-contained Python script for Blender 5.2 LTS** (bpy). It is run with
  `blender -b --factory-startup --python-exit-code 1 -P car_{id}.py` or from Blender's **Scripting** tab.
- The script builds the whole car procedurally from code: no external files, no add-ons, no downloads, no random seeds.
  The result must be identical every time it runs.
- Use only Blender 5.x APIs. Do not use `bgl`, `mesh.vertex_colors`, `material.use_nodes = True` (nodes are always on
  in 5.x), or operators that need a 3D viewport context. Prefer `bmesh` and the `bpy.data` APIs over `bpy.ops` for modelling.
- The script must start with `bpy.ops.wm.read_factory_settings(use_empty=True)` so it always runs on an empty scene.
- At the end, the script writes three files **into the same folder as the script** (`os.path.dirname(os.path.abspath(__file__))`,
  falling back to `~/TurboRush3D_Cars/{id}/` when `__file__` is not a real file path, which happens when the script is run from
  Blender's Text Editor). Create the folder if needed and print the absolute output folder:
  1. `Car_{id}.blend`: the saved scene.
  2. `Car_{id}.fbx`: the export for Unity (export settings below).
  3. `Car_{id}_preview.png`: a 1280×720 render from a 3/4 rear view, slightly above the car (the in-game chase camera angle),
     using the `BLENDER_WORKBENCH` engine with material colours and a white background.
     If that fails, fall back to `BLENDER_EEVEE`. A failed preview must not stop the FBX export.
- At the end, print one summary line per object: the object name, its triangle count and its bounding-box size in metres.
  Also print the total triangle count.
- Reply with the complete script in one code block, then the printed summary you expect.

### Units, axes and pivot (critical for the game import)
- Units are metres (scene unit scale 1.0). Real size, not scaled.
- **The front of the car points along Blender −Y.** Z is up, and the car's right side is +X.
- The root object is an Empty named `Car_{id}` located at the world origin. Its origin is on the ground (Z = 0),
  centred left–right and centred between the front and rear axles.
- The bottoms of all four tyres touch Z = 0 exactly.
- Apply all transforms on mesh objects (scale 1, rotation 0) before export, except the wheel objects,
  whose origins must sit at the wheel centre (see below).

### Size (gameplay collision is fixed, so stay within these limits)
- Overall length {length:.2f} m (±0.05), overall width {width:.2f} m (±0.05, including mirrors), overall height {height:.2f} m (±0.08).
- Wheel radius {wheel:.2f} m (tyre outer radius). Tyre width 0.28–0.34 m.
- Wheelbase 2.55–2.75 m. Track (the distance between the left and right wheel centres) 1.55–1.65 m.
- The tyres sit fully inside the overall width.

### Object hierarchy and names (exact names, case-sensitive)
```
Car_{id}            Empty at the origin (the parent of everything)
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
| `Paint`     | {paint} | the main body colour (the game recolours this slot, so all main paint must use it) |
| `Accent`    | {accent} | stripes, wing, skirts, the car's signature details |
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
Select only the `Car_{id}` hierarchy (the root Empty and its 6 children) and call:
```python
bpy.ops.export_scene.fbx(
    filepath=fbx_path, use_selection=True, object_types={{'EMPTY', 'MESH'}},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
    add_leaf_bones=False, bake_anim=False, use_custom_props=False)
```
(This export call has been verified to run in Blender 5.2.3. With the car facing −Y, it imports into Unity facing +Z.)

### Self-check the script must perform before exporting (fail loudly with an exception if any check fails)
1. All 7 objects exist with the exact names above, and the wheels and Body are children of `Car_{id}`.
2. Triangle counts are within budget.
3. The overall bounding box (Body + wheels) is within the size limits above.
4. The lowest point of every wheel is at Z = 0 within 1 mm, and each wheel's origin is at its centre.
5. The front of the car is at −Y: the HeadLight faces have a mean Y below 0 and the TailLight faces have a mean Y above 0.
6. Only the allowed material names are used, and `Paint`, `Accent`, `Trim`, `Glass`, `Tyre`, `HeadLight` and `TailLight` are all used.
"""

def prompt(c):
    cid, name, order, stars, paint, accent, L, W, H, wr, intro, brief = c
    unlock = "owned from the start" if stars == 0 else f"unlocked at {stars} stars"
    head = f"""# Blender car model for "Turbo Rush 3D": {name} (car {order} of 8)

You are an expert low-poly 3D vehicle artist and Blender Python (bpy) developer. Write a Blender Python script that
builds an **original, stylised, child-friendly 3D race car** for a mobile arcade racing game called Turbo Rush 3D.
The game is for children: bright, friendly and offline, running on low-end Android phones in Unity (URP).

## The car
- **Name:** {name} (internal id `{cid}`), {unlock}.
- **Main paint colour:** {paint} · **Accent colour:** {accent}
- **Character:** {intro}

### Design brief
{brief}

All eight cars in the game have identical handling, so this car's personality comes only from its shape and colours.
It must be clearly distinguishable from the other seven cars by its silhouette alone:
Blaze (wedge coupé with a wing), Zoom (bubble hatch with a roof light bar), Sunny (muscle coupé with a bonnet scoop),
Mint (smooth electric teardrop), Grape (tall buggy with a roll cage), Tango (rally hatch with spot lamps),
Frost (arrow concept car with twin fins) and Bolt (wide hyper-racer with a big wing).

"""
    return head + SHARED.format(id=cid, length=L, width=W, height=H, wheel=wr, paint=paint, accent=accent)

files = []
for c in CARS:
    fn = f"{c[2]:02d}_{c[0]}_{c[1]}.md"
    with open(os.path.join(OUT, fn), "w") as f: f.write(prompt(c))
    files.append(fn)

readme = """# Turbo Rush 3D: Kimi prompts for the car models

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
"""
with open(os.path.join(OUT, "README.md"), "w") as f: f.write(readme)

zpath = os.path.join(os.path.dirname(OUT.rstrip("/")), "TurboRush3D_Kimi_Car_Prompts.zip")
with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
    for fn in ["README.md"] + files:
        z.write(os.path.join(OUT, fn), "TurboRush3D_Kimi_Car_Prompts/" + fn)
print(zpath)
