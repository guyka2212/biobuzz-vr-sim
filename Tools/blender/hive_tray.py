"""HIVE tray (§9.6.2, Figs 9-9…9-11; cell geometry from FIRST's field CAD).

Local frame matches Hive.cs: origin at the pivot, the bar along +Y ("v"), +Z = "w" (up when
level), X across the cell. Each cell is a hollow pentagon-section prism, open at its outer end.
Exports HiveTrayRed.fbx and HiveTrayBlue.fbx (same mesh, alliance material).
"""
exec(open(r"C:\Users\User\Documents\VrFsim\Fully-Vr\VrFsim\Tools\blender\vrfsim_lib.py").read())

INNER_V, OUTER_V = 9.644, 21.394
FLOOR_W, EAVE_W, PEAK_W, HW = -1.468, 6.09, 12.529, 10.07
T = 0.3   # wall thickness


def pentagon(off):
    """Cell cross-section (x, w), offset outward by `off` inches."""
    theta = math.atan2(PEAK_W - EAVE_W, HW)
    slope = (PEAK_W - EAVE_W) / HW
    peak = PEAK_W + off / math.cos(theta)
    eave = peak - slope * (HW + off)
    return [(-HW - off, FLOOR_W - off), (HW + off, FLOOR_W - off), (HW + off, eave), (0.0, peak), (-HW - off, eave)]


def cell_shell(bm, s):
    """Hollow prism: outer skin, inner skin, mouth rim, back wall (both faces)."""
    v_back_out, v_back_in, v_mouth = s * (INNER_V - T), s * INNER_V, s * OUTER_V
    outer, inner = pentagon(T), pentagon(0.0)
    def ring(poly, v): return [bm.verts.new((x * IN, v * IN, w * IN)) for x, w in poly]
    ob, om = ring(outer, v_back_out), ring(outer, v_mouth)
    ib, im = ring(inner, v_back_in), ring(inner, v_mouth)
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ob[i], ob[j], om[j], om[i]))      # outer skin
        bm.faces.new((ib[i], im[i], im[j], ib[j]))      # inner skin
        bm.faces.new((om[i], om[j], im[j], im[i]))      # mouth rim
    bm.faces.new(ob[::-1])                              # back wall, outside
    bm.faces.new(ib)                                    # back wall, inside


def build(alliance):
    clear_scene()
    material = "HiveRed" if alliance == "Red" else "HiveBlue"
    shells = bmesh.new()
    for s in (-1, 1):
        cell_shell(shells, s)
        # A raised lip around the mouth makes the opening read clearly from the driver station.
        lip_poly = pentagon(T + 0.35)
        inner_poly = pentagon(T)
        v0, v1 = s * (OUTER_V - 0.8), s * OUTER_V
        lo = [shells.verts.new((x * IN, v0 * IN, w * IN)) for x, w in lip_poly]
        hi = [shells.verts.new((x * IN, v1 * IN, w * IN)) for x, w in lip_poly]
        li = [shells.verts.new((x * IN, v0 * IN, w * IN)) for x, w in inner_poly]
        hi_in = [shells.verts.new((x * IN, v1 * IN, w * IN)) for x, w in inner_poly]
        for i in range(5):
            j = (i + 1) % 5
            shells.faces.new((lo[i], lo[j], hi[j], hi[i]))
            shells.faces.new((hi[i], hi[j], hi_in[j], hi_in[i]))
            shells.faces.new((li[i], li[j], lo[j], lo[i]))
        # Raised honeycomb on both side walls.
        vm = s * (INNER_V + OUTER_V) / 2
        for sx in (-1, 1):
            for dv, w in ((-3.4, 0.7), (3.4, 0.7), (0.0, 3.9)):
                cyl(shells, (sx * (HW + T + 0.1), vm + dv, w), 1.75, 0.2, segments=6, axis="X")
    shell_ob = finish(new_obj("Cells", shells, material), weld=False)

    bar = bmesh.new()
    box(bar, (0, 0, -0.6), (3.0, 2 * INNER_V, 1.8))                          # connecting bar
    for s in (-1, 1):
        box(bar, (0, s * (INNER_V + 6.0), FLOOR_W - T - 0.6), (2.2, 10.0, 1.2))  # stiffener under each cell
    bar_ob = finish(new_obj("Bar", bar, "HiveFrame"), bevel_in=0.08)

    axle = bmesh.new()
    cyl(axle, (0, 0, 0), 0.5, 5.0, segments=12, axis="X")
    axle_ob = finish(new_obj("Axle", axle, "RobotMetal", smooth=True))

    damper = bmesh.new()
    for s in (-1, 1):
        box(damper, (0, s * 3.2, 0.9), (2.2, 1.4, 1.2))
    damper_ob = finish(new_obj("Dampers", damper, "RobotWheel"), bevel_in=0.15)

    # AprilTag cluster stickers on each cell's underside (generic pattern, not the real tag images).
    tags = bmesh.new()
    for s in (-1, 1):
        vc = s * (OUTER_V - 9.938)
        box(tags, (0, vc, FLOOR_W - T - 0.03), (14.0, 7.5, 0.04))
    tag_ob = finish(new_obj("AprilTags", tags, "RobotDark"))

    root = bpy.data.objects.new(f"HiveTray{alliance}", None)
    bpy.context.scene.collection.objects.link(root)
    parts = [shell_ob, bar_ob, axle_ob, damper_ob, tag_ob]
    for o in parts:
        o.parent = root
    return root, parts


for alliance in ("Red", "Blue"):
    root, parts = build(alliance)
    print(alliance, "tray tris:", tri_count(parts), export_fbx(f"HiveTray{alliance}", [root] + parts))
