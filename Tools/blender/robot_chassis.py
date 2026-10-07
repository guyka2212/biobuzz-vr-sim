"""goBILDA-style drive chassis, unit cube (1 x 1 x 1) centred on its origin:
X = width, Y = length (forward +Y), Z = height. RobotBuilder scales it to (width, 4 in, length).

Material slots, in this order (RobotBuilder maps them by index):
  0 RobotDark  -> tinted with the robot's chassis colour (base plate)
  1 RobotMetal -> aluminium U-channel rails and cross members
  2 RobotWheel -> electronics (Control Hub, battery, motors)
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())

clear_scene()
U = 1.0 / IN
plate, metal, dark = bmesh.new(), bmesh.new(), bmesh.new()

# Base plate with lightening pockets (raised ribs around rectangular pockets).
box(plate, (0, 0, -0.08 * U), (0.84 * U, 0.86 * U, 0.06 * U))
for ix in (-1, 0, 1):
    for iy in (-1, 0, 1):
        if ix == 0 and iy == 0:
            continue
        box(plate, (ix * 0.27 * U, iy * 0.27 * U, -0.035 * U), (0.2 * U, 0.2 * U, 0.03 * U))

# U-channel side rails (open toward the inside), front/back cross channels.
for sx in (-1, 1):
    x = sx * 0.46
    box(metal, (x * U, 0, 0.02 * U), (0.07 * U, 1.0 * U, 0.6 * U))                    # web
    for z in (-0.28, 0.32):
        box(metal, ((x - sx * 0.045) * U, 0, z * U), (0.09 * U, 1.0 * U, 0.04 * U))   # flanges
    # Hole pattern suggested by small raised bosses along the web.
    for k in range(7):
        y = -0.42 + 0.14 * k
        cyl(metal, ((x + sx * 0.04) * U, y * U, 0.02 * U), 0.045 * U, 0.02 * U, segments=8, axis="X")
for sy in (-1, 1):
    box(metal, (0, sy * 0.47 * U, 0.02 * U), (0.86 * U, 0.06 * U, 0.6 * U))

# Electronics: Control Hub, battery, two drive motors visible between the rails.
box(dark, (0.12 * U, -0.18 * U, 0.06 * U), (0.36 * U, 0.26 * U, 0.12 * U))      # Control Hub
box(dark, (-0.2 * U, 0.18 * U, 0.08 * U), (0.22 * U, 0.34 * U, 0.16 * U))       # battery
for sx in (-1, 1):
    for sy in (-1, 1):
        cyl(dark, (sx * 0.3 * U, sy * 0.3 * U, 0.0), 0.06 * U, 0.22 * U, segments=10, axis="X")  # motors

me = bpy.data.meshes.new("Chassis")
combined = bmesh.new()
for src, mi in ((plate, 0), (metal, 1), (dark, 2)):
    for f in src.faces:
        f.material_index = mi
    tmp = bpy.data.meshes.new("tmp"); src.to_mesh(tmp); combined.from_mesh(tmp); bpy.data.meshes.remove(tmp); src.free()
ob = new_obj("Chassis", combined, None)
for name in ("RobotDark", "RobotMetal", "RobotWheel"):
    ob.data.materials.append(mat(name))
finish(ob, weld=False)
print("chassis tris:", tri_count([ob]))
print(export_fbx("RobotChassis", [ob]))
