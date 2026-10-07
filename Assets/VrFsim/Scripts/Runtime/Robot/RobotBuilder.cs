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

            if (art && art.chassis)
            {
                // Blender chassis: slot 0 plate (team colour), 1 aluminium channel, 2 electronics.
                var ch = Part(vis, "Chassis", new Vector3(0f, ChassisHeightIn * 0.5f, 0f), new Vector3(W, ChassisHeightIn, L), chassisMat, art.chassis);
                ch.GetComponent<MeshRenderer>().sharedMaterials = new[] { chassisMat, lib.robotMetal, lib.robotWheel };
            }
            else
            {
                Part(vis, "ChassisPlate", new Vector3(0f, 1.6f, 0f), new Vector3(W - 1f, 0.25f, L - 1f), chassisMat);
                Part(vis, "RailL", new Vector3(-W * 0.5f + 0.75f, 2.2f, 0f), new Vector3(1.5f, 3.2f, L), chassisMat);
                Part(vis, "RailR", new Vector3(W * 0.5f - 0.75f, 2.2f, 0f), new Vector3(1.5f, 3.2f, L), chassisMat);
                Part(vis, "RailF", new Vector3(0f, 3.4f, L * 0.5f - 0.5f), new Vector3(W - 3f, 1f, 1f), chassisMat);
                Part(vis, "RailB", new Vector3(0f, 3.4f, -L * 0.5f + 0.5f), new Vector3(W - 3f, 1f, 1f), chassisMat);
            }

            BuildWheels(rig, vis, c, lib, art);
            BuildIntake(rig, vis, c, lib, art, accentMat);
            BuildStorage(rig, root.transform, c);
            BuildLauncher(rig, vis, c, lib, art, accentMat);
            if (c.hasBoxTube) BuildBoxTube(rig, vis, c, lib, art, accentMat);
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
                    Part(vis, "IntakePlate", o3 * (edge + reach * 0.5f - 0.5f) + side * s * (across + 0.25f) + Vector3.up * 2.5f,
                        sideways ? new Vector3(reach + 1f, 4f, 0.25f) : new Vector3(0.25f, 4f, reach + 1f), lib.robotMetal);
            }
            switch (c.intakeMount)
            {
                case IntakeMount.Front: Mouth(Vector2.up); break;
                case IntakeMount.Back: Mouth(Vector2.down); break;
                case IntakeMount.Side: Mouth(Vector2.right); break;
                case IntakeMount.FrontAndBack: Mouth(Vector2.up); Mouth(Vector2.down); break;
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
                slot.localPosition = new Vector3(x, ChassisHeightIn + 2.4f, z) * Units.MetersPerInch;
                rig.storageSlots.Add(slot);
            }
        }

        static void BuildLauncher(RobotRig rig, Transform vis, RobotConfig c, MaterialLibrary lib, RobotArt art, Material accent)
        {
            float topY = c.stowHeightIn;
            if (c.launcher == LauncherKind.Dumper)
            {
                var dir = MountOutward(c.launcherMount);
                var pos = MountXZ(c, c.launcherMount, 2.5f);
                var o3 = new Vector3(dir.x, 0f, dir.y);
                var bucket = Child(vis, "Dumper");
                bucket.localPosition = new Vector3(pos.x, topY - 3f, pos.y) * Units.MetersPerInch;
                bucket.localRotation = Quaternion.LookRotation(o3);
                float span = Mathf.Abs(dir.x) > 0.5f ? c.lengthIn * 0.8f : c.widthIn * 0.8f;
                Part(bucket, "Bucket", new Vector3(0f, 0f, 0f), new Vector3(span, 4f, 5f), accent, art ? art.dumper : null);
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
                var ringY = topY - 4.5f;
                Part(vis, "TurretRing" + i, new Vector3(pos.x, ringY, pos.y), new Vector3(2f * r, 0.4f, 2f * r), lib.robotMetal, Cylinder);
                var pivot = Child(vis, "Turret" + i);
                pivot.localPosition = new Vector3(pos.x, ringY + 0.5f, pos.y) * Units.MetersPerInch;
                bool turretArt = art && art.turret;
                var body = Part(pivot, "Launcher", new Vector3(0f, 2f, 0f), new Vector3(r * 1.3f, 4f, r * 1.6f), accent, turretArt ? art.turret : null);
                if (!turretArt)
                {
                    Part(pivot, "Flywheel", new Vector3(0f, 3.4f, r * 0.6f), new Vector3(2.6f, 0.8f, 2.6f), lib.robotDark, Cylinder);
                    Part(pivot, "Hood", new Vector3(0f, 4.1f, 0f), new Vector3(r * 1.1f, 0.3f, r * 1.8f), lib.robotMetal);
                }
                var exit = Child(pivot, "Exit");
                exit.localPosition = new Vector3(0f, 4.6f, r * 0.9f) * Units.MetersPerInch;
                rig.turrets[i] = pivot;
                rig.turretExits[i] = exit;
                _ = body;
            }
        }

        static void BuildBoxTube(RobotRig rig, Transform vis, RobotConfig c, MaterialLibrary lib, RobotArt art, Material accent)
        {
            var pos = MountXZ(c, c.boxTubeMount, 1.2f);
            var dir = MountOutward(c.boxTubeMount);
            var tube = Child(vis, "BoxTube");
            tube.localPosition = new Vector3(pos.x, ChassisHeightIn, pos.y) * Units.MetersPerInch;
            tube.localRotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y));
            float h = c.stowHeightIn - ChassisHeightIn;
            Part(tube, "Tube", new Vector3(0f, h * 0.5f, 0f), new Vector3(1.5f, h, 1.5f), lib.robotMetal, art ? art.boxTube : null);
            var tip = Child(tube, "Tip");
            tip.localPosition = new Vector3(0f, h, 0f) * Units.MetersPerInch;
            Part(tip, "Cup", new Vector3(0f, 0f, 1.5f), new Vector3(4.2f, 1.2f, 4.2f), accent);
            rig.boxTube = tube;
            rig.boxTubeTip = tip;
        }

        static void BuildSigns(Transform vis, RobotConfig c, Alliance a, MaterialLibrary lib)
        {
            var mat = a == Alliance.Red ? lib.bumperRed : lib.bumperBlue;
            string text = c.teamNumber > 0 ? c.teamNumber.ToString() : c.name;
            foreach (float s in new[] { -1f, 1f })
            {
                var pos = new Vector3(s * (c.widthIn * 0.5f + 0.15f), 5.5f, 0f);
                var plate = Part(vis, "Sign" + s, pos, new Vector3(0.25f, 3f, Mathf.Min(10f, c.lengthIn - 2f)), mat);
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
