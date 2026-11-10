"""Grape (car 5): a tall, toy-like off-road buggy with a pink roll cage and a rear spare wheel.
Run: blender -b --factory-startup --python-exit-code 1 -P Tools/Blender/car_grape.py -- --out Art/Cars/grape
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib, carkit
importlib.reload(carkit)
from carkit import *
from mathutils import Matrix
import math

CAR = "grape"
COLORS = {"Paint": "#8F4CEB", "Accent": "#FF99E6"}
R, TRACK, FY, RY = 0.40, 1.60, -1.325, 1.325   # wheel radius, track, front/rear axle Y

root = new_scene(CAR)

# ------------------------------------------------------------------ Body
body = MeshBuilder()

# Tub: one side profile (y, z) extruded across the car. Hood, open cab dip, rear deck.
tub = [(-1.60, 0.47), (-1.86, 0.60), (-1.90, 0.86), (-1.72, 1.04), (-0.72, 1.11), (-0.58, 0.98),
       (0.62, 0.98), (0.76, 1.11), (1.56, 1.11), (1.70, 0.92), (1.70, 0.58), (1.50, 0.47)]
body.add(prism(tub, -0.58, 0.58, "Paint", bevel=0.07, segments=2))

# Round wheel arches (Paint flare with a pink Accent lip), all four corners.
for y in (FY, RY):
    body.add(arch((y, R), 0.52, 0.92, 0.47, 0.56, "Paint", 12, 168, 8), mirror=True)
    body.add(arch((y, R), 0.92, 0.99, 0.46, 0.58, "Accent", 12, 168, 8), mirror=True)

# Side nerf bars between the arches.
body.add(cylinder((0.76, -0.80, 0.56), (0.76, 0.80, 0.56), 0.05, "Trim", 6), mirror=True)
body.add(cylinder((0.60, -0.62, 0.56), (0.76, -0.80, 0.56), 0.045, "Trim", 6), mirror=True)
body.add(cylinder((0.60, 0.62, 0.56), (0.76, 0.80, 0.56), 0.045, "Trim", 6), mirror=True)

# Front: chunky bumper, friendly round headlight "eyes", nose grille, two spot lamps on the bumper.
body.add(box((0, -1.91, 0.52), (1.44, 0.22, 0.20), "Trim", bevel=0.05))
body.add(cylinder((0.36, -1.78, 0.77), (0.36, -1.92, 0.77), 0.145, "Trim", 12), mirror=True)
body.add(cylinder((0.36, -1.84, 0.77), (0.36, -1.95, 0.77), 0.11, "HeadLight", 12), mirror=True)
body.add(box((0, -1.88, 0.71), (0.32, 0.09, 0.15), "Trim", bevel=0.03))
body.add(cylinder((0.60, -1.86, 0.70), (0.60, -1.97, 0.70), 0.075, "Trim", 8), mirror=True)
body.add(cylinder((0.60, -1.94, 0.70), (0.60, -2.00, 0.70), 0.055, "HeadLight", 8), mirror=True)
body.add(box((0, -1.55, 0.45), (0.90, 0.40, 0.06), "Trim"))  # skid plate

# Cab: dashboard, windscreen, two simple seats.
body.add(box((0, -0.53, 1.04), (1.06, 0.16, 0.14), "Trim", bevel=0.03))
body.add(box((0, -0.54, 1.26), (1.00, 0.04, 0.38), "Glass", rot=(-22, 0, 0)))
body.add(box((0.26, 0.12, 1.03), (0.40, 0.44, 0.10), "Trim", bevel=0.03), mirror=True)
body.add(box((0.26, 0.40, 1.24), (0.40, 0.11, 0.46), "Trim", bevel=0.04, rot=(-10, 0, 0)), mirror=True)

# Pink roll cage: front hoop, rear hoop, roof rails and rear stays, with ball joints at the corners.
TUBE = 0.045
FT, RT, TOP = -0.40, 0.52, 1.54
cage = [((0.53, -0.62, 1.04), (0.50, FT, TOP)),
        ((0.53, 0.72, 1.06), (0.50, RT, TOP)),
        ((0.50, FT, TOP), (0.50, RT, TOP)),
        ((0.50, RT, TOP), (0.42, 1.40, 1.10))]
for a, b in cage:
    body.add(cylinder(a, b, TUBE, "Accent", 8), mirror=True)
body.add(cylinder((-0.50, FT, TOP), (0.50, FT, TOP), TUBE, "Accent", 8))
body.add(cylinder((-0.50, RT, TOP), (0.50, RT, TOP), TUBE, "Accent", 8))
for y in (FT, RT):
    body.add(sphere((0.50, y, TOP), 0.06, "Accent"), mirror=True)
# Soft-top roof panel inside the cage (a big Paint block for the chase camera).
body.add(box((0, (FT + RT) / 2, 1.525), (0.96, RT - FT, 0.05), "Paint", bevel=0.015))

# Rear: round tail lights, bumper, spare wheel with a pink hub on a bracket.
body.add(cylinder((0.50, 1.62, 0.78), (0.50, 1.74, 0.78), 0.14, "Trim", 10), mirror=True)
body.add(cylinder((0.50, 1.68, 0.78), (0.50, 1.77, 0.78), 0.11, "TailLight", 10), mirror=True)
body.add(box((0, 1.70, 0.52), (1.40, 0.24, 0.18), "Trim", bevel=0.05))
body.add(box((0, 1.76, 1.04), (0.26, 0.16, 0.22), "Trim", bevel=0.02))
SPARE_Y, SPARE_Z = 1.95, 1.06
spare_m = Matrix.Translation((0, SPARE_Y, SPARE_Z)) @ Matrix.Rotation(math.radians(90), 4, 'Z')
body.add(standard_wheel(0.34, 0.24, 12, "Rim", "Accent", knobbly=0.04, matrix=spare_m))

body_ob = body.build("Body", COLORS, root)

# ------------------------------------------------------------------ Wheels
wheels = add_wheels(root, COLORS, FY, RY, TRACK, R,
                    lambda: standard_wheel(R, 0.30, 12, "Rim", "Rim", knobbly=0.04))

# ------------------------------------------------------------------ LOD1 (blocky, same silhouette, wheels included)
lod = MeshBuilder()
lod.add(prism(tub, -0.58, 0.58, "Paint"))
for y in (FY, RY):
    lod.add(arch((y, R), 0.52, 0.99, 0.47, 0.57, "Paint", 12, 168, 3), mirror=True)
    lod.add(cylinder((0.65, y, R), (0.95, y, R), R, "Tyre", 8, cap_mat="Rim"), mirror=True)
lod.add(box((0, -1.91, 0.52), (1.44, 0.22, 0.20), "Trim"))
lod.add(box((0.36, -1.86, 0.77), (0.24, 0.12, 0.22), "HeadLight"), mirror=True)
lod.add(box((0, 1.70, 0.52), (1.40, 0.24, 0.18), "Trim"))
lod.add(box((0.50, 1.70, 0.78), (0.22, 0.12, 0.20), "TailLight"), mirror=True)
lod.add(box((0, -0.54, 1.26), (1.00, 0.04, 0.38), "Glass", rot=(-22, 0, 0)))
for a, b in cage:
    lod.add(cylinder(a, b, 0.05, "Accent", 4), mirror=True)
lod.add(box((0, (FT + RT) / 2, 1.53), (1.06, RT - FT + 0.08, 0.08), "Accent"))
lod.add(box((0, (FT + RT) / 2, 1.535), (0.92, RT - FT - 0.04, 0.08), "Paint"))
lod.add(cylinder((0, 1.80, SPARE_Z), (0, 2.07, SPARE_Z), 0.34, "Tyre", 8, cap_mat="Rim"))
lod.add(box((0, 2.08, SPARE_Z), (0.12, 0.04, 0.12), "Accent"))
lod_ob = lod.build("Body_LOD1", COLORS, root)

finish(CAR, root, body_ob, wheels, lod_ob, out_dir_from_argv(CAR))
