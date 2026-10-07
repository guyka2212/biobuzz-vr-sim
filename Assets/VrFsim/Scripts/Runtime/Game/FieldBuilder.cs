using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>
    /// Builds the BIOBUZZ field from <see cref="FieldSpec"/>: simple box/capsule colliders sized
    /// to the real geometry, plus greybox visuals. When art prefabs are supplied (Blender FBX), the
    /// greybox visuals for that part are skipped and the art is placed instead; colliders never
    /// come from art meshes.
    /// </summary>
    public static class FieldBuilder
    {
        public class Result
        {
            public Transform root;
            public Transform staticRoot;
            public Hive redHive, blueHive;
            public Flower[] flowers = new Flower[FieldSpec.FlowerCount];

            public Hive HiveOf(Alliance a) => a == Alliance.Red ? redHive : blueHive;
        }

        static Mesh cube, cylinder;

        static Mesh Cube => cube ? cube : cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        static Mesh Cylinder => cylinder ? cylinder : cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

        public static Result Build(Transform parent, MaterialLibrary lib, FieldArt art = null)
        {
            var res = new Result();
            res.root = new GameObject("Field").transform;
            res.root.SetParent(parent, false);
            res.staticRoot = Child(res.root, "Static");

            BuildFloorAndWalls(res.staticRoot, lib, art);
            BuildTape(res.staticRoot, lib);
            BuildVenue(res.staticRoot, lib, art);
            BuildHiveFrame(res.staticRoot, lib, art);

            var hives = Child(res.root, "Hives");
            res.redHive = BuildHive(hives, Alliance.Red, lib, art);
            res.blueHive = BuildHive(hives, Alliance.Blue, lib, art);

            var flowers = Child(res.staticRoot, "Flowers");
            for (int i = 0; i < FieldSpec.FlowerCount; i++) res.flowers[i] = BuildFlower(flowers, i, lib, art);
            return res;
        }

        // ── Floor, walls ────────────────────────────────────────────────────────────────────

        static void BuildFloorAndWalls(Transform root, MaterialLibrary lib, FieldArt art)
        {
            float w = FieldSpec.WallInner;
            var floor = Child(root, "Floor");

            // One collider for the tile surface (top at y = 0) and one for the venue floor below it.
            var tileCol = floor.gameObject.AddComponent<BoxCollider>();
            tileCol.center = new Vector3(0f, -0.25f, 0f);
            tileCol.size = new Vector3(Units.In(2f * w), 0.5f, Units.In(2f * w));
            tileCol.sharedMaterial = lib.tilePhysics;

            if (art && art.tiles) Place(art.tiles, floor);
            else
            {
                Visual(floor, "Tiles", Units.Field(0, 0, -0.05f), Units.Size(2f * w, 2f * w, 0.1f), Quaternion.identity, lib.tile);
                // Tile seams: the 24-in grid, slightly darker.
                for (int i = 1; i < 6; i++)
                {
                    float s = -w + i * FieldSpec.TilePitch;
                    Visual(floor, "SeamX" + i, Units.Field(s, 0, 0.01f), Units.Size(0.12f, 2f * w, 0.01f), Quaternion.identity, lib.robotDark);
                    Visual(floor, "SeamY" + i, Units.Field(0, s, 0.01f), Units.Size(2f * w, 0.12f, 0.01f), Quaternion.identity, lib.robotDark);
                }
            }

            var walls = Child(root, "Walls");
            float t = FieldSpec.WallThickness, h = FieldSpec.WallHeight;
            float half = w + t * 0.5f, len = 2f * (w + t);
            WallSegment(walls, "WallLeft", new Vector2(-half, 0f), new Vector2(t, len), h, lib, art);
            WallSegment(walls, "WallRight", new Vector2(half, 0f), new Vector2(t, len), h, lib, art);
            WallSegment(walls, "WallAudience", new Vector2(0f, -half), new Vector2(len, t), h, lib, art);
            WallSegment(walls, "WallRear", new Vector2(0f, half), new Vector2(len, t), h, lib, art);
            if (art && art.perimeter) Place(art.perimeter, walls);
        }

        static void WallSegment(Transform parent, string name, Vector2 c, Vector2 size, float h, MaterialLibrary lib, FieldArt art)
        {
            // Tall invisible extension stops elements that clip the top rail at speed.
            var go = Collider(parent, name, Units.Field(c.x, c.y, (h - FieldSpec.TileThickness) * 0.5f),
                Units.Size(size.x, size.y, h + FieldSpec.TileThickness), Quaternion.identity, lib.wallPhysics);
            if (art && art.perimeter) return;
            bool alongX = size.x > size.y;
            var panelSize = Units.Size(alongX ? size.x : 0.5f, alongX ? 0.5f : size.y, h - 1.5f);
            Visual(go.transform.parent, name + "Panel", Units.Field(c.x, c.y, (h - 1.5f) * 0.5f), panelSize, Quaternion.identity, lib.wall);
            Visual(go.transform.parent, name + "Rail", Units.Field(c.x, c.y, h - 0.75f), Units.Size(size.x + 0.4f, size.y + 0.4f, 1.5f), Quaternion.identity, lib.wallFrame);
        }

        // ── Tape: LOADING ZONES, GARDENS, ALLIANCE AREAS ───────────────────────────────────

        static void BuildTape(Transform root, MaterialLibrary lib)
        {
            var tape = Child(root, "Tape");
            float tw = FieldSpec.TapeWidth;
            foreach (Alliance a in new[] { Alliance.Red, Alliance.Blue })
            {
                var mat = a == Alliance.Red ? lib.tapeRed : lib.tapeBlue;
                var lz = FieldSpec.LoadingZoneRed.For(a);
                // The zone includes its tape; tape runs on the three field-side edges.
                bool left = a == Alliance.Red;
                float innerX = left ? lz.xMax - tw * 0.5f : lz.xMin + tw * 0.5f;
                TapeStrip(tape, $"LZ_{a}_Inner", new FieldRect(innerX - tw * 0.5f, innerX + tw * 0.5f, lz.yMin, lz.yMax), mat);
                TapeStrip(tape, $"LZ_{a}_A", new FieldRect(lz.xMin, lz.xMax, lz.yMin, lz.yMin + tw), mat);
                TapeStrip(tape, $"LZ_{a}_B", new FieldRect(lz.xMin, lz.xMax, lz.yMax - tw, lz.yMax), mat);

                // The GARDEN is the two strips of 1-in tape themselves.
                TapeStrip(tape, $"Garden_{a}", FieldSpec.GardenRed.For(a), mat);

                var area = FieldSpec.AllianceAreaRed.For(a);
                float outerX = left ? area.xMin : area.xMax - tw;
                TapeStrip(tape, $"Area_{a}_Back", new FieldRect(outerX, outerX + tw, area.yMin, area.yMax), mat, -FieldSpec.TileThickness);
                TapeStrip(tape, $"Area_{a}_S", new FieldRect(area.xMin, area.xMax, area.yMin, area.yMin + tw), mat, -FieldSpec.TileThickness);
                TapeStrip(tape, $"Area_{a}_N", new FieldRect(area.xMin, area.xMax, area.yMax - tw, area.yMax), mat, -FieldSpec.TileThickness);
            }
        }

        static void TapeStrip(Transform parent, string name, FieldRect r, Material mat, float baseZ = 0f)
        {
            var c = r.Center; var s = r.Size;
            Visual(parent, name, Units.Field(c.x, c.y, baseZ + 0.02f), Units.Size(s.x, s.y, 0.02f), Quaternion.identity, mat);
        }

        // ── Venue ───────────────────────────────────────────────────────────────────────────

        static void BuildVenue(Transform root, MaterialLibrary lib, FieldArt art)
        {
            var venue = Child(root, "Venue");
            float z = -FieldSpec.TileThickness;
            var floor = Collider(venue, "VenueFloor", new Vector3(0f, Units.In(z) - 0.25f, 0f), new Vector3(30f, 0.5f, 30f), Quaternion.identity, lib.tilePhysics);
            if (art && art.venue) { Place(art.venue, venue); return; }
            Visual(venue, "VenueFloorVisual", new Vector3(0f, Units.In(z) - 0.01f, 0f), new Vector3(24f, 0.02f, 24f), Quaternion.identity, lib.venueFloor);
            // Distant low backdrop so the horizon is not empty.
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, 90f * i, 0f);
                var pos = rot * new Vector3(0f, 2f, 11f);
                Visual(venue, "Backdrop" + i, pos, new Vector3(22f, 4.5f, 0.2f), rot, lib.venueWall);
            }
            _ = floor;
        }

        // ── HIVE structure ──────────────────────────────────────────────────────────────────

        static void BuildHiveFrame(Transform root, MaterialLibrary lib, FieldArt art)
        {
            var frame = Child(root, "HiveFrame");
            float footY = FieldSpec.FrameDepth * 0.5f;
            float apexZ = FieldSpec.HivePivotHeight + 1f;
            float legLen = Mathf.Sqrt(footY * footY + apexZ * apexZ);
            float legAngle = Mathf.Atan2(footY, apexZ) * Mathf.Rad2Deg; // from vertical
            bool showVisual = !(art && art.hiveFrame);

            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * FieldSpec.FrameSideX;
                foreach (float sy in new[] { -1f, 1f })
                {
                    // Leg from foot (x, sy·footY, 0) to apex (x, 0, apexZ).
                    var mid = Units.Field(x, sy * footY * 0.5f, apexZ * 0.5f);
                    var rot = Quaternion.Euler(-sy * legAngle, 0f, 0f);
                    var leg = Collider(frame, $"Leg_{sx}_{sy}", mid, Units.Size(1f, 1.5f, legLen), rot, lib.structurePhysics);
                    if (showVisual) Visual(frame, leg.name + "_V", mid, Units.Size(1f, 1.5f, legLen), rot, lib.hiveFrame);
                }
                // Foot bar on the tiles.
                var foot = Units.Field(x, 0f, 0.15f);
                // Visual only: the bar is clamped into the foam and wheels roll over it.
                if (showVisual) Visual(frame, $"Foot_{sx}_V", foot, Units.Size(1f, 2f * footY + 2f, 0.3f), Quaternion.identity, lib.hiveFrame);
                // Logo panel high in the triangle, above any legal robot (29 in).
                var panel = Units.Field(x, 0f, 36f);
                Collider(frame, $"Panel_{sx}", panel, Units.Size(0.25f, 9f, 10f), Quaternion.identity, lib.structurePhysics);
                if (showVisual) Visual(frame, $"Panel_{sx}_V", panel, Units.Size(0.25f, 9f, 10f), Quaternion.identity, lib.wall);
            }
            var bar = Units.Field(0f, 0f, apexZ + 0.75f);
            Collider(frame, "Crossbar", bar, Units.Size(2f * FieldSpec.FrameSideX + 1f, 1.5f, 1.5f), Quaternion.identity, lib.structurePhysics);
            if (showVisual) Visual(frame, "Crossbar_V", bar, Units.Size(2f * FieldSpec.FrameSideX + 1f, 1.5f, 1.5f), Quaternion.identity, lib.hiveFrame);
            if (!showVisual) Place(art.hiveFrame, frame);
        }

        static Hive BuildHive(Transform parent, Alliance a, MaterialLibrary lib, FieldArt art)
        {
            var pivotPos = FieldSpec.HivePivot(a);
            var pivotParent = Child(parent, $"HiveMount_{a}");
            pivotParent.position = Units.Field(pivotPos.x, pivotPos.y, FieldSpec.HivePivotHeight);
            var tray = new GameObject($"Hive_{a}");
            tray.transform.SetParent(pivotParent, false);
            var body = tray.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var hive = tray.AddComponent<Hive>();
            hive.alliance = a;

            var mat = a == Alliance.Red ? lib.hiveRed : lib.hiveBlue;
            var phys = lib.structurePhysics;
            Transform t = tray.transform;
            GameObject artTray = a == Alliance.Red ? art?.hiveTrayRed : art?.hiveTrayBlue;

            foreach (int s in new[] { -1, 1 })
            {
                float vMid = s * (FieldSpec.CellInnerV + FieldSpec.CellOuterV) * 0.5f;
                float depth = FieldSpec.CellOuterV - FieldSpec.CellInnerV;
                float hw = FieldSpec.CellHalfWidth;
                const float th = 0.5f;

                LocalCollider(t, $"Floor{s}", new Vector3(0f, FieldSpec.CellFloorW - th * 0.5f, vMid), new Vector3(2f * hw + 2f * th, th, depth), Quaternion.identity, phys);
                float sideH = FieldSpec.CellEaveW - FieldSpec.CellFloorW;
                foreach (float sx in new[] { -1f, 1f })
                {
                    LocalCollider(t, $"Side{s}_{sx}", new Vector3(sx * (hw + th * 0.5f), FieldSpec.CellFloorW + sideH * 0.5f, vMid), new Vector3(th, sideH, depth), Quaternion.identity, phys);
                    float rise = FieldSpec.CellPeakW - FieldSpec.CellEaveW;
                    float len = Mathf.Sqrt(hw * hw + rise * rise);
                    float ang = Mathf.Atan2(rise, hw) * Mathf.Rad2Deg;
                    Vector2 mid = new Vector2(sx * hw * 0.5f, FieldSpec.CellEaveW + rise * 0.5f);
                    Vector2 n = new Vector2(sx * Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad));
                    mid += n * th * 0.5f;
                    LocalCollider(t, $"Roof{s}_{sx}", new Vector3(mid.x, mid.y, vMid), new Vector3(len + th, th, depth), Quaternion.Euler(0f, 0f, -sx * ang), phys);
                }
                float backH = FieldSpec.CellPeakW - FieldSpec.CellFloorW;
                LocalCollider(t, $"Back{s}", new Vector3(0f, FieldSpec.CellFloorW + backH * 0.5f, s * (FieldSpec.CellInnerV - th * 0.5f)), new Vector3(2f * hw, backH, th), Quaternion.identity, phys);

                if (!artTray)
                {
                    var poly = new[]
                    {
                        new Vector2(-hw, FieldSpec.CellFloorW), new Vector2(hw, FieldSpec.CellFloorW),
                        new Vector2(hw, FieldSpec.CellEaveW), new Vector2(0f, FieldSpec.CellPeakW), new Vector2(-hw, FieldSpec.CellEaveW),
                    };
                    for (int i = 0; i < poly.Length; i++) poly[i] *= Units.MetersPerInch;
                    var mesh = MeshUtil.OpenPrism(poly, Units.In(s * FieldSpec.CellInnerV), Units.In(s * FieldSpec.CellOuterV), $"Cell{a}{s}");
                    var cell = new GameObject($"CellShell{s}", typeof(MeshFilter), typeof(MeshRenderer));
                    cell.transform.SetParent(t, false);
                    cell.GetComponent<MeshFilter>().sharedMesh = mesh;
                    cell.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }
            // The connecting bar and damper block through the pivot.
            LocalCollider(t, "Bar", new Vector3(0f, -0.6f, 0f), new Vector3(3f, 1.8f, 2f * FieldSpec.CellInnerV), Quaternion.identity, phys);
            if (!artTray)
            {
                LocalVisual(t, "BarV", new Vector3(0f, -0.6f, 0f), new Vector3(3f, 1.8f, 2f * FieldSpec.CellInnerV), Quaternion.identity, lib.hiveFrame);
                LocalVisual(t, "Axle", new Vector3(0f, 0f, 0f), new Vector3(4f, 1f, 1f), Quaternion.identity, lib.robotMetal);
            }
            else Place(artTray, t);

            hive.ResetTo(FieldSpec.StagedUpCellSign(a));
            return hive;
        }

        // ── FLOWER ──────────────────────────────────────────────────────────────────────────

        static Flower BuildFlower(Transform parent, int index, MaterialLibrary lib, FieldArt art)
        {
            Vector2 c = FieldSpec.FlowerCenter(index), n = FieldSpec.FlowerInward(index);
            var go = new GameObject($"Flower_F{index + 1}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Units.Field(c.x, c.y, 0f), Quaternion.LookRotation(new Vector3(n.x, 0f, n.y)));
            var f = go.AddComponent<Flower>();
            f.index = index; f.centerIn = c; f.inward = n;
            Transform t = go.transform;
            var phys = lib.structurePhysics;
            bool visual = !(art && art.flower);

            float back = -FieldSpec.FlowerBoreFromWall;
            float front = back + FieldSpec.FlowerPlateDeep;
            float halfAlong = FieldSpec.FlowerPlateAlong * 0.5f;

            RingPlate(t, "TopRing", FieldSpec.FlowerTopBore * 0.5f, FieldSpec.FlowerTopRingBottom, FieldSpec.FlowerTopRingTop, halfAlong, back, front, phys, visual ? lib.flowerRing : null);
            RingPlate(t, "MidRing", FieldSpec.FlowerMidBore * 0.5f, FieldSpec.FlowerMidRingBottom, FieldSpec.FlowerMidRingTop, halfAlong, back, front, phys, visual ? lib.flowerRing : null);
            RingPlate(t, "LowerRing", FieldSpec.FlowerLowerBore * 0.5f, 0f, FieldSpec.FlowerLowerRingTop, halfAlong, back, front, phys, visual ? lib.flowerRing : null);

            // Four HIPS pipes between the middle and top rings form the cage.
            const float pipeR = 0.42f;
            float pipeDist = FieldSpec.FlowerTopBore * 0.5f + pipeR;
            float y0 = FieldSpec.FlowerMidRingTop, y1 = FieldSpec.FlowerTopRingBottom;
            for (int i = 0; i < 4; i++)
            {
                float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(ang) * pipeDist, (y0 + y1) * 0.5f, Mathf.Sin(ang) * pipeDist);
                var pipe = new GameObject($"Pipe{i}");
                pipe.transform.SetParent(t, false);
                pipe.transform.localPosition = p * Units.MetersPerInch;
                var cap = pipe.AddComponent<CapsuleCollider>();
                cap.radius = Units.In(pipeR);
                cap.height = Units.In(y1 - y0);
                cap.sharedMaterial = phys;
                if (visual) LocalVisual(t, $"PipeV{i}", p, new Vector3(2f * pipeR, (y1 - y0) * 0.5f, 2f * pipeR), Quaternion.identity, lib.flower, Cylinder);
            }

            // Square extrusion on the wall side joins the lower and middle rings, and the bracket over the wall.
            LocalCollider(t, "Spine", new Vector3(0f, FieldSpec.FlowerTopRingTop * 0.5f, back + 0.25f), new Vector3(1f, FieldSpec.FlowerTopRingTop, 0.5f), Quaternion.identity, phys);
            LocalCollider(t, "Backstop", new Vector3(0f, FieldSpec.FlowerTopRingTop + FieldSpec.FlowerBackstopHeight * 0.5f, back + 0.2f), new Vector3(FieldSpec.FlowerPlateAlong, FieldSpec.FlowerBackstopHeight, 0.4f), Quaternion.identity, phys);
            if (visual)
            {
                LocalVisual(t, "SpineV", new Vector3(0f, FieldSpec.FlowerTopRingTop * 0.5f, back + 0.25f), new Vector3(1f, FieldSpec.FlowerTopRingTop, 0.5f), Quaternion.identity, lib.wallFrame);
                LocalVisual(t, "BackstopV", new Vector3(0f, FieldSpec.FlowerTopRingTop + FieldSpec.FlowerBackstopHeight * 0.5f, back + 0.2f), new Vector3(FieldSpec.FlowerPlateAlong, FieldSpec.FlowerBackstopHeight, 0.4f), Quaternion.identity, lib.flowerRing);
                LocalVisual(t, "Bracket", new Vector3(0f, FieldSpec.WallHeight + 0.4f, back - 0.9f), new Vector3(2f, 0.8f, 2.6f), Quaternion.identity, lib.wallFrame);
            }
            else Place(art.flower, t);
            return f;
        }

        /// <summary>A rectangular plate with a square hole of the bore's diameter, as four boxes.</summary>
        static void RingPlate(Transform t, string name, float holeR, float y0, float y1, float halfAlong, float back, float front, PhysicsMaterial phys, Material mat)
        {
            float h = y1 - y0, yc = (y0 + y1) * 0.5f;
            void Part(string n, float x0, float x1, float z0, float z1)
            {
                var c = new Vector3((x0 + x1) * 0.5f, yc, (z0 + z1) * 0.5f);
                var s = new Vector3(x1 - x0, h, z1 - z0);
                LocalCollider(t, name + n, c, s, Quaternion.identity, phys);
                if (mat) LocalVisual(t, name + n + "V", c, s, Quaternion.identity, mat);
            }
            Part("L", -halfAlong, -holeR, back, front);
            Part("R", holeR, halfAlong, back, front);
            Part("B", -holeR, holeR, back, -holeR);
            Part("F", -holeR, holeR, holeR, front);
        }

        // ── Helpers (positions/sizes in metres unless the name says Local, which is inches) ──

        public static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Collider(Transform parent, string name, Vector3 pos, Vector3 size, Quaternion rot, PhysicsMaterial phys)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.sharedMaterial = phys;
            return go;
        }

        public static GameObject Visual(Transform parent, string name, Vector3 pos, Vector3 size, Quaternion rot, Material mat, Mesh mesh = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = mesh ? mesh : Cube;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void LocalCollider(Transform t, string name, Vector3 posIn, Vector3 sizeIn, Quaternion rot, PhysicsMaterial phys) =>
            Collider(t, name, posIn * Units.MetersPerInch, sizeIn * Units.MetersPerInch, rot, phys);

        static void LocalVisual(Transform t, string name, Vector3 posIn, Vector3 sizeIn, Quaternion rot, Material mat, Mesh mesh = null) =>
            Visual(t, name, posIn * Units.MetersPerInch, sizeIn * Units.MetersPerInch, rot, mat, mesh);

        static void Place(GameObject prefab, Transform parent)
        {
            if (!prefab) return;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            foreach (var c in go.GetComponentsInChildren<UnityEngine.Collider>()) Object.DestroyImmediate(c);
        }
    }
}
