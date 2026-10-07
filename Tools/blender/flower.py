"""FLOWER (Competition Manual §9.7, Fig 9-12; dimensions from FIRST's field CAD).

Local frame: origin at the bore centre on the tiles, +Y into the field, the perimeter wall face
at y = -2.63 in. Matches FieldBuilder.BuildFlower's colliders.
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())

clear_scene()

BACK, FRONT = -2.63, 2.37          # plate extent toward the wall / into the field
HALF_ALONG = 5.95 / 2


def plate_with_hole(bm, hole_r, z0, z1, segments=20):
    """Rectangular plate (BACK..FRONT, ±HALF_ALONG) with a round hole at the origin."""
    top, bot = z1 * IN, z0 * IN
    rings = {k: [] for k in ("it", "ib", "ot", "ob")}
    for i in range(segments):
        a = 2 * math.pi * i / segments + math.pi / segments
        dx, dy = math.cos(a), math.sin(a)
        ks = []
        if dx > 1e-6: ks.append(HALF_ALONG / dx)
        if dx < -1e-6: ks.append(-HALF_ALONG / dx)
        if dy > 1e-6: ks.append(FRONT / dy)
        if dy < -1e-6: ks.append(BACK / dy)
        k = min(ks)
        for key, r, z in (("it", hole_r, top), ("ib", hole_r, bot), ("ot", k, top), ("ob", k, bot)):
            rings[key].append(bm.verts.new((dx * r * IN, dy * r * IN, z)))
    it, ib, ot, ob = rings["it"], rings["ib"], rings["ot"], rings["ob"]
    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((ot[i], ot[j], it[j], it[i]))
        bm.faces.new((ob[i], ib[i], ib[j], ob[j]))
        bm.faces.new((it[i], it[j], ib[j], ib[i]))
        bm.faces.new((ot[i], ob[i], ob[j], ot[j]))


# Rings: lower (sits on the tiles), middle (bottom of the scoring volume), top (the opening).
rings = bmesh.new()
plate_with_hole(rings, 3.222 / 2, 0.0, 0.354)
plate_with_hole(rings, 3.896 / 2, 3.904, 5.254)
plate_with_hole(rings, 4.171 / 2, 20.254, 21.404)
ring_ob = finish(new_obj("FlowerRings", rings, "FlowerRing"), bevel_in=0.06)

# Four HIPS pipes between the middle and top rings.
pipes = bmesh.new()
for i in range(4):
    a = math.radians(45 + 90 * i)
    d = 4.171 / 2 + 0.42
    cyl(pipes, (math.cos(a) * d, math.sin(a) * d, (5.254 + 20.254) / 2), 0.42, 15.0, segments=10)
pipe_ob = finish(new_obj("FlowerPipes", pipes, "FlowerPipe", smooth=True))

# Wall-side square extrusion (spine), backstop, and the bracket that hooks over the perimeter.
frame = bmesh.new()
box(frame, (0, BACK + 0.25, 21.404 / 2), (1.0, 0.5, 21.404))
box(frame, (0, BACK + 0.2, 21.404 + 1.25 / 2), (5.95, 0.4, 1.25))
box(frame, (0, BACK - 0.6, 11.0 + 0.4), (2.2, 2.4, 0.8))          # over the wall top
box(frame, (0, BACK - 1.6, 11.0 - 1.5), (2.2, 0.25, 3.0))         # drop on the outside
box(frame, (0, BACK + 0.25, 0.1), (2.2, 1.6, 0.2))                # foot under the wall
frame_ob = finish(new_obj("FlowerFrame", frame, "WallFrame"), bevel_in=0.05)

# Decorative petals around the opening (visual only, 0.12 in thick, above the top ring).
petals = bmesh.new()
for i in range(6):
    a = math.radians(30 + 60 * i)
    if math.sin(a) < -0.6:      # keep petals off the wall side
        continue
    r0, r1, w = 2.25, 3.6, 1.1
    c = ((r0 + r1) / 2 * math.cos(a), (r0 + r1) / 2 * math.sin(a))
    verts = []
    for k in range(8):
        t = 2 * math.pi * k / 8
        lx, ly = (r1 - r0) / 2 * math.cos(t), w / 2 * math.sin(t)
        x = c[0] + lx * math.cos(a) - ly * math.sin(a)
        y = c[1] + lx * math.sin(a) + ly * math.cos(a)
        verts.append(petals.verts.new((x * IN, y * IN, 21.42 * IN)))
    f = petals.faces.new(verts)
    ext = bmesh.ops.extrude_face_region(petals, geom=[f])
    bmesh.ops.translate(petals, verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)], vec=(0, 0, 0.12 * IN))
petal_ob = finish(new_obj("FlowerPetals", petals, "FlowerPipe"))

root = bpy.data.objects.new("Flower", None)
bpy.context.scene.collection.objects.link(root)
for o in (ring_ob, pipe_ob, frame_ob, petal_ob):
    o.parent = root
print("flower tris:", tri_count([ring_ob, pipe_ob, frame_ob, petal_ob]))

print(export_fbx("Flower", list(bpy.data.objects)))
