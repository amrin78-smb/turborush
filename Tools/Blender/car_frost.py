"""Frost (car 7): a low, arrow-shaped icy concept car with a jet canopy, twin blue fins and turbine wheels.
Run: blender -b --factory-startup --python-exit-code 1 -P Tools/Blender/car_frost.py -- --out Art/Cars/frost
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib, carkit
importlib.reload(carkit)
from carkit import *

CAR = "frost"
COLORS = {"Paint": "#E6F2FF", "Accent": "#26A6FF"}
R, TRACK, FY, RY = 0.37, 1.60, -1.35, 1.35
WZ = 0.47

root = new_scene(CAR)

# Arrow body: (y, W, zb, zbelt, zdeck, wdeck, wheel zone)
SECTIONS = [(-2.18, 0.30, 0.25, 0.34, 0.40, 0.20, False), (-2.08, 0.60, 0.22, 0.44, 0.53, 0.46, False),
            (-1.95, 0.83, 0.22, 0.62, 0.70, 0.68, False), (FY - WZ - 0.03, 0.92, 0.22, 0.79, 0.83, 0.78, False),
            (FY - WZ, 0.93, 0.22, 0.80, 0.84, 0.80, True), (FY + WZ, 0.94, 0.22, 0.80, 0.88, 0.82, True),
            (FY + WZ + 0.03, 0.94, 0.22, 0.80, 0.88, 0.82, False), (RY - WZ - 0.03, 0.94, 0.22, 0.80, 0.90, 0.84, False),
            (RY - WZ, 0.94, 0.22, 0.80, 0.90, 0.84, True), (RY + WZ, 0.94, 0.22, 0.80, 0.92, 0.84, True),
            (RY + WZ + 0.03, 0.94, 0.22, 0.80, 0.92, 0.84, False), (2.08, 0.92, 0.25, 0.80, 0.92, 0.80, False),
            (2.14, 0.86, 0.30, 0.78, 0.88, 0.72, False)]
body_rings = [body_ring(y, W, zb, zbelt, zd, wd * 0.9, crown=0.06, wheel=w, inset=0.62) for y, W, zb, zbelt, zd, wd, w in SECTIONS]

# Jet-fighter canopy far forward, tapering into a fairing.
CAN = [(-1.05, [(0.50, 0.85), (0.48, 0.86), (0.40, 0.87), (0.20, 0.875)]),
       (-0.55, [(0.60, 0.86), (0.58, 0.98), (0.46, 1.08), (0.22, 1.13)]),
       (0.00, [(0.62, 0.88), (0.60, 1.02), (0.48, 1.13), (0.24, 1.18)]),
       (0.50, [(0.60, 0.89), (0.56, 0.99), (0.44, 1.08), (0.20, 1.12)]),
       (1.00, [(0.50, 0.90), (0.46, 0.93), (0.36, 0.97), (0.16, 0.99)]),
       (1.50, [(0.30, 0.91), (0.28, 0.92), (0.22, 0.93), (0.10, 0.935)])]
can_rings = [mirrored_ring(y, pts) for y, pts in CAN]


def can_mats(i, k):
    if i >= 3: return "Paint"                      # fairing behind the canopy
    return "Paint" if k == 0 else "Glass"


body = MeshBuilder()
body.add(loft(body_rings, body_mats, "Paint", "Paint"))
body.add(loft(can_rings, can_mats, "Paint", "Paint"))
# Front: two slim swept headlight blades and a blue splitter.
body.add(box((0.48, -1.97, 0.665), (0.44, 0.07, 0.06), "HeadLight", rot=(-20, 0, 32)), mirror=True)
body.add(box((0, -2.04, 0.235), (1.30, 0.28, 0.035), "Accent", bevel=0.01))
# Two thin blue lines along each side.
body.add(box((0.942, 0.0, 0.66), (0.012, 1.70, 0.03), "Accent"), mirror=True)
body.add(box((0.942, 0.1, 0.58), (0.012, 1.40, 0.022), "Accent"), mirror=True)
body.add(box((0.93, 0.0, 0.29), (0.05, 1.72, 0.14), "Trim", bevel=0.015), mirror=True)
# Rear: twin fins joined by a thin wing, wrap-around light strip, diffuser with blue strakes.
FIN = [(1.40, 0.90), (2.10, 0.90), (2.14, 1.16), (1.98, 1.18)]
body.add(prism(FIN, 0.55, 0.60, "Accent", bevel=0.01), mirror=True)
body.add(box((0, 2.04, 1.12), (1.16, 0.22, 0.035), "Accent", bevel=0.008))
body.add(box((0, 2.145, 0.73), (1.56, 0.03, 0.05), "TailLight"))
body.add(box((0.85, 2.09, 0.73), (0.03, 0.10, 0.05), "TailLight", rot=(0, 0, -35)), mirror=True)
body.add(box((0, 2.08, 0.34), (1.50, 0.16, 0.20), "Trim", bevel=0.02))
for x in (0.24, 0.52):
    body.add(box((x, 2.13, 0.33), (0.03, 0.08, 0.17), "Accent"), mirror=True)
body_ob = body.build("Body", COLORS, root)

wheels = add_wheels(root, COLORS, FY, RY, TRACK, R,
                    lambda: spoke_wheel(R, 0.30, "Accent", "Trim", lambda s: s % 2 == 0, twist=0.5, rim_ratio=0.76))

lod = MeshBuilder()
lod.add(loft(body_rings, body_mats, "Paint", "Paint"))
lod.add(loft([can_rings[i] for i in (0, 2, 4, 5)], lambda i, k: "Glass" if i == 0 or (i == 1 and k != 0) else "Paint", "Paint", "Paint"))
lod_wheels(lod, FY, RY, TRACK, R, "Accent")
lod.add(box((0.575, 1.80, 1.03), (0.05, 0.60, 0.26), "Accent"), mirror=True)
lod.add(box((0, 2.04, 1.12), (1.16, 0.22, 0.035), "Accent"))
lod.add(box((0, 2.145, 0.73), (1.56, 0.03, 0.05), "TailLight"))
lod.add(box((0.50, -1.99, 0.62), (0.34, 0.05, 0.045), "HeadLight", rot=(0, 0, 32)), mirror=True)
lod.add(box((0, 2.08, 0.34), (1.50, 0.16, 0.20), "Trim"))
lod.add(box((0, -2.04, 0.235), (1.30, 0.28, 0.035), "Accent"))
lod_ob = lod.build("Body_LOD1", COLORS, root)

finish(CAR, root, body_ob, wheels, lod_ob, out_dir_from_argv(CAR), spec_limits(4.35, 1.95, 1.20))
