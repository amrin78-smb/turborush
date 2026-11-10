"""Shared low-poly car toolkit for Turbo Rush 3D (Blender 5.2, bpy + bmesh).

Every car script builds parts with these helpers into a Body, four wheels and a LOD1,
then calls finish(), which runs the spec self-checks, writes .blend/.fbx and renders previews.
Conventions: metres, front of the car along -Y, Z up, right side +X, origin on the ground between the axles.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

MATS = ["Paint", "Accent", "Trim", "Glass", "Tyre", "Rim", "HeadLight", "TailLight", "Glow"]
BASE_COLORS = {"Trim": "#26262E", "Glass": "#1F2B47", "Tyre": "#1A1A1F", "Rim": "#8C93A3",
               "HeadLight": "#FFF7D6", "TailLight": "#FF2A1F", "Glow": "#33E6FF"}
REQUIRED = ["Paint", "Accent", "Trim", "Glass", "Tyre", "HeadLight", "TailLight"]


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_rgba(h):
    h = h.lstrip("#")
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1.0,)


# ---------------------------------------------------------------- primitive parts
# Each part is a fresh bmesh whose faces carry material_index = MATS.index(name).

def _finish_part(bm, mat):
    if mat is not None:
        for f in bm.faces:
            f.material_index = MATS.index(mat)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def _bevel(bm, amount, segments=1):
    if amount > 0:
        bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=amount, offset_type='OFFSET',
                        segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)


def box(center, size, mat, bevel=0.0, segments=1, rot=(0, 0, 0)):
    """Axis-aligned box (optionally bevelled), then rotated (degrees, XYZ) about its centre."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    _bevel(bm, bevel, segments)
    m = Matrix.Translation(Vector(center)) @ _euler(rot)
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts[:])
    return _finish_part(bm, mat)


def _euler(rot):
    from mathutils import Euler
    return Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()


def prism(profile_yz, x0, x1, mat, bevel=0.0, segments=1):
    """Extrude a closed side profile [(y, z), ...] across X from x0 to x1."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    n = len(profile_yz)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([a[i], a[j], b[j], b[i]])
    _bevel(bm, bevel, segments)
    return _finish_part(bm, mat)


def lathe(profile, sides, mats, matrix=Matrix(), jitter=None):
    """Revolve [(r, x) or (r, x, twist_radians), ...] around the X axis.
    mats[i] is the material of segment i (profile[i] -> profile[i+1]): a name, or a function side_index -> name.
    jitter(point_index, side_index) -> radius scale (for knobbly treads). r == 0 makes a single pole vertex."""
    bm = bmesh.new()
    rings = []
    for pi, pt in enumerate(profile):
        r, x = pt[0], pt[1]
        tw = pt[2] if len(pt) > 2 else 0.0
        if r == 0:
            rings.append([bm.verts.new((x, 0, 0))])
            continue
        ring = []
        for s in range(sides):
            t = 2 * math.pi * s / sides + tw
            rr = r * (jitter(pi, s) if jitter else 1.0)
            ring.append(bm.verts.new((x, rr * math.cos(t), rr * math.sin(t))))
        rings.append(ring)
    for i in range(len(rings) - 1):
        A, B = rings[i], rings[i + 1]
        for s in range(sides):
            t = (s + 1) % sides
            if len(A) == 1:
                f = bm.faces.new([A[0], B[t], B[s]])
            elif len(B) == 1:
                f = bm.faces.new([A[s], A[t], B[0]])
            else:
                f = bm.faces.new([A[s], A[t], B[t], B[s]])
            m = mats[i](s) if callable(mats[i]) else mats[i]
            f.material_index = MATS.index(m)
    bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts[:])
    return _finish_part(bm, None)


def cylinder(p0, p1, radius, mat, sides=8, cap_mat=None, radius1=None):
    """Closed cylinder from p0 to p1. cap_mat colours the p1 end cap (e.g. a lamp lens)."""
    p0, p1 = Vector(p0), Vector(p1)
    r1 = radius if radius1 is None else radius1
    L = (p1 - p0).length
    prof = [(0, 0), (radius, 0), (r1, L), (0, L)]
    mats = [mat, mat, cap_mat or mat]
    m = Matrix.Translation(p0) @ _align_x((p1 - p0).normalized())
    return lathe(prof, sides, mats, m)


def _align_x(d):
    """Rotation taking +X to direction d."""
    q = Vector((1, 0, 0)).rotation_difference(d)
    return q.to_matrix().to_4x4()


def sphere(center, radius, mat, u=8, v=5):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=radius)
    bmesh.ops.translate(bm, vec=Vector(center), verts=bm.verts[:])
    return _finish_part(bm, mat)


def arch(center_yz, x0, x1, r_in, r_out, mat, a0=15, a1=165, steps=8):
    """Wheel-arch flare: an annulus sector around the X axis (angles from +Y... measured from -Y front? see below).
    Angle 0 points to -Y (front), 90 points up, 180 points to +Y (rear)."""
    cy, cz = center_yz
    bm = bmesh.new()
    sl = []
    for i in range(steps + 1):
        t = math.radians(a0 + (a1 - a0) * i / steps)
        dy, dz = -math.cos(t), math.sin(t)
        sl.append([bm.verts.new((x, cy + r * dy, cz + r * dz))
                   for (r, x) in ((r_in, x0), (r_out, x0), (r_out, x1), (r_in, x1))])
    for i in range(steps):
        A, B = sl[i], sl[i + 1]
        for k in range(4):
            j = (k + 1) % 4
            bm.faces.new([A[k], A[j], B[j], B[k]])
    bm.faces.new(sl[0])
    bm.faces.new(list(reversed(sl[-1])))
    return _finish_part(bm, mat)


def mirror_x(bm):
    """Return a copy mirrored across X = 0 (normals fixed)."""
    out = bm.copy()
    for v in out.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(out, faces=out.faces[:])
    return _finish_part(out, None)


# ---------------------------------------------------------------- mesh assembly

class MeshBuilder:
    def __init__(self):
        self.bm = bmesh.new()

    def add(self, *parts, mirror=False):
        for p in parts:
            self._merge(p)
            if mirror:
                self._merge(mirror_x(p))
        return self

    def _merge(self, src):
        vmap = {v: self.bm.verts.new(v.co) for v in src.verts}
        for f in src.faces:
            nf = self.bm.faces.new([vmap[v] for v in f.verts])
            nf.material_index = f.material_index

    def build(self, name, colors, parent=None, location=(0, 0, 0)):
        used = sorted({f.material_index for f in self.bm.faces})
        remap = {old: new for new, old in enumerate(used)}
        for f in self.bm.faces:
            f.material_index = remap[f.material_index]
            f.smooth = False
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for old in used:
            me.materials.append(material(MATS[old], colors))
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = location
        if parent:
            ob.parent = parent
        return ob


_mat_cache = {}


def material(name, colors):
    if name in _mat_cache:
        return _mat_cache[name]
    m = bpy.data.materials.new(name)
    rgba = hex_rgba(colors.get(name) or BASE_COLORS[name])
    m.diffuse_color = rgba
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Roughness"].default_value = 0.5
        bsdf.inputs["Metallic"].default_value = 0.0
    _mat_cache[name] = m
    return m


def standard_wheel(radius=0.40, width=0.30, sides=12, rim_mat="Rim", hub_mat="Rim", knobbly=0.0, matrix=Matrix()):
    """Right-hand wheel at the origin, spin axis X, rim face toward +X. ~192 triangles at 12 sides."""
    h = width / 2
    prof = [(0, -h), (radius * 0.9, -h), (radius, -h * 0.66), (radius, h * 0.66), (radius * 0.9, h),
            (radius * 0.62, h), (radius * 0.55, h * 0.8), (radius * 0.22, h * 0.8), (radius * 0.15, h + 0.01), (0, h + 0.01)]
    mats = ["Tyre", "Tyre", "Tyre", "Tyre", "Tyre", rim_mat, rim_mat, hub_mat, hub_mat]
    jit = None
    if knobbly > 0:
        # odd side indices stay at full radius (index sides*3/4 is the lowest point, which must touch the ground)
        jit = lambda pi, s: (1.0 if s % 2 == 1 or pi not in (2, 3) else 1.0 - knobbly)
    return lathe(prof, sides, mats, matrix, jit)


# ---------------------------------------------------------------- output

def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def world_bbox(obs):
    pts = [ob.matrix_world @ v.co for ob in obs for v in ob.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def face_mean(ob, matname):
    idx = [i for i, m in enumerate(ob.data.materials) if m.name.split(".")[0] == matname]
    cs = [ob.matrix_world @ p.center for p in ob.data.polygons if p.material_index in idx]
    return sum(cs, Vector()) / len(cs) if cs else None


def finish(car_id, root, body, wheels, lod, out_dir, limits=None):
    """limits: dict(length=(lo, hi), width=..., height=...) from the car's spec."""
    lim = dict(length=(4.05, 4.15), width=(1.95, 2.05), height=(1.52, 1.68))
    if limits:
        lim.update(limits)
    bpy.context.view_layer.update()
    errors = []
    names = [f"Car_{car_id}", "Body", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR", "Body_LOD1"]
    for n in names:
        if n not in bpy.data.objects:
            errors.append("missing object " + n)
    for ob in [body, lod] + list(wheels.values()):
        if ob.parent != root:
            errors.append(ob.name + " is not parented to the root")
    tb, tl = tri_count(body), tri_count(lod)
    tw = {k: tri_count(w) for k, w in wheels.items()}
    if tb > 2800: errors.append(f"Body has {tb} triangles (> 2800)")
    if tl > 900: errors.append(f"Body_LOD1 has {tl} triangles (> 900)")
    for k, t in tw.items():
        if t > 220: errors.append(f"{k} has {t} triangles (> 220)")
    total = tb + sum(tw.values())
    if total > 3700: errors.append(f"total {total} triangles (> 3700)")
    lo, hi = world_bbox([body] + list(wheels.values()))
    size = hi - lo
    for axis, key in ((1, "length"), (0, "width"), (2, "height")):
        a, b = lim[key]
        if not a <= size[axis] <= b:
            errors.append(f"{key} {size[axis]:.3f} m outside {a}-{b}")
    for k, w in wheels.items():
        zs = [(w.matrix_world @ v.co).z for v in w.data.vertices]
        if abs(min(zs)) > 0.001: errors.append(f"{k} lowest point {min(zs):.4f} != 0")
        ys = [v.co.y for v in w.data.vertices]; zl = [v.co.z for v in w.data.vertices]
        if abs((max(ys) + min(ys)) / 2) > 0.005 or abs((max(zl) + min(zl)) / 2) > 0.02:
            errors.append(k + " origin is not at the wheel centre")
        if tuple(w.rotation_euler) != (0, 0, 0): errors.append(k + " is rotated")
    hl, tlm = face_mean(body, "HeadLight"), face_mean(body, "TailLight")
    if hl is None or hl.y >= 0: errors.append("headlights are not at the front (-Y)")
    if tlm is None or tlm.y <= 0: errors.append("tail lights are not at the rear (+Y)")
    used = {m.name for ob in [body, lod] + list(wheels.values()) for m in ob.data.materials}
    for m in used:
        if m not in MATS: errors.append("bad material " + m)
    for m in REQUIRED:
        if m not in used: errors.append("material not used: " + m)
    for ob in [body, lod] + list(wheels.values()):
        print(f"  {ob.name:10s} {tri_count(ob):5d} tris  size {tuple(round(s, 3) for s in ob.dimensions)}")
    print(f"  total (Body + wheels) {total} tris; car size L{size.y:.3f} W{size.x:.3f} H{size.z:.3f} m")
    if errors:
        raise RuntimeError("self-check failed:\n  " + "\n  ".join(errors))

    os.makedirs(out_dir, exist_ok=True)
    fbx = os.path.join(out_dir, f"Car_{car_id}.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    for ob in [root, body, lod] + list(wheels.values()):
        ob.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
        add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    try:
        render_previews(car_id, out_dir, lod)
    except Exception as e:  # a failed preview must not stop the export
        print("preview failed:", e)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_dir, f"Car_{car_id}.blend"))
    print("output folder:", os.path.abspath(out_dir))


VIEWS = {
    "preview": ((3.2, 7.5, 3.4), 50),      # 3/4 rear, chase-camera height
    "chase": ((0.0, 8.5, 3.4), 50),        # exactly behind, like the game camera
    "front": ((-4.2, -6.0, 2.0), 50),
    "side": ((8.5, 0.0, 1.0), 50),
    "top": ((0.0, 0.01, 10.0), 50),
}


def render_previews(car_id, out_dir, lod):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'MATERIAL'
    sc.display.shading.show_cavity = True
    sc.display.shading.show_object_outline = True
    sc.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new("White")
    world.color = (1, 1, 1)
    sc.world = world
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    target = Vector((0, 0, 0.7))
    lod.hide_render = True
    views = VIEWS if os.environ.get("CAR_ALL_VIEWS") else {"preview": VIEWS["preview"]}
    for name, (loc, lens) in views.items():
        cam.location = loc
        d = target - Vector(loc)
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = lens
        cam_data.lens = 35 if name == "top" else lens
        sc.render.filepath = os.path.join(out_dir, f"Car_{car_id}_{name}.png")
        bpy.ops.render.render(write_still=True)
    # one shot of the LOD alone
    if os.environ.get("CAR_ALL_VIEWS"):
        lod.hide_render = False
        for ob in sc.objects:
            if ob.type == 'MESH' and ob != lod:
                ob.hide_render = True
        cam.location = VIEWS["preview"][0]
        cam.rotation_euler = (target - Vector(VIEWS["preview"][0])).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out_dir, f"Car_{car_id}_lod1.png")
        bpy.ops.render.render(write_still=True)
        for ob in sc.objects:
            ob.hide_render = False
        lod.hide_render = True


def out_dir_from_argv(car_id):
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in argv:
        return argv[argv.index("--out") + 1]
    return os.path.join(os.path.expanduser("~/TurboRush3D_Cars"), car_id)


def new_scene(car_id):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mat_cache.clear()
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    root = bpy.data.objects.new(f"Car_{car_id}", None)
    sc.collection.objects.link(root)
    return root


def add_wheels(root, colors, front_y, rear_y, track, radius, make_wheel):
    """make_wheel() returns a right-hand wheel bmesh at the origin; left wheels are mirrored."""
    wheels = {}
    for key, y in (("F", front_y), ("R", rear_y)):
        for side, sx in (("L", -1), ("R", 1)):
            bm = make_wheel()
            if sx < 0:
                bm2 = mirror_x(bm); bm.free(); bm = bm2
            mb = MeshBuilder(); mb.add(bm)
            name = f"Wheel_{key}{side}"
            wheels[name] = mb.build(name, colors, root, (sx * track / 2, y, radius))
    return wheels


# ---------------------------------------------------------------- lofted bodies

def mirrored_ring(y, right):
    """Closed ring at depth y from right-side (x, z) points listed bottom -> top; the left side is mirrored.
    Edges: k = 0..n-2 right side, n-1 the top bridge, n..2n-2 the left side, 2n-1 the bottom bridge."""
    pts = [(x, y, z) for x, z in right] + [(-x, y, z) for x, z in reversed(right)]
    return pts


def edge_kind(j, n):
    """Map a ring edge index to the side-profile edge k (same for both sides), 'top' or 'bottom'."""
    if j < n - 1: return j
    if j == n - 1: return "top"
    if j < 2 * n - 1: return 2 * n - 2 - j
    return "bottom"


def loft(rings, matfn, cap0="Trim", cap1="Trim"):
    """Skin consecutive rings (lists of (x, y, z), same length, made by mirrored_ring).
    matfn(segment_index, edge_kind) -> material name."""
    bm = bmesh.new()
    vs = [[bm.verts.new(p) for p in ring] for ring in rings]
    m = len(rings[0]); n = m // 2
    for i in range(len(vs) - 1):
        A, B = vs[i], vs[i + 1]
        for j in range(m):
            k = (j + 1) % m
            f = bm.faces.new([A[j], A[k], B[k], B[j]])
            f.material_index = MATS.index(matfn(i, edge_kind(j, n)))
    f0 = bm.faces.new(vs[0]); f0.material_index = MATS.index(cap0)
    f1 = bm.faces.new(list(reversed(vs[-1]))); f1.material_index = MATS.index(cap1)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
    return _finish_part(bm, None)


def body_ring(y, W, zb, zbelt, zdeck, wdeck, crown=0.04, wheel=False, inset=0.60, sh=0.07):
    """Lower body cross-section (right side, bottom -> top): a=bottom, b=lower side, c=belt, s=shoulder, d=deck edge, e=crown.
    In a wheel zone the lower side steps in to `inset` (the wheel well) and the belt becomes the arch shelf."""
    if wheel:
        a, b = (inset - 0.04, zb + 0.10), (inset, zbelt - 0.03)
    else:
        a, b = (W - 0.07, zb), (W, zb + 0.08)
    right = [a, b, (W, zbelt), (W - sh, zbelt + sh * 0.8), (wdeck, zdeck), (wdeck * 0.45, zdeck + crown)]
    return mirrored_ring(y, right)


def body_mats(i, k):
    return "Trim" if k == "bottom" else "Paint"


def spoke_wheel(radius, width, spoke_mat="Rim", gap_mat="Trim", spokes=lambda s: s % 2 == 0, twist=0.0,
                rim_ratio=0.62, hub_mat=None, sides=12, matrix=Matrix()):
    """Right-hand wheel (spin axis X, face toward +X) with painted spokes in a recessed rim. 168 tris at 12 sides.
    The tread has a vertex at the very bottom (side index sides*3/4), so the tyre touches Z = -radius exactly."""
    h = width / 2
    rr = radius * rim_ratio
    prof = [(0, -h), (radius, -0.7 * h), (radius, 0.7 * h), (radius * 0.94, h), (rr, h),
            (rr * 0.9, 0.5 * h), (radius * 0.2, 0.5 * h, twist), (radius * 0.13, h + 0.01, twist), (0, h + 0.01)]
    mats = ["Tyre", "Tyre", "Tyre", "Tyre", spoke_mat,
            lambda s: spoke_mat if spokes(s) else gap_mat, spoke_mat, hub_mat or spoke_mat]
    return lathe(prof, sides, mats, matrix)


def spec_limits(length, width, height, tol=0.05, htol=0.08):
    return dict(length=(length - tol, length + tol), width=(width - tol, width + tol), height=(height - htol, height + htol))


def lod_wheels(mb, front_y, rear_y, track, radius, rim_mat="Rim", width=0.30):
    """Blocky 8-sided wheels baked into a LOD mesh."""
    for y in (front_y, rear_y):
        x0 = track / 2 - width / 2
        mb.add(cylinder((x0, y, radius), (x0 + width, y, radius), radius, "Tyre", 8, cap_mat=rim_mat), mirror=True)
