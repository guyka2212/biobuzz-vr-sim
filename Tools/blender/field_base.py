"""Perimeter walls and tile floor.

Perimeter: inner faces at ±70.674 in (field CAD), ~11 in tall, aluminium top rail, kick rail
and posts at the corners and at the panel joints (±23.39 in, where the FLOWERS mount), with
light polycarbonate panels between them.
Tiles: one quad with a generated 6x6 foam-tile texture (interlocking tab seams).
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())
import numpy as np

clear_scene()
W_IN, T, H = 70.674, 1.0, 11.0
JOINTS = (-23.39, 23.39)

rails, panels = bmesh.new(), bmesh.new()
for side in range(4):
    # Build the +y (rear) wall, then rotate the copy around Z for the other three walls.
    rot = Matrix.Rotation(math.radians(90 * side), 4, "Z")
    tmp_r, tmp_p = bmesh.new(), bmesh.new()
    y = W_IN + T / 2
    L = 2 * (W_IN + T)
    box(tmp_r, (0, y, H - 0.75), (L, T + 0.4, 1.5))                 # top rail
    box(tmp_r, (0, y, 0.5), (L, T + 0.2, 1.6))                      # kick rail
    for x in (-W_IN - T / 2, JOINTS[0], JOINTS[1], W_IN + T / 2):
        box(tmp_r, (x, y, H / 2), (1.5, T + 0.5, H))                 # posts
    for x0, x1 in ((-W_IN, JOINTS[0]), (JOINTS[0], JOINTS[1]), (JOINTS[1], W_IN)):
        box(tmp_p, ((x0 + x1) / 2, y, (H + 0.4) / 2), (x1 - x0 - 1.4, 0.25, H - 2.6))
    for bm_src, bm_dst in ((tmp_r, rails), (tmp_p, panels)):
        bmesh.ops.transform(bm_src, matrix=rot, verts=bm_src.verts)
        me = bpy.data.meshes.new("tmp"); bm_src.to_mesh(me); bm_dst.from_mesh(me); bpy.data.meshes.remove(me)
        bm_src.free()
rail_ob = finish(new_obj("PerimeterFrame", rails, "WallFrame"), bevel_in=0.08)
panel_ob = finish(new_obj("PerimeterPanels", panels, "WallPanel"))
perim = bpy.data.objects.new("Perimeter", None)
bpy.context.scene.collection.objects.link(perim)
rail_ob.parent = panel_ob.parent = perim
print("perimeter tris:", tri_count([rail_ob, panel_ob]))
print(export_fbx("Perimeter", [perim, rail_ob, panel_ob]))

# ── Tile texture ────────────────────────────────────────────────────────────────────────
N = 1024
pitch = 1.0 / 6.0
xs = (np.arange(N) + 0.5) / N
X, Y = np.meshgrid(xs, xs)
rng = np.random.default_rng(11)
val = 0.30 + rng.normal(0, 0.015, (N, N))                      # foam grain
# Smooth low-frequency mottling: bilinear upsample of a coarse noise grid.
coarse = rng.normal(0, 0.025, (17, 17))
gx, gy = X * 16, Y * 16
x0, y0 = np.floor(gx).astype(int), np.floor(gy).astype(int)
fx, fy = gx - x0, gy - y0
x1, y1 = np.minimum(x0 + 1, 16), np.minimum(y0 + 1, 16)
val += coarse[y0, x0] * (1 - fx) * (1 - fy) + coarse[y0, x1] * fx * (1 - fy) + coarse[y1, x0] * (1 - fx) * fy + coarse[y1, x1] * fx * fy


def seam_distance(coord, other):
    """Distance (field fraction) to the nearest interior seam, drawn as a wavy interlocking tab line."""
    k = np.round(coord / pitch)
    d = coord - k * pitch
    freq = 2 * np.pi * 5 / pitch                                   # 5 tabs per tile
    amp = 0.55 / 141.35
    wave = np.sin(other * freq) * amp
    slope = np.abs(np.cos(other * freq)) * amp * freq
    return np.where((k > 0) & (k < 6), np.abs(d - wave) / np.sqrt(1 + slope ** 2), 1.0)


seam = np.minimum(seam_distance(X, Y), seam_distance(Y, X)) * 141.35   # inches
val = np.where(seam < 0.12, val * 0.42, np.where(seam < 0.24, val * 0.8, val))
val = np.clip(val, 0, 1)
rgba = np.ones((N, N, 4), dtype=np.float32)
rgba[..., 0] = val * 0.98
rgba[..., 1] = val
rgba[..., 2] = val * 1.04
img = bpy.data.images.get("FieldTiles") or bpy.data.images.new("FieldTiles", N, N, alpha=False)
img.pixels.foreach_set(np.clip(rgba, 0, 1).ravel())
img.filepath_raw = os.path.join(TEXTURES, "FieldTiles.png")
img.file_format = "PNG"
img.save()

# Floor quad with 0..1 UVs across the whole field.
me = bpy.data.meshes.new("Tiles")
h = W_IN * IN
me.from_pydata([(-h, -h, 0), (h, -h, 0), (h, h, 0), (-h, h, 0)], [], [(0, 1, 2, 3)])
uv = me.uv_layers.new(name="UVMap")
for i, c in enumerate([(0, 0), (1, 0), (1, 1), (0, 1)]):
    uv.data[i].uv = c
tiles = bpy.data.objects.new("Tiles", me)
bpy.context.scene.collection.objects.link(tiles)
m = mat("Tile")
me.materials.append(m)
tex = m.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img
bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
print(export_fbx("Tiles", [tiles]))
