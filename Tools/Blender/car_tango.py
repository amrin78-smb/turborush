"""Tango (car 6): a spirited rally hatchback with four spot lamps, a white roof spoiler and race roundels.
Run: blender -b --factory-startup --python-exit-code 1 -P Tools/Blender/car_tango.py -- --out Art/Cars/tango
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib, carkit
importlib.reload(carkit)
from carkit import *

CAR = "tango"
COLORS = {"Paint": "#FF7A14", "Accent": "#FFFFFF"}
R, TRACK, FY, RY = 0.37, 1.60, -1.30, 1.30
WZ = 0.47  # half-length of each wheel well

root = new_scene(CAR)

# Body: (y, W, zb, zbelt, zdeck, wdeck, wheel zone)
SECTIONS = [(-1.92, 0.80, 0.34, 0.66, 0.74, 0.64, False), (-1.88, 0.89, 0.30, 0.80, 0.84, 0.74, False),
            (FY - WZ - 0.03, 0.92, 0.30, 0.83, 0.87, 0.78, False), (FY - WZ, 0.92, 0.30, 0.83, 0.88, 0.78, True),
            (FY + WZ, 0.92, 0.30, 0.83, 0.95, 0.80, True), (FY + WZ + 0.03, 0.92, 0.30, 0.83, 0.95, 0.80, False),
            (RY - WZ - 0.03, 0.92, 0.30, 0.83, 0.98, 0.82, False), (RY - WZ, 0.92, 0.30, 0.83, 0.98, 0.82, True),
            (RY + WZ, 0.92, 0.30, 0.83, 1.00, 0.82, True), (RY + WZ + 0.03, 0.92, 0.30, 0.83, 1.00, 0.82, False),
            (1.98, 0.91, 0.30, 0.83, 1.00, 0.80, False), (2.02, 0.86, 0.34, 0.80, 0.96, 0.74, False)]
body_rings = [body_ring(y, W, zb, zbelt, zd, wd, wheel=w) for y, W, zb, zbelt, zd, wd, w in SECTIONS]

# Cabin (greenhouse): right side (x, z) points: door top, window bottom, roof edge.
CAB = [(-0.60, [(0.80, 0.94), (0.79, 0.96), (0.74, 0.98)]),
       (0.00, [(0.82, 0.94), (0.80, 1.06), (0.66, 1.37)]),
       (0.62, [(0.82, 0.95), (0.80, 1.07), (0.66, 1.37)]),
       (0.74, [(0.82, 0.95), (0.80, 1.07), (0.66, 1.37)]),
       (1.62, [(0.82, 0.97), (0.80, 1.08), (0.66, 1.37)]),
       (1.96, [(0.80, 0.98), (0.79, 1.00), (0.72, 1.02)])]
cab_rings = [mirrored_ring(y, pts) for y, pts in CAB]


def cab_mats(i, k):
    if i == 0: return "Glass" if k == "top" else "Paint"          # windscreen
    if i == 4: return "Glass" if k == "top" else "Paint"          # rear window
    if i == 2: return "Paint"                                      # B pillar
    return "Glass" if k == 1 else "Paint"                          # side windows, roof


body = MeshBuilder()
body.add(loft(body_rings, body_mats, "Paint", "Paint"))
body.add(loft(cab_rings, cab_mats, "Paint", "Paint"))
# Boxy flared arches.
for y in (FY, RY):
    body.add(arch((y, R), 0.78, 0.965, R + 0.05, R + 0.14, "Paint", 4, 176, 3), mirror=True)
    body.add(box((0.80, y + 0.49, 0.31), (0.30, 0.03, 0.24), "Trim"), mirror=True)          # mud flaps
# Front: grille, bumper, a bar of four round spot lamps, small corner headlights.
body.add(box((0, -1.93, 0.56), (0.90, 0.06, 0.20), "Trim", bevel=0.02))
body.add(box((0, -1.95, 0.39), (1.62, 0.12, 0.16), "Trim", bevel=0.04))
body.add(cylinder((-0.66, -1.98, 0.64), (0.66, -1.98, 0.64), 0.03, "Trim", 6))
for x in (0.20, 0.52):
    body.add(cylinder((x, -1.93, 0.64), (x, -2.01, 0.64), 0.085, "Trim", 10), mirror=True)
    body.add(cylinder((x, -1.98, 0.64), (x, -2.03, 0.64), 0.066, "HeadLight", 10), mirror=True)
body.add(box((0.70, -1.90, 0.70), (0.20, 0.04, 0.09), "HeadLight", rot=(0, 0, 0)), mirror=True)
# Roof scoop and the big white roof-edge spoiler.
body.add(box((0, 0.20, 1.395), (0.52, 0.42, 0.06), "Paint", bevel=0.02))
body.add(box((0, -0.01, 1.39), (0.40, 0.03, 0.04), "Trim"))
body.add(box((0, 1.84, 1.385), (1.38, 0.44, 0.05), "Accent", bevel=0.015, rot=(-4, 0, 0)))
body.add(box((0.67, 1.80, 1.36), (0.04, 0.40, 0.09), "Accent"), mirror=True)
# Race roundels (plain white discs) and thin side stripes.
body.add(cylinder((0.915, -0.30, 0.55), (0.935, -0.30, 0.55), 0.19, "Accent", 12), mirror=True)
body.add(box((0.925, 0.05, 0.775), (0.02, 1.62, 0.035), "Accent"), mirror=True)
body.add(box((0.925, 0.05, 0.34), (0.02, 1.62, 0.025), "Accent"), mirror=True)
# Mirrors.
body.add(box((0.86, -0.50, 1.02), (0.16, 0.09, 0.09), "Paint", bevel=0.015), mirror=True)
body.add(box((0.935, -0.50, 1.02), (0.01, 0.07, 0.07), "Trim"), mirror=True)
# Rear: square tail lights, bumper, one round exhaust.
body.add(box((0.62, 2.02, 0.84), (0.24, 0.05, 0.18), "TailLight"), mirror=True)
body.add(box((0, 2.02, 0.42), (1.60, 0.10, 0.18), "Trim", bevel=0.04))
body.add(cylinder((0.48, 1.98, 0.31), (0.48, 2.08, 0.31), 0.055, "Trim", 8))
body_ob = body.build("Body", COLORS, root)

wheels = add_wheels(root, COLORS, FY, RY, TRACK, R,
                    lambda: spoke_wheel(R, 0.30, "Accent", "Trim", lambda s: s % 2 == 0, rim_ratio=0.64))

lod = MeshBuilder()
lod.add(loft(body_rings[::1], body_mats, "Paint", "Paint"))
lod.add(loft([cab_rings[i] for i in (0, 1, 4, 5)], lambda i, k: "Glass" if (k == "top" and i != 1) or k == 1 else "Paint", "Paint", "Paint"))
lod_wheels(lod, FY, RY, TRACK, R, "Accent")
lod.add(box((0, 1.84, 1.385), (1.38, 0.44, 0.05), "Accent"))
lod.add(box((0, -1.97, 0.64), (1.20, 0.08, 0.14), "HeadLight"))
lod.add(box((0, -1.95, 0.39), (1.62, 0.12, 0.16), "Trim"))
lod.add(box((0.62, 2.02, 0.84), (0.24, 0.05, 0.18), "TailLight"), mirror=True)
lod.add(box((0, 2.02, 0.42), (1.60, 0.10, 0.18), "Trim"))
lod.add(box((0.925, -0.30, 0.55), (0.02, 0.34, 0.34), "Accent"), mirror=True)
lod_ob = lod.build("Body_LOD1", COLORS, root)

finish(CAR, root, body_ob, wheels, lod_ob, out_dir_from_argv(CAR), spec_limits(4.10, 1.95, 1.45))
