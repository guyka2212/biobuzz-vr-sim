"""
Shared helpers for VrFsim's Blender model scripts.

Run inside Blender (via the MCP bridge or Blender's Text Editor):
    exec(open(r"<repo>/Tools/blender/vrfsim_lib.py").read())

Conventions
- Blender world = FIRST field frame: X toward the audience's right, Y away from the audience,
  Z up. 1 Blender unit = 1 metre. Helpers take inches (IN) because the manual does.
- Every model uses one material per Unity material it maps to; the material NAME must match a
  material in Assets/VrFsim/Materials (the Unity import remaps by name).
- Low poly: bevels only where they read at VR distance, cylinders 8-16 sides.
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

IN = 0.0254
REPO = r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim"
MODELS = os.path.join(REPO, "Assets", "VrFsim", "Art", "Models")
TEXTURES = os.path.join(REPO, "Assets", "VrFsim", "Art", "Textures")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(TEXTURES, exist_ok=True)

# Viewport colours that roughly match the Unity materials, so screenshots read correctly.
PALETTE = {
    "Tile": (0.32, 0.33, 0.35), "WallPanel": (0.85, 0.9, 0.95), "WallFrame": (0.62, 0.64, 0.67),
    "TapeRed": (0.85, 0.08, 0.08), "TapeBlue": (0.05, 0.35, 0.95), "HiveFrame": (0.18, 0.18, 0.2),
    "HiveRed": (0.8, 0.1, 0.12), "HiveBlue": (0.1, 0.25, 0.85), "FlowerPipe": (0.93, 0.93, 0.9),
    "FlowerRing": (0.2, 0.62, 0.24), "VenueFloor": (0.16, 0.17, 0.2), "VenueWall": (0.1, 0.11, 0.14),
    "Pollen": (1.0, 0.85, 0.05), "NectarRed": (0.9, 0.08, 0.1), "NectarBlue": (0.08, 0.3, 0.95),
    "RobotMetal": (0.7, 0.72, 0.75), "AccentYellow": (0.98, 0.76, 0.1), "RobotDark": (0.12, 0.12, 0.13), "RobotWheel": (0.08, 0.08, 0.08),
}


def clear_scene():
    """Delete every object and orphan mesh (does NOT reset Blender, which would stop the MCP server)."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        col = PALETTE.get(name, (0.6, 0.6, 0.6))
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = (*col, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.6
        m.diffuse_color = (*col, 1.0)
    return m


def new_obj(name, bm, material, parent=None, smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if material:
        ob.data.materials.append(mat(material) if isinstance(material, str) else material)
    if parent:
        ob.parent = parent
    return ob


def box(bm, center_in, size_in, rot=None):
    """Add a box to a bmesh. center/size in inches (x, y, z), optional Euler (deg)."""
    m = Matrix.Translation(Vector(center_in) * IN)
    if rot:
        m = m @ Matrix.Rotation(math.radians(rot[2]), 4, "Z") @ Matrix.Rotation(math.radians(rot[1]), 4, "Y") @ Matrix.Rotation(math.radians(rot[0]), 4, "X")
    s = Matrix.Diagonal((*(Vector(size_in) * IN), 1.0))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m @ s)


def cyl(bm, center_in, radius_in, depth_in, segments=12, axis="Z", cap=True):
    rot = {"Z": Matrix.Identity(4), "X": Matrix.Rotation(math.radians(90), 4, "Y"), "Y": Matrix.Rotation(math.radians(90), 4, "X")}[axis]
    m = Matrix.Translation(Vector(center_in) * IN) @ rot
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments,
                          radius1=radius_in * IN, radius2=radius_in * IN, depth=depth_in * IN, matrix=m)


def ring_plate(bm, center_in, outer_x_in, outer_y_in, hole_r_in, thick_in, segments=16):
    """Rectangular plate with a round hole (z-axis), built as a proper ring so the hole reads round."""
    cx, cy, cz = center_in
    hx, hy = outer_x_in / 2, outer_y_in / 2
    top, bot = (cz + thick_in / 2) * IN, (cz - thick_in / 2) * IN
    inner_t, inner_b, outer_t, outer_b = [], [], [], []
    for i in range(segments):
        a = 2 * math.pi * i / segments + math.pi / segments
        dx, dy = math.cos(a), math.sin(a)
        r = hole_r_in
        inner_t.append(bm.verts.new(((cx + dx * r) * IN, (cy + dy * r) * IN, top)))
        inner_b.append(bm.verts.new(((cx + dx * r) * IN, (cy + dy * r) * IN, bot)))
        # Project the direction onto the rectangle boundary.
        k = min(hx / abs(dx) if abs(dx) > 1e-6 else 1e9, hy / abs(dy) if abs(dy) > 1e-6 else 1e9)
        outer_t.append(bm.verts.new(((cx + dx * k) * IN, (cy + dy * k) * IN, top)))
        outer_b.append(bm.verts.new(((cx + dx * k) * IN, (cy + dy * k) * IN, bot)))
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((outer_t[i], outer_t[j], inner_t[j], inner_t[i]))
        bm.faces.new((outer_b[i], inner_b[i], inner_b[j], outer_b[j]))
        bm.faces.new((inner_t[i], inner_t[j], inner_b[j], inner_b[i]))
        bm.faces.new((outer_t[i], outer_b[i], outer_b[j], outer_t[j]))


def finish(ob, bevel_in=0.0, segments=1, weld=True):
    """Clean normals and optionally bevel edges (applied, so the FBX stays plain)."""
    bpy.context.view_layer.objects.active = ob
    if weld:
        bm = bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(ob.data); bm.free()
    if bevel_in > 0:
        mod = ob.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel_in * IN
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(40)
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.update()
    return ob


def tri_count(objs=None):
    objs = objs or [o for o in bpy.context.scene.objects if o.type == "MESH"]
    n = 0
    for o in objs:
        n += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return n


def export_fbx(filename, objects=None):
    """Export selected objects (or all meshes) to Assets/VrFsim/Art/Models/<filename>.fbx, Unity axes."""
    bpy.ops.object.select_all(action="DESELECT")
    objs = objects or [o for o in bpy.context.scene.objects if o.type in ("MESH", "EMPTY")]
    for o in objs:
        o.select_set(True)
    path = os.path.join(MODELS, filename + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, use_tspace=False,
        embed_textures=False, path_mode="STRIP")
    return path, os.path.getsize(path)
