"""Import a car FBX and check it against the Turbo Rush 3D car spec (no scripts from the asset are run).
blender -b --factory-startup -P Tools/Blender/check_car_fbx.py -- path/Car_x.fbx [...]
FBX axes are converted back to Blender's (front = -Y) by the importer."""
import bpy, sys, os
from mathutils import Vector

ALLOWED = {"Paint", "Accent", "Trim", "Glass", "Tyre", "Rim", "HeadLight", "TailLight", "Glow"}
REQUIRED = {"Paint", "Accent", "Trim", "Glass", "Tyre", "HeadLight", "TailLight"}
fail_any = False
HERE = os.path.dirname(os.path.abspath(__file__))
PROMPTS = os.path.join(HERE, "..", "..", "Docs", "ArtBriefs", "KimiCarPrompts")


def limits(car):
    """Per-car size limits, read from the car's prompt (the single source of the spec)."""
    import re, glob
    md = open(glob.glob(os.path.join(PROMPTS, f"*_{car}_*.md"))[0]).read()
    m = re.search(r"Overall length ([\d.]+) m \(±([\d.]+)\), overall width ([\d.]+) m \(±([\d.]+).*?overall height ([\d.]+) m \(±([\d.]+)\)", md)
    L, dl, W, dw, H, dh = map(float, m.groups())
    r = float(re.search(r"Wheel radius ([\d.]+) m", md).group(1))
    return dict(length=(L - dl, L + dl), width=(W - dw, W + dw), height=(H - dh, H + dh), radius=r)

for path in sys.argv[sys.argv.index("--") + 1:]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward='-Z', axis_up='Y', bake_space_transform=True)
    car = os.path.basename(path)[4:-4]
    issues, info = [], []
    obs = {o.name: o for o in bpy.data.objects}
    names = [f"Car_{car}", "Body", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR", "Body_LOD1"]
    for n in names:
        if n not in obs: issues.append("missing " + n)
    if issues:
        print(f"== {car}: FAIL", issues, "objects:", list(obs)); fail_any = True; continue
    root = obs[f"Car_{car}"]
    def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)
    def wpts(o): return [o.matrix_world @ v.co for v in o.data.vertices]
    for n in names[1:]:
        if obs[n].parent != root: issues.append(n + " not child of root")
    tb = tris(obs["Body"]); tl = tris(obs["Body_LOD1"]); tw = {n: tris(obs[n]) for n in names[2:6]}
    total = tb + sum(tw.values())
    info.append(f"tris Body {tb}, wheels {list(tw.values())}, LOD1 {tl}, total {total}")
    if tb > 2800: issues.append(f"Body {tb} tris")
    if tl > 900: issues.append(f"LOD1 {tl} tris")
    if max(tw.values()) > 220: issues.append(f"wheel tris {tw}")
    if total > 3700: issues.append(f"total {total}")
    pts = [p for n in names[1:6] for p in wpts(obs[n])]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    sz = hi - lo
    info.append(f"size L{sz.y:.3f} W{sz.x:.3f} H{sz.z:.3f}  x[{lo.x:.2f},{hi.x:.2f}] y[{lo.y:.2f},{hi.y:.2f}] z[{lo.z:.3f},{hi.z:.2f}]")
    lim = limits(car)
    eps = 0.002
    for axis, key in ((1, "length"), (0, "width"), (2, "height")):
        a, b = lim[key]
        if not a - eps <= sz[axis] <= b + eps: issues.append(f"{key} {sz[axis]:.3f} outside {a:.2f}-{b:.2f}")
    for n in names[2:6]:
        w = obs[n]; wp = wpts(w); c = w.matrix_world.translation
        zmin = min(p.z for p in wp)
        mid = Vector([(max(p[i] for p in wp) + min(p[i] for p in wp)) / 2 for i in range(3)])
        r = (max(p.z for p in wp) - zmin) / 2
        info.append(f"{n} centre ({c.x:+.3f},{c.y:+.3f},{c.z:.3f}) r {r:.3f} bottom {zmin:+.4f} rot {tuple(round(a,3) for a in w.matrix_world.to_euler())}")
        if abs(zmin) > 0.002: issues.append(f"{n} bottom at {zmin:.4f}")
        if abs(mid.y - c.y) > 0.01 or abs(mid.z - c.z) > 0.01: issues.append(f"{n} origin is not on the spin axis")
        if abs(r - lim["radius"]) > 0.01: issues.append(f"{n} radius {r:.3f} != {lim['radius']}")
        e = w.matrix_world.to_euler()
        if max(abs(a) for a in e) > 1e-3: issues.append(f"{n} rotated {tuple(e)}")
    fl, fr, rl, rr = (obs[n].matrix_world.translation for n in names[2:6])
    if not (fl.x < 0 < fr.x and fl.y < rl.y): issues.append("wheel naming/sides wrong (L must be -X, F must be -Y)")
    body = obs["Body"]
    def mean(mat):
        idx = [i for i, m in enumerate(body.data.materials) if m and m.name.split(".")[0] == mat]
        cs = [body.matrix_world @ p.center for p in body.data.polygons if p.material_index in idx]
        return sum(cs, Vector()) / len(cs) if cs else None
    h, t = mean("HeadLight"), mean("TailLight")
    if h is None or h.y >= 0: issues.append("headlights not at -Y")
    if t is None or t.y <= 0: issues.append("taillights not at +Y")
    used = {m.name.split(".")[0] for n in names[1:] for m in obs[n].data.materials if m}
    if used - ALLOWED: issues.append(f"extra materials {used - ALLOWED}")
    if REQUIRED - used: issues.append(f"unused required {REQUIRED - used}")
    info.append("materials " + ", ".join(sorted(used)))
    print(f"== {car}: {'PASS' if not issues else 'FAIL'}")
    for i in info: print("   ", i)
    for i in issues: print("   !!", i)
    fail_any |= bool(issues)
sys.exit(1 if fail_any else 0)
