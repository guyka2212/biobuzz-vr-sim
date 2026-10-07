"""Robot part meshes for RobotArt. Each part is ONE object with ONE material, modelled at unit
size around its origin so RobotBuilder can scale it to any legal build:

  Wheels (mecanum / traction / omni): diameter 1, axle along X, true width proportion
  SwerveModule: unit cube housing          Turret: unit cube launcher body, shoots toward +Y
  Dumper: unit cube bucket, opens toward +Y BoxTube: unit cube, extends along Z (Unity Y)
  IntakeRoller: unit length along X, unit diameter

Blender +Y maps to Unity +Z (forward), Blender +Z to Unity +Y (up).
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())

clear_scene()
U = 1.0 / IN   # helpers take inches; 1 unit (metre) = U "inches"


def roller_wheel(name, roller_angle_deg, rollers, width_frac, roller_len, material="RobotWheel"):
    """Hub with side plates and barrel rollers around the rim (mecanum: 45°, omni: 90°)."""
    bm = bmesh.new()
    R = 0.5
    cyl(bm, (0, 0, 0), 0.30 * U, width_frac * 0.92 * U, segments=16, axis="X")          # hub
    for sx in (-1, 1):
        cyl(bm, (sx * width_frac * 0.47 * U, 0, 0), 0.40 * U, 0.04 * U, segments=16, axis="X")  # side plates
    for i in range(rollers):
        a = 2 * math.pi * i / rollers
        r = R - 0.07
        center = Vector((0, math.cos(a) * r, math.sin(a) * r))
        tmp = bmesh.new()
        # Barrel roller: a capsule-ish cone pair along the local X axis.
        half = roller_len / 2
        bmesh.ops.create_cone(tmp, cap_ends=True, segments=8, radius1=0.035, radius2=0.07, depth=half,
                              matrix=Matrix.Translation((half / 2, 0, 0)) @ Matrix.Rotation(math.radians(-90), 4, "Y"))
        bmesh.ops.create_cone(tmp, cap_ends=True, segments=8, radius1=0.07, radius2=0.035, depth=half,
                              matrix=Matrix.Translation((-half / 2, 0, 0)) @ Matrix.Rotation(math.radians(-90), 4, "Y"))
        tilt = Matrix.Rotation(math.radians(roller_angle_deg), 4, Vector((0, math.cos(a), math.sin(a))))
        bmesh.ops.transform(tmp, matrix=Matrix.Translation(center) @ tilt, verts=tmp.verts)
        me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); bm.from_mesh(me); bpy.data.meshes.remove(me); tmp.free()
    return finish(new_obj(name, bm, material, smooth=True), weld=False)


def traction_wheel(name):
    bm = bmesh.new()
    w = 0.45
    cyl(bm, (0, 0, 0), 0.5 * U, w * U, segments=24, axis="X")                  # tyre
    for sx in (-1, 1):
        cyl(bm, (sx * w * 0.5 * U, 0, 0), 0.34 * U, 0.03 * U, segments=16, axis="X")   # hub faces
    # Tread grooves: shallow raised ribs around the tyre.
    for i in range(18):
        a = 2 * math.pi * i / 18
        box(bm, (0, math.cos(a) * 0.5 * U, math.sin(a) * 0.5 * U), (w * 0.9 * U, 0.035 * U, 0.035 * U),
            rot=(math.degrees(a), 0, 0))
    return finish(new_obj(name, bm, "RobotWheel"), weld=False)


def swerve_module():
    bm = bmesh.new()
    box(bm, (0, 0, 0.1 * U), (1.0 * U, 1.0 * U, 0.6 * U))               # housing
    cyl(bm, (0, 0, 0.45 * U), 0.32 * U, 0.3 * U, segments=16)           # steering motor
    box(bm, (0, 0, -0.35 * U), (0.25 * U, 0.8 * U, 0.3 * U))            # fork
    return finish(new_obj("SwerveModule", bm, "RobotMetal"))


def turret():
    bm = bmesh.new()
    box(bm, (0, -0.1 * U, -0.25 * U), (0.9 * U, 0.8 * U, 0.5 * U))     # base / feeder
    cyl(bm, (0, 0.18 * U, 0.15 * U), 0.36 * U, 0.85 * U, segments=16, axis="X")   # flywheel shroud
    # Curved hood: a few angled slats that guide the shot up and forward (+Y).
    for k in range(4):
        a = math.radians(20 + 18 * k)
        box(bm, (0, (0.1 + 0.35 * math.sin(a)) * U, (0.2 + 0.32 * math.cos(a)) * U), (0.8 * U, 0.08 * U, 0.22 * U),
            rot=(-math.degrees(a), 0, 0))
    box(bm, (0, -0.42 * U, 0.1 * U), (0.5 * U, 0.12 * U, 0.6 * U))     # feeder tower
    return finish(new_obj("Turret", bm, "RobotMetal"), weld=False)


def dumper():
    bm = bmesh.new()
    box(bm, (0, -0.1 * U, -0.42 * U), (1.0 * U, 0.8 * U, 0.08 * U))    # floor
    box(bm, (0, -0.48 * U, 0.0), (1.0 * U, 0.08 * U, 0.9 * U))         # back wall
    for sx in (-1, 1):
        box(bm, (sx * 0.48 * U, -0.1 * U, 0.0), (0.06 * U, 0.8 * U, 0.9 * U))   # side walls
    box(bm, (0, 0.32 * U, -0.3 * U), (1.0 * U, 0.3 * U, 0.06 * U), rot=(30, 0, 0))  # launch lip
    cyl(bm, (0, -0.5 * U, -0.45 * U), 0.08 * U, 1.0 * U, segments=10, axis="X")    # pivot
    return finish(new_obj("Dumper", bm, "RobotMetal"), weld=False)


def box_tube():
    bm = bmesh.new()
    t = 0.12
    for sx, sy, w, d in ((-1, 0, t, 1.0), (1, 0, t, 1.0), (0, -1, 1.0, t), (0, 1, 1.0, t)):
        box(bm, (sx * (0.5 - t / 2) * U, sy * (0.5 - t / 2) * U, 0), (w * U, d * U, 1.0 * U))
    # Bearing blocks at both ends, like a slide's carriage.
    for z in (-0.46, 0.46):
        box(bm, (0, 0, z * U), (1.12 * U, 1.12 * U, 0.08 * U))
    return finish(new_obj("BoxTube", bm, "RobotMetal"), weld=False)


def intake_roller():
    bm = bmesh.new()
    cyl(bm, (0, 0, 0), 0.12 * U, 1.0 * U, segments=8, axis="X")         # shaft
    n = 7
    for i in range(n):
        x = -0.43 + 0.86 * i / (n - 1)
        # Compliant star wheels: a hub with six flaps.
        cyl(bm, (x * U, 0, 0), 0.22 * U, 0.06 * U, segments=8, axis="X")
        for k in range(6):
            a = 2 * math.pi * k / 6 + i * 0.3
            box(bm, (x * U, math.cos(a) * 0.36 * U, math.sin(a) * 0.36 * U), (0.05 * U, 0.08 * U, 0.3 * U),
                rot=(math.degrees(a) + 90, 0, 0))
    return finish(new_obj("IntakeRoller", bm, "RobotMetal"), weld=False)


parts = [
    roller_wheel("MecanumWheel", 45, 9, 0.45, 0.6),
    traction_wheel("TractionWheel"),
    roller_wheel("OmniWheel", 90, 12, 0.4, 0.24),
    swerve_module(),
    turret(),
    dumper(),
    box_tube(),
    intake_roller(),
]
for i, o in enumerate(parts):
    o.location = ((i % 4) * 1.6, (i // 4) * 1.8, 0)
for o in parts:
    print(o.name, tri_count([o]))


def export_all():
    for o in parts:
        loc = o.location.copy()
        o.location = (0, 0, 0)
        print(export_fbx("Robot" + o.name, [o]))
        o.location = loc
