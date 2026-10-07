"""Robot modules, version 2: an enclosed, detailed FTC robot built from scalable modules.

Every module is ONE object with up to three material slots, in this order (RobotBuilder assigns
materials by slot index):
  0 "RobotDark"  -> team colour (chassis colour for the body, accent colour for mechanisms)
  1 "RobotMetal" -> aluminium
  2 "RobotWheel" -> black parts (wheels, flywheels, electronics, bearings)

Axes: X = robot width, Y = robot forward, Z = up (Unity: X, Z, Y). Sizes are given per module.
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())

clear_scene()
U = 1.0 / IN   # the box()/cyl() helpers take inches; U converts a unit length (metre) to that


def multi(name, parts):
    """parts: list of (bmesh, slot). Builds one object with slots 0..2."""
    combined = bmesh.new()
    for bm, slot in parts:
        for f in bm.faces:
            f.material_index = slot
        tmp = bpy.data.meshes.new("tmp"); bm.to_mesh(tmp); combined.from_mesh(tmp); bpy.data.meshes.remove(tmp); bm.free()
    ob = new_obj(name, combined, None)
    for m in ("RobotDark", "RobotMetal", "RobotWheel"):
        ob.data.materials.append(mat(m))
    return finish(ob, weld=False)


def pocket_plate(team, dark, cx, y0, y1, z0, z1, t, ribs=3):
    """A side plate seen in the YZ plane at x = cx: a team-colour frame with diagonal ribs over a
    dark backing, which reads as a machined, pocketed plate."""
    L, H = y1 - y0, z1 - z0
    yc, zc = (y0 + y1) / 2, (z0 + z1) / 2
    box(dark, (cx * U, yc * U, zc * U), (t * 0.5 * U, L * 0.98 * U, H * 0.9 * U))
    b = 0.045
    box(team, (cx * U, yc * U, (z1 - b / 2) * U), (t * U, L * U, b * U))
    box(team, (cx * U, yc * U, (z0 + b / 2) * U), (t * U, L * U, b * U))
    box(team, (cx * U, (y0 + b / 2) * U, zc * U), (t * U, b * U, H * U))
    box(team, (cx * U, (y1 - b / 2) * U, zc * U), (t * U, b * U, H * U))
    seg = L / ribs
    for i in range(ribs):
        ya = y0 + i * seg
        box(team, (cx * U, (ya + seg / 2) * U, zc * U), (t * U, b * 0.8 * U, math.hypot(seg, H) * U),
            rot=(math.degrees(math.atan2(seg, H)) * (1 if i % 2 == 0 else -1), 0, 0))
        if i > 0:
            box(team, (cx * U, ya * U, zc * U), (t * U, b * 0.8 * U, H * U))


# ── Body: unit cube centred on the origin, scaled to (width, body height, length) ──────────
def body():
    team, metal, dark = bmesh.new(), bmesh.new(), bmesh.new()
    t = 0.03
    for sx in (-1, 1):
        pocket_plate(team, dark, sx * (0.5 - t / 2), -0.5, 0.5, -0.2, 0.5, t, ribs=4)
    # Front and rear skirts (low, so the intake roller sits in front of them).
    for sy in (-1, 1):
        box(team, (0, sy * (0.5 - t / 2) * U, 0.05 * U), (0.94 * U, t * U, 0.5 * U))
    # Top deck with an opening for the hopper.
    box(metal, (0, 0.33 * U, 0.48 * U), (0.94 * U, 0.3 * U, 0.04 * U))
    box(metal, (0, -0.33 * U, 0.48 * U), (0.94 * U, 0.3 * U, 0.04 * U))
    for sx in (-1, 1):
        box(metal, (sx * 0.36 * U, 0, 0.48 * U), (0.22 * U, 0.36 * U, 0.04 * U))
    # Inner channel rails and corner standoffs.
    for sx in (-1, 1):
        box(metal, (sx * 0.43 * U, 0, -0.05 * U), (0.08 * U, 0.98 * U, 0.12 * U))
        for sy in (-1, 1):
            cyl(metal, (sx * 0.46 * U, sy * 0.46 * U, 0.15 * U), 0.025 * U, 0.65 * U, segments=8)
    # Electronics on the deck, rear.
    box(dark, (0.2 * U, -0.36 * U, 0.53 * U), (0.32 * U, 0.2 * U, 0.06 * U))      # Control Hub
    box(dark, (-0.22 * U, -0.38 * U, 0.53 * U), (0.22 * U, 0.12 * U, 0.06 * U))   # battery
    cyl(dark, (-0.08 * U, -0.3 * U, 0.535 * U), 0.03 * U, 0.04 * U, segments=8)   # power switch
    return multi("Body", [(team, 0), (metal, 1), (dark, 2)])


# ── Turret: uniform scale = turret diameter; shoots toward +Y; ball exits near (0, 0.55, 0.62) ──
def turret():
    team, metal, dark = bmesh.new(), bmesh.new(), bmesh.new()
    # Geared turntable.
    cyl(dark, (0, 0, 0.04 * U), 0.5 * U, 0.08 * U, segments=24)
    for i in range(32):
        a = 2 * math.pi * i / 32
        box(dark, (math.cos(a) * 0.5 * U, math.sin(a) * 0.5 * U, 0.04 * U), (0.05 * U, 0.04 * U, 0.08 * U),
            rot=(0, 0, math.degrees(a)))
    cyl(metal, (0, 0, 0.1 * U), 0.42 * U, 0.04 * U, segments=20)                       # top plate
    # Flywheel side plates (team colour), flywheels and shaft.
    for sx in (-1, 1):
        box(team, (sx * 0.28 * U, 0.05 * U, 0.42 * U), (0.04 * U, 0.7 * U, 0.6 * U))
    for sx in (-0.12, 0.12):
        cyl(dark, (sx * U, 0.18 * U, 0.4 * U), 0.2 * U, 0.12 * U, segments=16, axis="X")
    cyl(metal, (0, 0.18 * U, 0.4 * U), 0.03 * U, 0.6 * U, segments=8, axis="X")
    # Curved hood over the flywheels: slats on an arc, exiting up and forward.
    for k in range(6):
        a = math.radians(-10 + 20 * k)
        y = 0.18 + 0.3 * math.sin(a)
        z = 0.4 + 0.3 * math.cos(a)
        box(metal, (0, y * U, z * U), (0.52 * U, 0.1 * U, 0.03 * U), rot=(-math.degrees(a), 0, 0))
    # Feeder ramp from the hopper side (rear) up into the flywheels, and the motor.
    box(metal, (0, -0.18 * U, 0.3 * U), (0.4 * U, 0.4 * U, 0.03 * U), rot=(-25, 0, 0))
    cyl(dark, (0.36 * U, 0.18 * U, 0.4 * U), 0.08 * U, 0.2 * U, segments=12, axis="X")
    return multi("Turret", [(team, 0), (metal, 1), (dark, 2)])


# ── Dumper: unit cube scaled to (span, 4 in, 5 in); bucket opens toward +Y ─────────────────
def dumper():
    team, metal, dark = bmesh.new(), bmesh.new(), bmesh.new()
    box(team, (0, -0.05 * U, -0.42 * U), (1.0 * U, 0.8 * U, 0.06 * U))                # floor
    box(team, (0, -0.47 * U, 0.0), (1.0 * U, 0.06 * U, 0.86 * U))                    # back
    for sx in (-1, 1):
        box(team, (sx * 0.48 * U, -0.1 * U, -0.05 * U), (0.04 * U, 0.8 * U, 0.76 * U))   # sides
        box(metal, (sx * 0.52 * U, -0.3 * U, -0.3 * U), (0.04 * U, 0.12 * U, 0.6 * U), rot=(30, 0, 0))  # arms
    box(metal, (0, 0.38 * U, -0.25 * U), (1.0 * U, 0.3 * U, 0.04 * U), rot=(35, 0, 0))  # launch lip
    cyl(dark, (0, -0.45 * U, -0.48 * U), 0.06 * U, 1.1 * U, segments=10, axis="X")    # pivot shaft
    return multi("Dumper", [(team, 0), (metal, 1), (dark, 2)])


# ── Box Tube slide: unit cube scaled to (1.5 in, height, 1.5 in); two rails + carriage ─────
def box_tube():
    metal, dark = bmesh.new(), bmesh.new()
    for sx in (-1, 1):
        box(metal, (sx * 0.32 * U, 0, 0), (0.28 * U, 1.0 * U, 1.0 * U))
        box(metal, (sx * 0.32 * U, 0.52 * U, 0), (0.18 * U, 0.06 * U, 1.0 * U))    # lip
    for z in (-0.47, 0.47):
        box(dark, (0, 0, z * U), (1.0 * U, 1.05 * U, 0.06 * U))                    # end blocks
    return multi("BoxTube", [(metal, 1), (dark, 2)])


# ── Placer cup at the top of the slide: unit cube scaled to (4.2, 1.2, 4.2) in ─────────────
def placer_cup():
    team, dark = bmesh.new(), bmesh.new()
    box(team, (0, 0, -0.4 * U), (1.0 * U, 1.0 * U, 0.2 * U))
    for sx in (-1, 1):
        box(team, (sx * 0.47 * U, 0, 0), (0.06 * U, 1.0 * U, 0.8 * U))
    box(team, (0, -0.47 * U, 0), (1.0 * U, 0.06 * U, 0.8 * U))
    box(dark, (0, 0.48 * U, -0.1 * U), (0.6 * U, 0.06 * U, 0.4 * U))                 # release flap
    return multi("PlacerCup", [(team, 0), (dark, 2)])


# ── Intake side plate: unit cube scaled to (0.25, 4, reach + 1) in, rounded front ──────────
def intake_plate():
    team, dark = bmesh.new(), bmesh.new()
    box(team, (0, -0.1 * U, 0), (1.0 * U, 0.8 * U, 1.0 * U))
    cyl(team, (0, 0.3 * U, 0), 0.5 * U, 1.0 * U, segments=12, axis="X")
    cyl(dark, (0.55 * U, 0.3 * U, 0), 0.12 * U, 0.1 * U, segments=8, axis="X")      # bearing
    return multi("IntakePlate", [(team, 0), (dark, 2)])


mods = [body(), turret(), dumper(), box_tube(), placer_cup(), intake_plate()]
for i, o in enumerate(mods):
    o.location = ((i % 4) * 1.6, (i // 4) * 1.8, 0)
for o in mods:
    print(o.name, tri_count([o]))


# Export each module at the origin, unscaled.
for o in mods:
    loc = o.location.copy()
    o.location = (0, 0, 0)
    print(export_fbx("Robot" + o.name, [o]))
    o.location = loc
