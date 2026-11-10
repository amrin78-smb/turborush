import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix

bpy.ops.wm.read_factory_settings(use_empty=True)

CAR_ID = "blaze"
PAINT_HEX = "#ED291F"
ACCENT_HEX = "#FFFFFF"
LENGTH = 4.25
WIDTH = 1.95
HEIGHT = 1.25
WHEEL_RADIUS = 0.37
WHEEL_Y = 1.30
TRACK_X = 0.80

OUT_DIR = os.path.expanduser("~/TurboRush3D_Cars/" + CAR_ID + "/")
if "__file__" in globals():
    candidate = os.path.abspath(__file__)
    if os.path.isfile(candidate):
        OUT_DIR = os.path.dirname(candidate)
os.makedirs(OUT_DIR, exist_ok=True)
print("Output folder:", os.path.abspath(OUT_DIR))

scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False

COLORS = {
    "Paint": PAINT_HEX,
    "Accent": ACCENT_HEX,
    "Trim": "#26262E",
    "Glass": "#1F2B47",
    "Tyre": "#1A1A1F",
    "Rim": "#8C93A3",
    "HeadLight": "#FFF7D6",
    "TailLight": "#FF2A1F",
}
ALLOWED = set(COLORS)
MATERIALS = {}


def rgba(hex_color):
    h = hex_color.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)) + (1.0,)


for material_name, color in COLORS.items():
    material = bpy.data.materials.new(material_name)
    material.diffuse_color = rgba(color)
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba(color)
    bsdf.inputs["Roughness"].default_value = 0.5
    bsdf.inputs["Metallic"].default_value = 0.0
    MATERIALS[material_name] = material


class MeshBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.face_materials = []

    def add(self, vertices, faces, material):
        offset = len(self.vertices)
        self.vertices.extend(tuple(v) for v in vertices)
        self.faces.extend(tuple(offset + i for i in face) for face in faces)
        if isinstance(material, str):
            self.face_materials.extend([material] * len(faces))
        else:
            self.face_materials.extend(material)

    def finish(self, name, parent):
        mesh = bpy.data.meshes.new(name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(obj)
        obj.parent = parent
        used = []
        for material_name in self.face_materials:
            if material_name not in used:
                used.append(material_name)
        for material_name in used:
            mesh.materials.append(MATERIALS[material_name])
        slot_lookup = {name: i for i, name in enumerate(used)}
        for polygon, material_name in zip(mesh.polygons, self.face_materials):
            polygon.material_index = slot_lookup[material_name]
            polygon.use_smooth = False
        mesh.calc_loop_triangles()
        return obj


def box(builder, center, size, material, bevel=0.0, rotation=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=list(bm.verts))
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel,
                        segments=1, affect='EDGES', clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    rot = Matrix.Identity(3)
    if rotation is not None:
        from mathutils import Euler
        rot = Euler(rotation).to_matrix()
    c = Vector(center)
    vertices = [c + rot @ v.co for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    builder.add(vertices, faces, material)
    bm.free()


def cylinder(builder, center, radius, depth, axis, material, segments=12):
    c = Vector(center)
    n = Vector(axis).normalized()
    reference = Vector((0.0, 0.0, 1.0))
    if abs(n.dot(reference)) > 0.95:
        reference = Vector((0.0, 1.0, 0.0))
    u = n.cross(reference).normalized()
    v = n.cross(u).normalized()
    vertices = []
    for side in (-1.0, 1.0):
        ring_center = c + n * (side * depth * 0.5)
        for i in range(segments):
            angle = math.tau * i / segments
            vertices.append(ring_center + radius * (u * math.cos(angle) + v * math.sin(angle)))
    faces = []
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((i, j, segments + j, segments + i))
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple(range(segments, 2 * segments)))
    builder.add(vertices, faces, material)


def beam(builder, start, end, thickness, material, bevel=0.0):
    a = Vector(start)
    b = Vector(end)
    delta = b - a
    rotation = delta.to_track_quat('Z', 'Y').to_euler()
    box(builder, (a + b) * 0.5, (thickness, thickness, delta.length),
        material, bevel=bevel, rotation=rotation)


def top_strip(builder, start, end, width, material, thickness=0.018):
    a = Vector(start)
    b = Vector(end)
    dy = b.y - a.y
    dz = b.z - a.z
    length = math.sqrt(dy * dy + dz * dz)
    angle = math.atan2(dz, dy)
    box(builder, (a + b) * 0.5, (width, length, thickness), material,
        rotation=(angle, 0.0, 0.0))


def hull(builder, sections, material="Paint", cabin=False, roof_material="Paint"):
    ring_vertices = []
    for y, w, bottom, top in sections:
        shoulder = min(0.12, (top - bottom) * 0.25)
        ring_vertices.extend([
            (-w * 0.78, y, bottom),
            (w * 0.78, y, bottom),
            (w, y, bottom + shoulder),
            (w, y, top - shoulder),
            (w * 0.78, y, top),
            (-w * 0.78, y, top),
            (-w, y, top - shoulder),
            (-w, y, bottom + shoulder),
        ])
    faces = []
    materials = []
    for s in range(len(sections) - 1):
        for i in range(8):
            j = (i + 1) % 8
            faces.append((s * 8 + i, s * 8 + j, (s + 1) * 8 + j, (s + 1) * 8 + i))
            if cabin:
                materials.append(roof_material if i == 4 else "Glass")
            else:
                materials.append(material)
    faces.append(tuple(reversed(range(8))))
    materials.append("Glass" if cabin else material)
    faces.append(tuple(range((len(sections) - 1) * 8, len(sections) * 8)))
    materials.append("Glass" if cabin else material)
    builder.add(ring_vertices, faces, materials)


def add_wheel_arch(builder, x, y, z, material="Paint", lod=False):
    if lod:
        return
    box(builder, (x, y, z), (0.14, 0.82, 0.12), material, bevel=0.035)


def add_mirrors(builder, y, z, cap_material="Paint", lod=False):
    half = WIDTH * 0.5
    center_x = half - 0.055
    for side in (-1, 1):
        box(builder, (side * (center_x - 0.07), y + 0.03, z - 0.055),
            (0.10, 0.07, 0.06), "Trim")
        box(builder, (side * center_x, y, z), (0.11, 0.20, 0.12),
            cap_material, bevel=0.0 if lod else 0.025)


def make_wheel(name, x, y, side, radius, width, style, parent):
    builder = MeshBuilder()
    cylinder(builder, (0, 0, 0), radius, width, (1, 0, 0), "Tyre", segments=16)
    rim_material = "Accent" if style == "zoom" else "Rim"
    outer = side * (width * 0.5 + 0.005)
    if style == "mint":
        cylinder(builder, (outer, 0, 0), radius * 0.68, 0.025,
                 (1, 0, 0), "Rim", segments=16)
        for angle in (0.0, math.tau / 3.0, 2.0 * math.tau / 3.0):
            cy = math.sin(angle) * radius * 0.34
            cz = math.cos(angle) * radius * 0.34
            box(builder, (outer + side * 0.016, cy, cz),
                (0.018, 0.035, radius * 0.30), "Trim", rotation=(angle, 0, 0))
    else:
        cylinder(builder, (outer, 0, 0), radius * 0.62, 0.024,
                 (1, 0, 0), rim_material, segments=12)
        for i in range(5):
            angle = math.tau * i / 5.0
            cy = math.sin(angle) * radius * 0.28
            cz = math.cos(angle) * radius * 0.28
            box(builder, (outer + side * 0.015, cy, cz),
                (0.022, 0.065, radius * 0.47), rim_material,
                rotation=(angle, 0, 0))
        cylinder(builder, (outer + side * 0.018, 0, 0), radius * 0.22, 0.035,
                 (1, 0, 0), rim_material, segments=10)
    obj = builder.finish(name, parent)
    obj.location = (x, y, radius)
    return obj


def add_lod_wheels(builder, radius, style):
    rim_material = "Accent" if style == "zoom" else "Rim"
    for x, y, width in ((-TRACK_X, -WHEEL_Y, 0.30), (TRACK_X, -WHEEL_Y, 0.30),
                        (-TRACK_X, WHEEL_Y, 0.32), (TRACK_X, WHEEL_Y, 0.32)):
        cylinder(builder, (x, y, radius), radius, width, (1, 0, 0), "Tyre", segments=8)
        side = -1 if x < 0 else 1
        cylinder(builder, (x + side * (width * 0.5 + 0.004), y, radius),
                 radius * 0.60, 0.018, (1, 0, 0), rim_material, segments=8)


def triangle_count(obj):
    if obj.type != 'MESH':
        return 0
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def world_bounds(objects):
    points = []
    for obj in objects:
        if obj.type == 'MESH':
            points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    mins = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    maxs = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return mins, maxs


def self_check(root):
    bpy.context.view_layer.update()
    expected = {"Car_" + CAR_ID, "Body", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR", "Body_LOD1"}
    actual = set(obj.name for obj in scene.objects)
    assert actual == expected, "Object hierarchy mismatch: " + repr(actual)
    body = bpy.data.objects["Body"]
    lod = bpy.data.objects["Body_LOD1"]
    wheels = [bpy.data.objects[name] for name in ("Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR")]
    assert all(obj.parent == root for obj in [body, lod] + wheels), "Incorrect parent"
    counts = {obj.name: triangle_count(obj) for obj in [body, lod] + wheels}
    assert counts["Body"] <= 2800, counts
    assert counts["Body_LOD1"] <= 900, counts
    assert all(counts[obj.name] <= 220 for obj in wheels), counts
    total = counts["Body"] + sum(counts[obj.name] for obj in wheels)
    assert total <= 3700, total
    mins, maxs = world_bounds([body] + wheels)
    size = maxs - mins
    assert abs(size.x - WIDTH) <= 0.05, tuple(size)
    assert abs(size.y - LENGTH) <= 0.05, tuple(size)
    assert abs(size.z - HEIGHT) <= 0.08, tuple(size)
    assert abs(mins.z) <= 0.001, mins.z
    for obj, x, y in zip(wheels, (-TRACK_X, TRACK_X, -TRACK_X, TRACK_X),
                         (-WHEEL_Y, -WHEEL_Y, WHEEL_Y, WHEEL_Y)):
        assert (obj.location - Vector((x, y, WHEEL_RADIUS))).length < 0.0001
        assert obj.rotation_euler.to_matrix() == Matrix.Identity(3)
        assert (obj.scale - Vector((1, 1, 1))).length < 0.0001
        wheel_min, _ = world_bounds([obj])
        assert abs(wheel_min.z) <= 0.001, (obj.name, wheel_min.z)
    material_use = set()
    for obj in [body, lod] + wheels:
        used_slots = set(p.material_index for p in obj.data.polygons)
        assert used_slots == set(range(len(obj.data.materials))), "Unused material slot on " + obj.name
        material_use.update(obj.data.materials[i].name for i in used_slots)
        assert all(p.area > 1e-10 for p in obj.data.polygons), "Zero-area face on " + obj.name
    assert material_use <= ALLOWED
    assert {"Paint", "Accent", "Trim", "Glass", "Tyre", "HeadLight", "TailLight"} <= material_use
    for material_name, sign in (("HeadLight", -1), ("TailLight", 1)):
        ys = []
        for polygon in body.data.polygons:
            if body.data.materials[polygon.material_index].name == material_name:
                ys.extend(body.data.vertices[i].co.y for i in polygon.vertices)
        assert ys and (sum(ys) / len(ys)) * sign > 0, material_name + " on wrong end"
    return counts, total, size


def export_and_preview(root):
    body = bpy.data.objects["Body"]
    lod = bpy.data.objects["Body_LOD1"]
    wheels = [bpy.data.objects[name] for name in ("Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR")]
    counts, total, size = self_check(root)
    lod.hide_render = False
    lod.hide_viewport = False
    for obj in [root, body, lod] + wheels:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    fbx_path = os.path.join(OUT_DIR, "Car_" + CAR_ID + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=True, object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
        add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    for obj in [root, body, lod] + wheels:
        obj.select_set(False)
    lod.hide_render = True
    lod.hide_viewport = True

    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (1, 1, 1)
    world.use_nodes = True
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (1, 1, 1, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (5.6, 6.8, 3.7)
    target = Vector((0, 0, HEIGHT * 0.48))
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 5.7
    camera_data.clip_start = 0.05
    camera_data.clip_end = 100.0
    scene.camera = camera
    sun_data = bpy.data.lights.new("PreviewSun", 'SUN')
    sun_data.energy = 2.0
    sun = bpy.data.objects.new("PreviewSun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(28), math.radians(-22), math.radians(-35))

    blend_path = os.path.join(OUT_DIR, "Car_" + CAR_ID + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    preview_path = os.path.join(OUT_DIR, "Car_" + CAR_ID + "_preview.png")
    scene.render.filepath = preview_path
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.film_transparent = False
    rendered = False
    for engine in ('BLENDER_WORKBENCH', 'BLENDER_EEVEE', 'CYCLES'):
        try:
            scene.render.engine = engine
            if engine == 'BLENDER_WORKBENCH':
                scene.display.shading.light = 'STUDIO'
                scene.display.shading.color_type = 'MATERIAL'
                scene.display.shading.show_shadows = True
                scene.display.shading.show_cavity = True
                scene.display.shading.background_type = 'WORLD'
            elif engine == 'CYCLES':
                scene.cycles.samples = 32
                scene.cycles.use_denoising = True
            bpy.ops.render.render(write_still=True)
            rendered = True
            print("Preview rendered with", engine)
            break
        except Exception as exc:
            print("Preview engine failed:", engine, str(exc))
    if not rendered:
        print("WARNING: preview rendering failed; FBX export completed:", fbx_path)

    for obj in [root, body, lod] + wheels:
        dimensions = tuple(obj.dimensions) if obj.type == 'MESH' else (0.0, 0.0, 0.0)
        print("SUMMARY {} | {} triangles | {:.3f} x {:.3f} x {:.3f} m".format(
            obj.name, counts.get(obj.name, 0), *dimensions))
    print("TOTAL (without Body_LOD1):", total, "triangles")
    print("OVERALL SIZE: {:.3f} x {:.3f} x {:.3f} m".format(*size))
    print("FBX:", fbx_path)
    print("BLEND:", blend_path)
    print("PREVIEW:", preview_path if rendered else "not rendered")


def build_design(builder, lod=False):
    sections = [
        (-2.10, 0.60, 0.42, 0.60),
        (-1.88, 0.82, 0.34, 0.73),
        (-1.30, 0.87, 0.32, 0.83),
        (-0.62, 0.85, 0.32, 0.91),
        (0.48, 0.86, 0.32, 0.90),
        (1.28, 0.88, 0.34, 0.86),
        (1.90, 0.82, 0.38, 0.76),
        (2.10, 0.68, 0.43, 0.66),
    ]
    if lod:
        sections = [sections[i] for i in (0, 2, 4, 5, 7)]
    hull(builder, sections)

    cabin = [
        (-0.88, 0.66, 0.86, 0.91),
        (-0.27, 0.59, 0.86, 1.15),
        (0.46, 0.58, 0.85, 1.16),
        (1.08, 0.67, 0.82, 0.86),
    ]
    hull(builder, cabin, cabin=True, roof_material="Paint")

    for x in (-0.14, 0.14):
        stripe_path = [
            ((-1.91, 0.748), (-0.88, 0.925)),
            ((-0.88, 0.925), (-0.27, 1.168)),
            ((-0.27, 1.168), (0.46, 1.178)),
            ((0.46, 1.178), (1.08, 0.878)),
            ((1.08, 0.878), (1.88, 0.778)),
        ]
        if lod:
            stripe_path = [stripe_path[0], stripe_path[2], stripe_path[4]]
        for (y1, z1), (y2, z2) in stripe_path:
            top_strip(builder, (x, y1, z1), (x, y2, z2), 0.11, "Accent")

    for side in (-1, 1):
        box(builder, (side * 0.865, 0.0, 0.34), (0.07, 2.55, 0.12), "Accent")
        add_wheel_arch(builder, side * 0.82, -WHEEL_Y, 0.77, lod=lod)
        add_wheel_arch(builder, side * 0.82, WHEEL_Y, 0.77, lod=lod)

    box(builder, (0, 1.55, 1.19), (1.72, 0.34, 0.08), "Accent",
        bevel=0.0 if lod else 0.025)
    for x in (-0.52, 0.52):
        box(builder, (x, 1.55, 1.00), (0.07, 0.10, 0.34), "Trim")

    if lod:
        for x in (-0.50, 0.50):
            box(builder, (x, -2.105, 0.58), (0.27, 0.04, 0.24), "HeadLight")
            box(builder, (x, 2.105, 0.72), (0.28, 0.04, 0.25), "TailLight")
    else:
        for x in (-0.50, 0.50):
            cylinder(builder, (x, -2.105, 0.58), 0.16, 0.04,
                     (0, 1, 0), "HeadLight", segments=12)
            cylinder(builder, (x, 2.105, 0.72), 0.17, 0.04,
                     (0, 1, 0), "TailLight", segments=12)

    box(builder, (0, 2.085, 0.48), (1.15, 0.06, 0.23), "Trim",
        bevel=0.0 if lod else 0.025)
    if not lod:
        for x in (-0.32, 0.32):
            cylinder(builder, (x, 2.105, 0.405), 0.085, 0.04,
                     (0, 1, 0), "Trim", segments=10)
        smile = [(-0.34, 0.50), (-0.18, 0.455), (0.0, 0.44), (0.18, 0.455), (0.34, 0.50)]
        for a, b in zip(smile, smile[1:]):
            beam(builder, (a[0], -2.115, a[1]), (b[0], -2.115, b[1]), 0.035, "Trim")

    add_mirrors(builder, -0.58, 0.94, "Paint", lod=lod)


root = bpy.data.objects.new("Car_" + CAR_ID, None)
scene.collection.objects.link(root)
root.empty_display_type = 'PLAIN_AXES'
root.empty_display_size = 0.35
root.location = (0, 0, 0)

body_builder = MeshBuilder()
build_design(body_builder, lod=False)
body = body_builder.finish("Body", root)

wheel_front_width = 0.30
wheel_rear_width = 0.33 if CAR_ID == "sunny" else 0.30
wheel_style = CAR_ID
make_wheel("Wheel_FL", -TRACK_X, -WHEEL_Y, -1, WHEEL_RADIUS, wheel_front_width, wheel_style, root)
make_wheel("Wheel_FR", TRACK_X, -WHEEL_Y, 1, WHEEL_RADIUS, wheel_front_width, wheel_style, root)
make_wheel("Wheel_RL", -TRACK_X, WHEEL_Y, -1, WHEEL_RADIUS, wheel_rear_width, wheel_style, root)
make_wheel("Wheel_RR", TRACK_X, WHEEL_Y, 1, WHEEL_RADIUS, wheel_rear_width, wheel_style, root)

lod_builder = MeshBuilder()
build_design(lod_builder, lod=True)
add_lod_wheels(lod_builder, WHEEL_RADIUS, wheel_style)
lod = lod_builder.finish("Body_LOD1", root)

export_and_preview(root)
