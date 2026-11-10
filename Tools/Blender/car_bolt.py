"""Bolt (car 8): a wide, low closed-wheel hyper-racer with pontoon arches, a swan-neck wing and cyan glow lines.
Run: blender -b --factory-startup --python-exit-code 1 -P Tools/Blender/car_bolt.py -- --out Art/Cars/bolt
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib, carkit
importlib.reload(carkit)
from carkit import *

CAR = "bolt"
COLORS = {"Paint": "#FF5CA8", "Accent": "#33E6FF"}
R, TRACK, FY, RY = 0.38, 1.60, -1.35, 1.35

root = new_scene(CAR)

# Narrow central fuselage, inboard of the wheels: (y, W, zb, zbelt, zdeck, wdeck)
FUS = [(-2.12, 0.30, 0.20, 0.26, 0.32, 0.22), (-1.95, 0.45, 0.18, 0.38, 0.47, 0.36),
       (-1.40, 0.55, 0.18, 0.52, 0.63, 0.46), (-0.70, 0.58, 0.18, 0.62, 0.74, 0.50),
       (0.70, 0.60, 0.18, 0.64, 0.76, 0.52), (1.95, 0.60, 0.18, 0.62, 0.76, 0.50),
       (2.14, 0.56, 0.22, 0.60, 0.72, 0.44)]
fus_rings = [body_ring(y, W, zb, zbelt, zd, wd, sh=0.05) for y, W, zb, zbelt, zd, wd in FUS]

# Compact teardrop canopy with an engine fairing behind it.
CAN = [(-0.80, [(0.36, 0.72), (0.34, 0.73), (0.28, 0.74), (0.14, 0.745)]),
       (-0.35, [(0.46, 0.72), (0.44, 0.86), (0.36, 0.98), (0.18, 1.04)]),
       (0.15, [(0.48, 0.74), (0.46, 0.90), (0.38, 1.04), (0.18, 1.10)]),
       (0.55, [(0.44, 0.75), (0.40, 0.86), (0.32, 0.96), (0.16, 1.00)]),
       (1.30, [(0.30, 0.76), (0.27, 0.80), (0.22, 0.83), (0.10, 0.85)])]
can_rings = [mirrored_ring(y, pts) for y, pts in CAN]
can_mats = lambda i, k: "Paint" if i >= 3 or k == 0 else "Glass"

# Side pod (between the wheels) side profile (y, z), extruded outward.
POD = [(-0.82, 0.20), (0.86, 0.20), (0.86, 0.60), (-0.50, 0.66), (-0.82, 0.52)]
BOLT = [(-0.30, 0.58), (0.10, 0.58), (-0.02, 0.45), (0.22, 0.45), (-0.20, 0.25), (-0.06, 0.40), (-0.28, 0.40)]

body = MeshBuilder()
body.add(loft(fus_rings, body_mats, "Paint", "Paint"))
body.add(loft(can_rings, can_mats, "Paint", "Paint"))
for y in (FY, RY):  # pontoon fenders over every wheel, with cyan edges
    body.add(arch((y, R), 0.55, 0.96, R + 0.05, R + 0.20, "Paint", 2, 178, 9), mirror=True)
    body.add(arch((y, R), 0.96, 0.99, R + 0.04, R + 0.21, "Accent", 2, 178, 9), mirror=True)
body.add(prism(POD, 0.55, 0.95, "Paint", bevel=0.04), mirror=True)
body.add(box((0.95, 0.10, 0.62), (0.04, 1.30, 0.04), "Accent", rot=(2.5, 0, 0)), mirror=True)   # pod top edge
body.add(box((0.955, 0.02, 0.215), (0.03, 1.62, 0.035), "Glow"), mirror=True)                 # skirt glow line
body.add(prism(BOLT, 0.945, 0.965, "Accent"), mirror=True)                                     # lightning decal
# Front: low splitter with a full-width cyan glow strip, small angled headlights on the pontoons.
body.add(box((0, -2.04, 0.20), (1.90, 0.26, 0.05), "Trim", bevel=0.01))
body.add(box((0, -2.175, 0.205), (1.86, 0.03, 0.045), "Glow"))
body.add(box((0.76, -1.775, 0.805), (0.24, 0.13, 0.035), "HeadLight", rot=(45, 0, 0)), mirror=True)
# Rear deck closing the rear pontoons, light bar, diffuser ringed with glow.
body.add(box((0, 1.98, 0.42), (1.84, 0.36, 0.44), "Paint", bevel=0.05))
body.add(box((0, 2.165, 0.55), (1.60, 0.03, 0.07), "TailLight"))
body.add(box((0, 2.12, 0.27), (1.40, 0.14, 0.16), "Trim", bevel=0.02))
body.add(box((0, 2.195, 0.36), (1.40, 0.02, 0.025), "Glow"))
body.add(box((0.71, 2.195, 0.27), (0.025, 0.02, 0.18), "Glow"), mirror=True)
for x in (0.0, 0.35):
    body.add(box((x, 2.17, 0.26), (0.025, 0.06, 0.14), "Trim"), mirror=(x != 0))
# Two-element rear wing on one swan-neck pylon, cyan end plates.
body.add(box((0, 2.04, 1.04), (1.80, 0.26, 0.04), "Paint", bevel=0.01, rot=(-6, 0, 0)))
body.add(box((0, 2.10, 1.13), (1.80, 0.15, 0.03), "Paint", bevel=0.008, rot=(-18, 0, 0)))
body.add(box((0.915, 2.04, 1.07), (0.03, 0.30, 0.24), "Accent", bevel=0.008), mirror=True)
for a, b in [((0, 1.55, 0.74), (0, 1.82, 1.16)), ((0, 1.82, 1.16), (0, 2.00, 1.17)), ((0, 2.00, 1.17), (0, 2.02, 1.07))]:
    body.add(cylinder(a, b, 0.035, "Trim", 6))
body_ob = body.build("Body", COLORS, root)

wheels = add_wheels(root, COLORS, FY, RY, TRACK, R,
                    lambda: spoke_wheel(R, 0.30, "Accent", "Trim", lambda s: s % 4 == 0, rim_ratio=0.76))

lod = MeshBuilder()
lod.add(loft(fus_rings, body_mats, "Paint", "Paint"))
lod.add(loft([can_rings[i] for i in (0, 2, 4)], lambda i, k: "Paint" if i == 1 or k == 0 else "Glass", "Paint", "Paint"))
lod_wheels(lod, FY, RY, TRACK, R, "Accent")
for y in (FY, RY):
    lod.add(arch((y, R), 0.55, 0.99, R + 0.05, R + 0.20, "Paint", 2, 178, 4), mirror=True)
lod.add(box((0.75, 0.02, 0.43), (0.40, 1.68, 0.46), "Paint"), mirror=True)
lod.add(box((0.955, 0.02, 0.215), (0.03, 1.62, 0.035), "Glow"), mirror=True)
lod.add(box((0, -2.04, 0.20), (1.90, 0.26, 0.05), "Glow"))
lod.add(box((0, 1.98, 0.42), (1.84, 0.36, 0.44), "Paint"))
lod.add(box((0, 2.165, 0.55), (1.60, 0.03, 0.07), "TailLight"))
lod.add(box((0, 2.04, 1.06), (1.86, 0.26, 0.06), "Accent"))
lod.add(box((0, 1.85, 0.92), (0.06, 0.30, 0.30), "Trim"))
lod.add(box((0.76, -1.775, 0.805), (0.24, 0.13, 0.035), "HeadLight", rot=(45, 0, 0)), mirror=True)
lod.add(box((0, 2.12, 0.27), (1.40, 0.14, 0.16), "Trim"))
lod_ob = lod.build("Body_LOD1", COLORS, root)

finish(CAR, root, body_ob, wheels, lod_ob, out_dir_from_argv(CAR), spec_limits(4.40, 2.00, 1.20))
