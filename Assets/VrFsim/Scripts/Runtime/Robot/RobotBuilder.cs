using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Settings;

namespace VrFsim.Robot
{
    /// <summary>Everything the robot logic needs to find on the built hierarchy.</summary>
    public class RobotRig
    {
        public GameObject root;
        public Rigidbody body;
        public Transform[] turrets = new Transform[0];   // yaw pivots (0 = POLLEN, 1 = NECTAR for a double turret)
        public Transform[] turretExits = new Transform[0];
        public Transform dumperExit;
        public Vector3 dumperDirLocal;
        public Transform boxTube, boxTubeTip;
        public Transform ramp;
        public readonly List<IntakeMouth> mouths = new List<IntakeMouth>();
        public readonly List<Transform> storageSlots = new List<Transform>();
        public readonly List<Transform> wheels = new List<Transform>();
        public readonly List<Transform> swerveModules = new List<Transform>();
        public Transform upperBody;
        public BoxCollider upperCollider;
    }

    /// <summary>An intake opening, in robot-local metres.</summary>
    public struct IntakeMouth
    {
        public Vector3 center, halfSize, outward;
    }

    public static class RobotBuilder
    {
        static Mesh cube, cylinder;
        static Mesh Cube => cube ? cube : cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        static Mesh Cylinder => cylinder ? cylinder : cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

        public const float ChassisHeightIn = 4f;
        /// <summary>The enclosed body runs from 0.6 in above the tiles up to the deck.</summary>
        public const float BodyBottomIn = 0.6f, BodyHeightIn = 6f, DeckIn = BodyBottomIn + BodyHeightIn;
        public const float WheelInsetIn = 2.6f;

        public static float TurretRadiusIn(RobotConfig c) => Mathf.Min(3.8f, 0.24f * Mathf.Min(c.lengthIn, c.widthIn));

        public static float IntakeReachIn(IntakeReach r) => r == IntakeReach.Vector ? 2f : r == IntakeReach.Triangle ? 4.5f : 3f;
        public static float IntakeWidthFrac(IntakeReach r) => r == IntakeReach.Triangle ? 0.6f : r == IntakeReach.Vector ? 1f : 0.95f;

        /// <summary>Robot-local position (inches, x right, z forward) of a 3×3 mount cell.</summary>
        public static Vector2 MountXZ(RobotConfig c, MountPos p, float marginIn)
        {
            var cell = RobotConfig.Cell(p);
            float hx = Mathf.Max(0f, c.widthIn * 0.5f - marginIn), hz = Mathf.Max(0f, c.lengthIn * 0.5f - marginIn);
            return new Vector2((cell.x - 1) * hx, (1 - cell.y) * hz);
        }

        /// <summary>Unit outward direction (x, z) of an edge or corner mount.</summary>
        public static Vector2 MountOutward(MountPos p)
        {
            var cell = RobotConfig.Cell(p);
            var d = new Vector2(cell.x - 1, 1 - cell.y);
            return d == Vector2.zero ? Vector2.up : d.normalized;
        }

        public static RobotRig Build(RobotConfig c, Alliance alliance, MaterialLibrary lib, RobotArt art, string name)
        {
            var rig = new RobotRig();
            var root = new GameObject(name);
            rig.root = root;
            float L = c.lengthIn, W = c.widthIn;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = Units.Lb(c.massLb);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.maxAngularVelocity = DriveParams.MaxOmega * 1.5f;
            rig.body = rb;

            // Chassis: full footprint, resting on the tiles (frictionless; wheels are modelled).
            var chassisCol = root.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, Units.In(ChassisHeightIn * 0.5f), 0f);
            chassisCol.size = new Vector3(Units.In(W), Units.In(ChassisHeightIn), Units.In(L));
            chassisCol.sharedMaterial = lib.robotPhysics;

            // Upper body (mechanisms): inset footprint, up to the current height.
            var upper = Child(root.transform, "UpperBody");
            rig.upperBody = upper;
            rig.upperCollider = upper.gameObject.AddComponent<BoxCollider>();
            rig.upperCollider.sharedMaterial = lib.robotPhysics;
            SetUpperHeight(rig, c, c.stowHeightIn);

            rb.centerOfMass = new Vector3(0f, Units.In(2.5f), 0f);
            float m = rb.mass, Lm = Units.In(L), Wm = Units.In(W), Hm = Units.In(c.heightIn);
            rb.inertiaTensor = new Vector3(m * (Lm * Lm + Hm * Hm) / 12f, m * (Lm * Lm + Wm * Wm) / 12f, m * (Wm * Wm + Hm * Hm) / 12f);
            rb.inertiaTensorRotation = Quaternion.identity;

            var vis = Child(root.transform, "Visual");
            var chassisMat = Tinted(lib.robotDark, c.ChassisColor);
            var accentMat = Tinted(lib.robotMetal, c.AccentColor);

            if (art && art.body)
            {
                // Closed body: pocketed side plates (team colour), deck (aluminium), electronics (black).
                Part(vis, "Body", new Vector3(0f, BodyBottomIn + BodyHeightIn * 0.5f, 0f), new Vector3(W, BodyHeightIn, L),
                    chassisMat, art.body, lib.robotMetal, lib.robotWheel);
            }
            else
            {
                Part(vis, "Body", new Vector3(0f, BodyBottomIn + BodyHeightIn * 0.5f, 0f), new Vector3(W - 0.2f, BodyHeightIn, L - 0.2f), chassisMat);
                Part(vis, "Deck", new Vector3(0f, DeckIn - 0.1f, 0f), new Vector3(W, 0.2f, L), lib.robotMetal);
            }

            BuildWheels(rig, vis, c, lib, art);
            BuildIntake(rig, vis, c, lib, art, accentMat);
            BuildStorage(rig, root.transform, c);
            BuildLauncher(rig, vis, c, lib, art, accentMat);
            if (c.hasBoxTube) BuildBoxTube(rig, vis, c, lib, art, accentMat);
            BuildDecal(vis, c, accentMat);
            BuildSigns(vis, c, alliance, lib);
            return rig;
        }

        public static void SetUpperHeight(RobotRig rig, RobotConfig c, float heightIn)
        {
            float bottom = ChassisHeightIn;
            float h = Mathf.Max(1f, heightIn - bottom);
            rig.upperCollider.center = new Vector3(0f, Units.In(bottom + h * 0.5f), 0f);
            rig.upperCollider.size = new Vector3(Units.In(c.widthIn * 0.8f), Units.In(h), Units.In(c.lengthIn * 0.8f));
        }

        // ── Parts ───────────────────────────────────────────────────────────────────────────

        static void BuildWheels(RobotRig rig, Transform vis, RobotConfig c, MaterialLibrary lib, RobotArt art)
        {
            float wx = c.widthIn * 0.5f - WheelInsetIn * 0.6f, wz = c.lengthIn * 0.5f - WheelInsetIn;
            float d = DriveParams.WheelDiameter / Units.MetersPerInch;
            Mesh mesh = null;
            switch (c.drivetrain)
            {
                case DrivetrainType.Mecanum: case DrivetrainType.Butterfly: mesh = art ? art.mecanumWheel : null; break;
                case DrivetrainType.XDrive: mesh = art ? art.omniWheel : null; break;
                default: mesh = art ? art.tractionWheel : null; break;
            }
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = (i & 2) == 0 ? 1f : -1f;
                var pos = new Vector3(sx * wx, d * 0.5f, sz * wz);
                Transform holder = vis;
                if (c.drivetrain == DrivetrainType.Swerve)
                {
                    var module = Child(vis, "SwerveModule" + i);
                    module.localPosition = pos * Units.MetersPerInch;
                    rig.swerveModules.Add(module);
                    Part(module, "Housing", new Vector3(0f, d * 0.5f + 0.6f, 0f), new Vector3(3f, 1.2f, 3f), lib.robotMetal, art ? art.swerveModule : null);
                    holder = module;
                    pos = new Vector3(0f, 0f, 0f);
                }
                var axle = Child(holder, "Wheel" + i);
                axle.localPosition = pos * Units.MetersPerInch;
                if (c.drivetrain == DrivetrainType.XDrive) axle.localRotation = Quaternion.Euler(0f, sx * sz * 45f, 0f);
                var tire = Part(axle, "Tire", Vector3.zero, new Vector3(d, 1.5f, d), lib.robotWheel, mesh ? mesh : Cylinder);
                if (!mesh) tire.localRotation = Quaternion.Euler(0f, 0f, 90f); // cylinder axis Y -> X
                else
                {
                    // Art wheels keep true proportions. Mecanum wheels come in two hands; mirroring
                    // alternate corners gives the X roller pattern seen from above.
                    float hand = c.drivetrain == DrivetrainType.Mecanum || c.drivetrain == DrivetrainType.Butterfly ? sx * sz : 1f;
                    tire.localScale = new Vector3(hand, 1f, 1f) * Units.In(d);
                }
                rig.wheels.Add(axle);
            }
            if (c.drivetrain == DrivetrainType.Tank)
            {
                // Middle drop-centre wheels and a tread-like side skirt.
                foreach (float sx in new[] { -1f, 1f })
                    Part(vis, "Skirt" + sx, new Vector3(sx * wx, d * 0.5f, 0f), new Vector3(1.6f, d * 0.9f, c.lengthIn - 2f), lib.robotWheel);
            }
        }

        static void BuildIntake(RobotRig rig, Transform vis, RobotConfig c, MaterialLibrary lib, RobotArt art, Material accent)
        {
            float reach = IntakeReachIn(c.intakeReach);
            float rollerZ = 2.6f;
            void Mouth(Vector2 outward)
            {
                var o3 = new Vector3(outward.x, 0f, outward.y);
                bool sideways = Mathf.Abs(outward.x) > 0.5f;
                float edge = sideways ? c.widthIn * 0.5f : c.lengthIn * 0.5f;
                float across = (sideways ? c.lengthIn : c.widthIn) * IntakeWidthFrac(c.intakeReach) * 0.5f;
                var center = o3 * (edge + reach * 0.5f - 0.5f) + Vector3.up * 2.5f;
                var half = sideways ? new Vector3(reach * 0.5f + 1.5f, 3f, across) : new Vector3(across, 3f, reach * 0.5f + 1.5f);
                rig.mouths.Add(new IntakeMouth { center = center * Units.MetersPerInch, halfSize = half * Units.MetersPerInch, outward = o3 });

                var side = Quaternion.LookRotation(o3) * Vector3.right;
                var rollerPos = o3 * (edge + reach - 1f) + Vector3.up * rollerZ;
                bool hasArt = art && art.intakeRoller;
                var roller = Part(vis, "IntakeRoller", rollerPos, hasArt ? new Vector3(across * 2f, 2f, 2f) : new Vector3(2f, across * 2f, 2f),
                    accent, hasArt ? art.intakeRoller : Cylinder);
                roller.localRotation = Quaternion.FromToRotation(hasArt ? Vector3.right : Vector3.up, side);

                foreach (float s in new[] { -1f, 1f })
                {
                    var platePos = o3 * (edge + reach * 0.5f - 0.5f) + side * s * (across + 0.25f) + Vector3.up * 2.5f;
                    var plate = art && art.intakePlate
                        ? Part(vis, "IntakePlate", platePos, new Vector3(0.25f, 4f, reach + 1f), accent, art.intakePlate, lib.robotMetal, lib.robotWheel)
                        : Part(vis, "IntakePlate", platePos, new Vector3(0.25f, 4f, reach + 1f), lib.robotMetal);
                    plate.localRotation = Quaternion.LookRotation(o3);
                }
            }
            switch (c.intakeMount)
            {
                case IntakeMount.Front: Mouth(Vector2.up); break;
                case IntakeMount.Back: Mouth(Vector2.down); break;
                case IntakeMount.Side: Mouth(Vector2.right); Mouth(Vector2.left); break;
                case IntakeMount.FrontAndBack: Mouth(Vector2.up); Mouth(Vector2.down); break;
                case IntakeMount.Left: Mouth(Vector2.left); break;
                case IntakeMount.Right: Mouth(Vector2.right); break;
            }
            if (c.intakeKind == IntakeKind.Ramp)
            {
                var m = rig.mouths[0];
                var ramp = Child(vis, "Ramp");
                ramp.localPosition = (m.center / Units.MetersPerInch + m.outward * 1f + Vector3.up * 1f) * Units.MetersPerInch;
                ramp.localRotation = Quaternion.LookRotation(m.outward);
                Part(ramp, "RampFrame", new Vector3(0f, 2f, 0f), new Vector3(c.widthIn * 0.5f, 4f, 0.3f), lib.robotMetal);
                rig.ramp = ramp;
            }
            if (c.intakeKind == IntakeKind.SideRollers)
            {
                var m = rig.mouths[0];
                var side = Quaternion.LookRotation(m.outward) * Vector3.right;
                foreach (float s in new[] { -1f, 1f })
                {
                    var p = m.center / Units.MetersPerInch + m.outward * 1.5f + side * s * 1.9f + Vector3.down * 1.2f;
                    Part(vis, "SideRoller", p, new Vector3(1.6f, 1f, 1.6f), accent, Cylinder);
                }
            }
        }

        static void BuildStorage(RobotRig rig, Transform root, RobotConfig c)
        {
            var store = Child(root, "Storage");
            for (int i = 0; i < c.storage; i++)
            {
                var slot = Child(store, "Slot" + i);
                float x = (i % 2 == 0 ? -1f : 1f) * 1.9f, z = (i < 2 ? 1f : -1f) * 1.9f;
                // Inside the closed body, visible through the deck opening.
                slot.localPosition = new Vector3(x, BodyBottomIn + 2.2f, z) * Units.MetersPerInch;
                rig.storageSlots.Add(slot);
            }
        }

        static void BuildLauncher(RobotRig rig, Transform vis, RobotConfig c, MaterialLibrary lib, RobotArt art, Material accent)
        {
            if (c.launcher == LauncherKind.Dumper)
            {
                var dir = MountOutward(c.launcherMount);
                var pos = MountXZ(c, c.launcherMount, 2.5f);
                var o3 = new Vector3(dir.x, 0f, dir.y);
                var bucket = Child(vis, "Dumper");
                bucket.localPosition = new Vector3(pos.x, DeckIn + 2f, pos.y) * Units.MetersPerInch;
                bucket.localRotation = Quaternion.LookRotation(o3);
                float span = Mathf.Abs(dir.x) > 0.5f ? c.lengthIn * 0.8f : c.widthIn * 0.8f;
                if (art && art.dumper) Part(bucket, "Bucket", Vector3.zero, new Vector3(span, 4f, 5f), accent, art.dumper, lib.robotMetal, lib.robotWheel);
                else Part(bucket, "Bucket", Vector3.zero, new Vector3(span, 4f, 5f), accent);
                if (!(art && art.dumper)) Part(bucket, "Hood", new Vector3(0f, 2.3f, 1.5f), new Vector3(span, 0.4f, 3f), lib.robotMetal);
                var exit = Child(bucket, "Exit");
                exit.localPosition = new Vector3(0f, 3f, 3f) * Units.MetersPerInch;
                rig.dumperExit = exit;
                rig.dumperDirLocal = o3;
                return;
            }
            int count = c.launcher == LauncherKind.DoubleTurret ? 2 : 1;
            rig.turrets = new Transform[count];
            rig.turretExits = new Transform[count];
            float r = TurretRadiusIn(c);
            for (int i = 0; i < count; i++)
            {
                var mount = i == 0 ? c.launcherMount : c.launcherMount2;
                var pos = MountXZ(c, mount, r + 0.5f);
                bool turretArt = art && art.turret;
                var pivot = Child(vis, "Turret" + i);
                pivot.localPosition = new Vector3(pos.x, DeckIn, pos.y) * Units.MetersPerInch;
                float dia = Mathf.Clamp(2f * r + 1f, 5f, 9f);
                if (turretArt)
                {
                    Part(pivot, "Launcher", Vector3.zero, Vector3.one * dia, accent, art.turret, lib.robotMetal, lib.robotWheel);
                }
                else
                {
                    Part(vis, "TurretRing" + i, new Vector3(pos.x, DeckIn + 0.2f, pos.y), new Vector3(2f * r, 0.4f, 2f * r), lib.robotMetal, Cylinder);
                    Part(pivot, "Launcher", new Vector3(0f, 2.5f, 0f), new Vector3(r * 1.3f, 4f, r * 1.6f), accent);
                    Part(pivot, "Flywheel", new Vector3(0f, 3.9f, r * 0.6f), new Vector3(2.6f, 0.8f, 2.6f), lib.robotDark, Cylinder);
                }
                var exit = Child(pivot, "Exit");
                // Where the ball leaves the hood: up and in front of the flywheels.
                exit.localPosition = new Vector3(0f, 0.62f * dia, 0.55f * dia) * Units.MetersPerInch;
                rig.turrets[i] = pivot;
                rig.turretExits[i] = exit;
            }
        }

        static void BuildBoxTube(RobotRig rig, Transform vis, RobotConfig c, MaterialLibrary lib, RobotArt art, Material accent)
        {
            var pos = MountXZ(c, c.boxTubeMount, 1.2f);
            var dir = MountOutward(c.boxTubeMount);
            var tube = Child(vis, "BoxTube");
            tube.localPosition = new Vector3(pos.x, DeckIn, pos.y) * Units.MetersPerInch;
            tube.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            float h = Mathf.Max(3f, c.stowHeightIn - DeckIn);
            if (art && art.boxTube) Part(tube, "Tube", new Vector3(0f, h * 0.5f, 0f), new Vector3(1.5f, h, 1.5f), lib.robotMetal, art.boxTube, lib.robotMetal, lib.robotWheel);
            else Part(tube, "Tube", new Vector3(0f, h * 0.5f, 0f), new Vector3(1.5f, h, 1.5f), lib.robotMetal);
            var tip = Child(tube, "Tip");
            tip.localPosition = new Vector3(0f, h, 0f) * Units.MetersPerInch;
            if (art && art.placerCup) Part(tip, "Cup", new Vector3(0f, 0.6f, 1.6f), new Vector3(4.2f, 1.2f, 4.2f), accent, art.placerCup, lib.robotMetal, lib.robotWheel);
            else Part(tip, "Cup", new Vector3(0f, 0f, 1.5f), new Vector3(4.2f, 1.2f, 4.2f), accent);
            rig.boxTube = tube;
            rig.boxTubeTip = tip;
        }

        /// <summary>Livery on the front and rear deck (the centre of the deck is open over storage).</summary>
        static void BuildDecal(Transform vis, RobotConfig c, Material accent)
        {
            if (c.decal == Decal.None) return;
            float L = c.lengthIn, W = c.widthIn, y = DeckIn + 0.04f;
            float z0 = L * 0.19f, z1 = L * 0.47f, zm = (z0 + z1) * 0.5f, zl = z1 - z0;
            var d = Child(vis, "Decal");
            void Bar(Vector3 center, float w, float len, float yaw = 0f)
            {
                var t = Part(d, "Bar", center, new Vector3(w, 0.06f, len), accent);
                t.localRotation = Quaternion.Euler(0f, yaw, 0f);
                t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (float s in new[] { 1f, -1f })
            {
                switch (c.decal)
                {
                    case Decal.Stripe:
                        Bar(new Vector3(0f, y, s * zm), 2f, zl);
                        break;
                    case Decal.Racing:
                        Bar(new Vector3(-1.6f, y, s * zm), 1.1f, zl);
                        Bar(new Vector3(1.6f, y, s * zm), 1.1f, zl);
                        break;
                    case Decal.Chevron:
                        if (s < 0) break;   // one chevron, pointing forward
                        float arm = Mathf.Min(W * 0.32f, zl * 1.1f);
                        Bar(new Vector3(-arm * 0.33f, y, zm), 1.3f, arm, 35f);
                        Bar(new Vector3(arm * 0.33f, y, zm), 1.3f, arm, -35f);
                        break;
                    case Decal.Hazard:
                        for (int i = -2; i <= 2; i++)
                            Bar(new Vector3(i * W * 0.17f, y, s * zm), 1f, zl * 1.2f, 40f);
                        break;
                    case Decal.Checker:
                        float sq = Mathf.Min(2f, zl / 2.2f);
                        for (int ix = -3; ix <= 3; ix++)
                            for (int iz = 0; iz < 2; iz++)
                                if (((ix + iz) & 1) == 0)
                                    Bar(new Vector3(ix * sq, y, s * (z0 + sq * (iz + 0.5f) + 0.2f)), sq, sq);
                        break;
                }
            }
        }

        static void BuildSigns(Transform vis, RobotConfig c, Alliance a, MaterialLibrary lib)
        {
            var mat = a == Alliance.Red ? lib.bumperRed : lib.bumperBlue;
            string text = c.teamNumber > 0 ? c.teamNumber.ToString() : c.name;
            foreach (float s in new[] { -1f, 1f })
            {
                var pos = new Vector3(s * (c.widthIn * 0.5f + 0.15f), BodyBottomIn + BodyHeightIn * 0.55f, 0f);
                float len = Mathf.Min(10f, c.lengthIn - 2f);
                var plate = Part(vis, "Sign" + s, pos, new Vector3(0.25f, 3f, len), mat);
                if (c.plate != PlateStyle.Classic)
                {
                    // A frame around the placard: square and thick (bold) or thin with round corners.
                    bool bold = c.plate == PlateStyle.Bold;
                    float f = bold ? 0.45f : 0.25f, x = pos.x + s * 0.05f;
                    var frameMat = bold ? lib.robotWheel : lib.robotMetal;
                    Part(vis, "PlateTop", new Vector3(x, pos.y + 1.5f + f * 0.5f, 0f), new Vector3(0.3f, f, len + 2f * f), frameMat);
                    Part(vis, "PlateBottom", new Vector3(x, pos.y - 1.5f - f * 0.5f, 0f), new Vector3(0.3f, f, len + 2f * f), frameMat);
                    Part(vis, "PlateFront", new Vector3(x, pos.y, len * 0.5f + f * 0.5f), new Vector3(0.3f, 3f, f), frameMat);
                    Part(vis, "PlateBack", new Vector3(x, pos.y, -len * 0.5f - f * 0.5f), new Vector3(0.3f, 3f, f), frameMat);
                    if (!bold)
                        foreach (float cy in new[] { -1f, 1f })
                            foreach (float cz in new[] { -1f, 1f })
                            {
                                var corner = Part(vis, "PlateCorner", new Vector3(x, pos.y + cy * 1.6f, cz * (len * 0.5f + 0.1f)), new Vector3(0.6f, 0.3f, 0.6f), frameMat, Cylinder);
                                corner.localRotation = Quaternion.Euler(0f, 0f, 90f);
                            }
                }
                var label = new GameObject("Number", typeof(TextMeshPro));
                label.transform.SetParent(vis, false);
                label.transform.localPosition = (pos + Vector3.right * s * 0.2f) * Units.MetersPerInch;
                label.transform.localRotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f);
                var tmp = label.GetComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = 0.5f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.rectTransform.sizeDelta = new Vector2(Units.In(10f), Units.In(3f));
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = 0.6f;
                _ = plate;
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────────

        static readonly Dictionary<(Material, Color), Material> tintCache = new Dictionary<(Material, Color), Material>();

        /// <summary>One material per (base, colour), shared by every robot with that colour.</summary>
        static Material Tinted(Material baseMat, Color c)
        {
            if (!baseMat) return null;
            if (tintCache.TryGetValue((baseMat, c), out var m) && m) return m;
            m = new Material(baseMat) { name = baseMat.name + "_" + ColorUtility.ToHtmlStringRGB(c) };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            tintCache[(baseMat, c)] = m;
            return m;
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>A visual-only part; position and size in inches.</summary>
        /// <summary>A multi-material part: slot 0 = <paramref name="mat"/>, then the extra slots.</summary>
        static Transform Part(Transform parent, string name, Vector3 posIn, Vector3 sizeIn, Material mat, Mesh mesh, Material slot1, Material slot2)
        {
            var t = Part(parent, name, posIn, sizeIn, mat, mesh);
            int n = mesh ? Mathf.Max(1, mesh.subMeshCount) : 1;
            var slots = new[] { mat, slot1, slot2 };
            var used = new Material[n];
            for (int i = 0; i < n; i++) used[i] = slots[Mathf.Min(i, slots.Length - 1)];
            t.GetComponent<MeshRenderer>().sharedMaterials = used;
            return t;
        }

        static Transform Part(Transform parent, string name, Vector3 posIn, Vector3 sizeIn, Material mat, Mesh mesh = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = posIn * Units.MetersPerInch;
            go.transform.localScale = sizeIn * Units.MetersPerInch;
            if (mesh == Cylinder) go.transform.localScale = new Vector3(sizeIn.x, sizeIn.y * 0.5f, sizeIn.z) * Units.MetersPerInch;
            go.GetComponent<MeshFilter>().sharedMesh = mesh ? mesh : Cube;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }
    }
}
