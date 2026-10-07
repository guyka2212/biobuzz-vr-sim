using System;
using UnityEngine;

namespace VrFsim.Settings
{
    public enum DrivetrainType { Mecanum, Tank, Swerve, XDrive, Butterfly }

    /// <summary>How far the intake reaches past the chassis edge.</summary>
    public enum IntakeReach { Sloped, Vector, Triangle }

    /// <summary>Sweeper takes elements off the floor only; side rollers and ramp can also pull
    /// POLLEN out of a FLOWER's retrieval opening.</summary>
    public enum IntakeKind { Sweeper, SideRollers, Ramp }

    /// <summary>Which chassis edges carry intake rollers. Side = both sides, dsim's 'SIDES'.</summary>
    /// <summary>
    /// Which chassis edges carry intake rollers. Side = both sides (a double side intake, dsim's
    /// "SIDES"); FrontAndBack is the double front/back intake; Left and Right are single sides.
    /// (Values are stored as numbers, so new ones go at the end.)
    /// </summary>
    public enum IntakeMount { Front, Back, Side, FrontAndBack, Left, Right }

    public enum LauncherKind { Turret, DoubleTurret, Dumper }

    /// <summary>The 3×3 mounting grid on the chassis top.</summary>
    public enum MountPos { FrontLeft, Front, FrontRight, Left, Center, Right, BackLeft, Back, BackRight }

    /// <summary>Livery drawn on the deck in the accent colour.</summary>
    public enum Decal { None, Stripe, Chevron, Racing, Hazard, Checker }

    /// <summary>Frame around the team-number sign placards.</summary>
    public enum PlateStyle { Classic, Bold, Rounded }

    /// <summary>
    /// A robot build. All lengths in inches, mass in pounds, matching how FTC teams talk about
    /// robots. <see cref="Validate"/> clamps everything to legal, buildable values.
    /// </summary>
    [Serializable]
    public class RobotConfig
    {
        public string name = "My Robot";
        public string teamName = "";
        public int teamNumber = 0;

        [Header("Chassis")]
        public DrivetrainType drivetrain = DrivetrainType.Mecanum;
        public float lengthIn = 16f;
        public float widthIn = 16f;
        public float heightIn = 16f;       // deployed height (R105 caps at 29; start cube 18)
        public float stowHeightIn = 14f;   // height in the starting configuration
        public float massLb = 26f;
        public float driveRpm = 435f;      // wheel RPM after the gearbox
        public float butterflyTractionRpm = 312f; // butterfly in traction (tank) mode

        [Header("Intake")]
        public IntakeReach intakeReach = IntakeReach.Sloped;
        public IntakeKind intakeKind = IntakeKind.Sweeper;
        public IntakeMount intakeMount = IntakeMount.Front;

        [Header("Launcher")]
        public LauncherKind launcher = LauncherKind.Turret;
        public MountPos launcherMount = MountPos.Center;
        public MountPos launcherMount2 = MountPos.Back; // NECTAR turret of a double turret
        public float hoodDeg = 75f;                     // dumper launch elevation

        [Header("Box Tube (FLOWER placer)")]
        public bool hasBoxTube = false;
        public MountPos boxTubeMount = MountPos.Front;

        [Header("Storage and passing")]
        public int storage = 4;
        public Vector2 passTargetIn = new Vector2(-36f, -36f); // field point, red frame

        [Header("Appearance")]
        public string chassisColor = "#2B2F36";
        public string accentColor = "#F2B705";
        /// <summary>Use the chassis colour for the accent too.</summary>
        public bool accentMatchesChassis = false;
        public Decal decal = Decal.None;
        public PlateStyle plate = PlateStyle.Classic;

        // ── Limits ────────────────────────────────────────────────────────────────────────
        public const float MinSize = 12f, MaxSize = 18f, SizeStep = 0.5f;
        public const float SwerveMinWidth = 13.5f;
        public const float MinHeight = 12f, MaxHeight = 18f;
        public const float HoodMin = 70f, HoodMax = 85f;
        public const int StorageMin = 1, StorageMax = 4;

        public static Vector2 MassRange(DrivetrainType d)
        {
            switch (d)
            {
                case DrivetrainType.Tank: return new Vector2(22f, 42f);
                case DrivetrainType.Swerve: return new Vector2(21.5f, 40f);
                case DrivetrainType.Butterfly: return new Vector2(24f, 42f);
                default: return new Vector2(18f, 42f);
            }
        }

        public static Vector2 RpmRange(DrivetrainType d)
        {
            switch (d)
            {
                case DrivetrainType.Tank: return new Vector2(200f, 560f);
                case DrivetrainType.Swerve: return new Vector2(200f, 500f);
                default: return new Vector2(200f, 600f);
            }
        }

        public static readonly Vector2 ButterflyTractionRpmRange = new Vector2(200f, 560f);

        /// <summary>Extra mass the mechanisms add on top of the bare drivetrain floor.</summary>
        public float MechanismMassLb()
        {
            float m = 1.5f * (IsDoubleIntake ? 2 : 1);
            m += launcher == LauncherKind.Turret ? 5f : launcher == LauncherKind.DoubleTurret ? 8.5f : 3.5f;
            if (hasBoxTube) m += 1.5f;
            return m;
        }

        public Vector2 LegalMassRange()
        {
            var r = MassRange(drivetrain);
            return new Vector2(Mathf.Min(r.x + MechanismMassLb() * 0.5f, r.y), r.y);
        }

        /// <summary>Rollers on two opposite edges.</summary>
        public bool IsDoubleIntake => intakeMount == IntakeMount.Side || intakeMount == IntakeMount.FrontAndBack;

        public bool CarriesNectar => launcher != LauncherKind.Turret || hasBoxTube;

        public void Validate()
        {
            name = string.IsNullOrWhiteSpace(name) ? "My Robot" : name.Trim();
            if (name.Length > 24) name = name.Substring(0, 24);
            teamName ??= "";
            if (teamName.Length > 32) teamName = teamName.Substring(0, 32);
            teamNumber = Mathf.Clamp(teamNumber, 0, 99999);
            if (!Enum.IsDefined(typeof(IntakeMount), intakeMount)) intakeMount = IntakeMount.Front;

            float minW = drivetrain == DrivetrainType.Swerve ? SwerveMinWidth : MinSize;
            lengthIn = Snap(Mathf.Clamp(lengthIn, MinSize, MaxSize));
            widthIn = Snap(Mathf.Clamp(widthIn, minW, MaxSize));
            heightIn = Mathf.Round(Mathf.Clamp(heightIn, MinHeight, MaxHeight));
            stowHeightIn = Mathf.Round(Mathf.Clamp(stowHeightIn, MinHeight, heightIn));

            var mr = LegalMassRange();
            massLb = Mathf.Round(Mathf.Clamp(massLb, mr.x, mr.y) * 2f) / 2f;
            var rr = RpmRange(drivetrain);
            driveRpm = Mathf.Round(Mathf.Clamp(driveRpm, rr.x, rr.y));
            butterflyTractionRpm = Mathf.Round(Mathf.Clamp(butterflyTractionRpm, ButterflyTractionRpmRange.x, ButterflyTractionRpmRange.y));

            hoodDeg = Mathf.Clamp(hoodDeg, HoodMin, HoodMax);
            storage = Mathf.Clamp(storage, StorageMin, StorageMax);

            // A dumper fires along a whole chassis edge, so it must sit on an edge cell.
            if (launcher == LauncherKind.Dumper) launcherMount = ToEdge(launcherMount);
            // Two turret rings in neighbouring cells overlap; the NECTAR turret goes at least two cells away.
            if (launcher == LauncherKind.DoubleTurret)
            {
                if (launcherMount == MountPos.Center) launcherMount = MountPos.Front;
                if (Adjacent(launcherMount, launcherMount2)) launcherMount2 = Opposite(launcherMount);
            }
            // The Box Tube must reach past an edge to place into a FLOWER, and cannot share a cell.
            if (hasBoxTube)
            {
                if (boxTubeMount == MountPos.Center || Clashes(boxTubeMount)) boxTubeMount = FirstFreeEdge();
            }

            passTargetIn = new Vector2(Mathf.Clamp(passTargetIn.x, -70f, 70f), Mathf.Clamp(passTargetIn.y, -70f, 70f));
            if (!ColorUtility.TryParseHtmlString(chassisColor, out _)) chassisColor = "#2B2F36";
            if (!ColorUtility.TryParseHtmlString(accentColor, out _)) accentColor = "#F2B705";
        }

        static float Snap(float v) => Mathf.Round(v / SizeStep) * SizeStep;

        public static Vector2Int Cell(MountPos p) => new Vector2Int((int)p % 3, (int)p / 3);

        public static bool Adjacent(MountPos a, MountPos b)
        {
            var ca = Cell(a); var cb = Cell(b);
            return Mathf.Abs(ca.x - cb.x) <= 1 && Mathf.Abs(ca.y - cb.y) <= 1;
        }

        public static MountPos Opposite(MountPos p) => (MountPos)(8 - (int)p);

        /// <summary>The four edge cells (a turretless dumper fires along a whole edge).</summary>
        public static bool IsEdge(MountPos p) => p == MountPos.Front || p == MountPos.Back || p == MountPos.Left || p == MountPos.Right;

        static MountPos ToEdge(MountPos p)
        {
            switch (p)
            {
                case MountPos.FrontLeft: case MountPos.FrontRight: case MountPos.Center: return MountPos.Front;
                case MountPos.BackLeft: case MountPos.BackRight: return MountPos.Back;
                default: return p;
            }
        }

        /// <summary>Can the Box Tube go in this cell without clashing with the launcher?</summary>
        public bool BoxTubeCellFree(MountPos p) => p != MountPos.Center && !Clashes(p);

        bool Clashes(MountPos p)
        {
            if (p == launcherMount) return true;
            if (launcher == LauncherKind.DoubleTurret && p == launcherMount2) return true;
            if (launcher == LauncherKind.Dumper)
            {
                // A dumper occupies its whole edge row/column.
                var e = Cell(launcherMount); var c = Cell(p);
                bool edgeRow = e.y != 1 && e.x == 1 && c.y == e.y;           // front/back edge
                bool edgeCol = e.x != 1 && e.y == 1 && c.x == e.x;           // left/right edge
                bool corner = e.x != 1 && e.y != 1 && (c.x == e.x || c.y == e.y);
                return edgeRow || edgeCol || corner;
            }
            return false;
        }

        MountPos FirstFreeEdge()
        {
            foreach (MountPos p in Enum.GetValues(typeof(MountPos)))
                if (p != MountPos.Center && !Clashes(p)) return p;
            hasBoxTube = false;
            return MountPos.Front;
        }

        public RobotConfig Clone() => JsonUtility.FromJson<RobotConfig>(JsonUtility.ToJson(this));

        public Color ChassisColor => ColorUtility.TryParseHtmlString(chassisColor, out var c) ? c : Color.gray;
        public Color AccentColor => accentMatchesChassis ? ChassisColor
            : ColorUtility.TryParseHtmlString(accentColor, out var c) ? c : Color.yellow;

        /// <summary>The robot paint colours (the same names dsim offers).</summary>
        public static readonly (string name, string hex)[] Palette =
        {
            ("Graphite", "#2B2F36"), ("White", "#EDEDED"), ("Silver", "#B8BCC2"), ("Black", "#141414"),
            ("Red", "#C8202A"), ("Orange", "#FF8A3D"), ("Yellow", "#F2C80F"), ("Green", "#3DBB5C"),
            ("Teal", "#1FB5A6"), ("Blue", "#2F6FE4"), ("Purple", "#8E5CE6"), ("Pink", "#F0609E"),
            ("Gold", "#D4A017"), ("Lime", "#9BE15D"), ("Magenta", "#D63AF9"), ("Cyan", "#2EC8F0"),
            ("Lavender", "#B9A7F2"), ("Ember", "#E2502A"),
        };
    }
}
