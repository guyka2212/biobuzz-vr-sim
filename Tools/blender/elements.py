"""POLLEN and NECTAR: low-poly unit-diameter spheres with an equirectangular hole texture.

The real elements are perforated balls with 26 holes in bands (1 at each pole, 4 at ±49°,
8 at ±15.6°). Instead of cutting holes (thousands of triangles x 56 balls), the holes are a
grey-scale texture tinted by the Unity material (yellow / red / blue).
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())
import numpy as np

clear_scene()

# ── Hole texture ────────────────────────────────────────────────────────────────────────
W, H = 512, 256
bands = [(90.0, 1, 0.0), (49.1, 4, 45.0), (15.6, 8, 0.0), (-15.6, 8, 22.5), (-49.1, 4, 0.0), (-90.0, 1, 0.0)]
centers = []
for lat, count, offset in bands:
    for i in range(count):
        lon = offset + 360.0 * i / count
        la, lo = math.radians(lat), math.radians(lon)
        centers.append((math.cos(la) * math.cos(lo), math.cos(la) * math.sin(lo), math.sin(la)))
centers = np.array(centers)

u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
lon = (u * 2 * np.pi)[None, :]
lat = (v * np.pi - np.pi / 2)[:, None]
dirs = np.stack([np.cos(lat) * np.cos(lon), np.cos(lat) * np.sin(lon), np.sin(lat) * np.ones_like(lon)], axis=-1)
ang = np.degrees(np.arccos(np.clip(dirs @ centers.T, -1, 1))).min(axis=-1)
hole_r = 9.5
shade = np.clip((ang - hole_r) / 1.2, 0, 1)            # 0 inside a hole, 1 on the shell
rim = np.clip(1 - np.abs(ang - hole_r - 0.8) / 1.5, 0, 1) * 0.12
value = 0.18 + 0.82 * shade - rim                        # dark holes, slightly shaded rims
rng = np.random.default_rng(3)
value = np.clip(value + rng.normal(0, 0.012, value.shape), 0, 1)
rgba = np.ones((H, W, 4), dtype=np.float32)
rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = value

img = bpy.data.images.get("ElementHoles") or bpy.data.images.new("ElementHoles", W, H, alpha=False)
img.pixels.foreach_set(rgba.ravel())
img.filepath_raw = os.path.join(TEXTURES, "ElementHoles.png")
img.file_format = "PNG"
img.save()

# ── Meshes ──────────────────────────────────────────────────────────────────────────────
def element(name, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.5, location=(0, 0, 0))
    ob = bpy.context.active_object
    ob.name = name; ob.data.name = name
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.append(mat(material))
    m = ob.data.materials[0]
    tex = m.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    mix = m.node_tree.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs[6].default_value = (*PALETTE[material], 1)
    m.node_tree.links.new(tex.outputs["Color"], mix.inputs[7])
    m.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return ob

pollen = element("Pollen", "Pollen")
nectar = element("NectarRed", "NectarRed")
nectar.location.x = 0.8
blue = element("NectarBlue", "NectarBlue")
blue.location.x = 1.6
for n in ("NectarRed", "NectarBlue"):
    bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
pollen.name = pollen.data.name = "Element"
print("tris per element:", tri_count([pollen]), export_fbx("Element", [pollen]))
