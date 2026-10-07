"""HIVE frame (§9.6.1, Fig 9-8): 49.46 in wide x 38.95 in deep, pivots 43.95 in up.

Field frame, origin at field centre. Two triangular side frames on the x = ±24 tile seams, a
crossbar joining their apexes, axle cradles at the pivots (x = ±12.75), and a decorative
honeycomb panel on each side (original artwork, not the BIOBUZZ logo).
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())

clear_scene()
FOOT_Y = 38.95 / 2
APEX_Z = 43.95 + 1.0
SIDE_X = 24.5
leg_len = math.hypot(FOOT_Y, APEX_Z)
leg_ang = math.degrees(math.atan2(FOOT_Y, APEX_Z))

frame = bmesh.new()
for sx in (-1, 1):
    x = sx * SIDE_X
    for sy in (-1, 1):
        # Leg from the foot (x, sy*FOOT_Y, 0) up to the apex (x, 0, APEX_Z).
        box(frame, (x, sy * FOOT_Y / 2, APEX_Z / 2), (1.0, 1.5, leg_len), rot=(sy * leg_ang, 0, 0))
    box(frame, (x, 0, 0.15), (1.0, 2 * FOOT_Y + 2, 0.3))                 # foot bar on the tiles
    box(frame, (x, 0, APEX_Z + 0.2), (1.6, 5.0, 2.2))                    # apex gusset
    box(frame, (x, 0, 22.0), (0.8, 2 * FOOT_Y * (1 - 22.0 / APEX_Z) + 1.2, 1.0))  # mid brace
box(frame, (0, 0, APEX_Z + 0.75), (2 * SIDE_X + 1.0, 1.5, 1.5))          # crossbar
for px in (-12.75, 12.75):
    for dx in (-2.8, 2.8):
        box(frame, (px + dx, 0, 44.6), (0.8, 2.6, 3.0))                  # axle cradles
frame_ob = finish(new_obj("HiveFrame", frame, "HiveFrame"), bevel_in=0.12)

panel = bmesh.new()
for sx in (-1, 1):
    box(panel, (sx * SIDE_X, 0, 36.0), (0.25, 9.0, 10.0))
panel_ob = finish(new_obj("FramePanels", panel, "WallPanel"), bevel_in=0.1)

hexes = bmesh.new()
for sx in (-1, 1):
    for (y, z) in ((-2.2, 34.4), (2.2, 34.4), (0.0, 38.2), (-4.0, 38.2), (4.0, 38.2), (-2.2, 42.0), (2.2, 42.0)):
        if abs(y) > 3.9 and z > 38: continue
        cyl(hexes, (sx * (SIDE_X + 0.2), y, z), 1.25, 0.18, segments=6, axis="X")
hex_ob = finish(new_obj("FrameHexes", hexes, "AccentYellow"), weld=False)

root = bpy.data.objects.new("HiveFrameArt", None)
bpy.context.scene.collection.objects.link(root)
parts = [frame_ob, panel_ob, hex_ob]
for o in parts:
    o.parent = root
print("frame tris:", tri_count(parts))

print(export_fbx("HiveFrame", [root] + parts))
