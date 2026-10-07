using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>Axis-aligned rectangle on the field plane, in inches.</summary>
    [System.Serializable]
    public struct FieldRect
    {
        public float xMin, xMax, yMin, yMax;

        public FieldRect(float xMin, float xMax, float yMin, float yMax)
        {
            this.xMin = xMin; this.xMax = xMax; this.yMin = yMin; this.yMax = yMax;
        }

        public Vector2 Center => new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        public Vector2 Size => new Vector2(xMax - xMin, yMax - yMin);

        /// <summary>Point symmetry about the field centre: red feature → blue feature.</summary>
        public FieldRect Rotate180() => new FieldRect(-xMax, -xMin, -yMax, -yMin);

        public FieldRect For(Alliance a) => a == Alliance.Red ? this : Rotate180();

        public bool Contains(Vector2 p) => p.x >= xMin && p.x <= xMax && p.y >= yMin && p.y <= yMax;

        /// <summary>True if a circle of radius r centred at p overlaps the rectangle at all.</summary>
        public bool OverlapsCircle(Vector2 p, float r)
        {
            float dx = Mathf.Max(xMin - p.x, 0f, p.x - xMax);
            float dy = Mathf.Max(yMin - p.y, 0f, p.y - yMax);
            return dx * dx + dy * dy <= r * r;
        }
    }

    /// <summary>
    /// BIOBUZZ field geometry, in inches, field frame (see <see cref="Units"/>).
    ///
    /// Sources: Competition Manual TU03 §9 and the Event Field Setup Guide V1.0. Where the manual
    /// gives a rounded "approximately" figure and FIRST's field CAD gives a precise one (tile pitch,
    /// zone edges, HIVE cells, FLOWER rings), the CAD figure is used. All red-side features are
    /// listed; blue's are the point-symmetric copies (<see cref="FieldRect.Rotate180"/>).
    /// </summary>
    public static class FieldSpec
    {
        // ── Perimeter and tiles ──────────────────────────────────────────────────────────────
        /// <summary>Half-span to the inside face of the perimeter walls (CAD: 141.348 in square).</summary>
        public const float WallInner = 70.674f;
        public const float WallHeight = 11.0f;
        public const float WallThickness = 1.0f;
        public const float TilePitch = 23.528f;
        public const float TileThickness = 0.59f;

        // ── Zones (§9.3, Fig 9-2/9-3; CAD coordinates) ───────────────────────────────────────
        public static readonly FieldRect LoadingZoneRed = new FieldRect(-WallInner, -59.101f, 23.907f, 46.599f);
        public static readonly FieldRect GardenRed = new FieldRect(-WallInner, -47.409f, -WallInner, -68.101f);
        /// <summary>Outside the field: ~97 × 54 in, against the red (left) wall.</summary>
        public static readonly FieldRect AllianceAreaRed = new FieldRect(-125.65f, -WallInner - WallThickness, -48.41f, 48.41f);
        public const float TapeWidth = 1.0f;

        // ── HIVE structure (§9.6, Figs 9-8…9-11; CAD) ───────────────────────────────────────
        public const float HivePivotHeight = 43.95f;
        /// <summary>Red pivot x; blue is +12.75 (25.5 in centre to centre, structure centred).</summary>
        public const float HivePivotXRed = -12.75f;
        public const float HiveTiltDeg = 30f;
        public const float FrameWidth = 49.46f;
        public const float FrameDepth = 38.95f;
        /// <summary>The A-frame side plates sit on the x = ±24 tile seam and extend 1 in outward.</summary>
        public const float FrameSideX = 24.5f;

        // CELL geometry in the tray frame: v along the bar from the pivot, w normal to the bar
        // (up when level), x across. A cell is a pentagon-section prism ("house" shape), open at
        // its outer end only.
        public const float CellInnerV = 9.644f;      // back wall
        public const float CellOuterV = 21.394f;     // open mouth
        public const float CellFloorW = -1.468f;
        public const float CellPeakW = 12.529f;
        public const float CellEaveW = 6.09f;        // where the side walls meet the roof slopes
        public const float CellHalfWidth = 10.07f;
        public const float BackWallV = 9.44f;

        // ── FLOWERS (§9.7, Fig 9-12; Event Field Setup Guide §10; CAD) ──────────────────────
        /// <summary>Bore centre distance from the wall face.</summary>
        public const float FlowerBoreFromWall = 2.63f;
        /// <summary>Offset of each FLOWER along its wall from the wall centre (perimeter joint).</summary>
        public const float FlowerAlongWall = 23.39f;
        public const float FlowerTopRingBottom = 20.254f;
        public const float FlowerTopRingTop = 21.404f;
        public const float FlowerTopBore = 4.171f;
        public const float FlowerMidRingBottom = 3.904f;
        public const float FlowerMidRingTop = 5.254f;
        public const float FlowerMidBore = 3.896f;
        public const float FlowerLowerRingTop = 0.354f;
        public const float FlowerLowerBore = 3.222f;
        public const float FlowerPlateAlong = 5.95f;
        public const float FlowerPlateDeep = 5.0f;
        public const float FlowerBackstopHeight = 1.25f;
        /// <summary>Scoring volume: between the top ring and the middle ring (§10.5.2).</summary>
        public const float FlowerScoreZMin = FlowerMidRingBottom;
        public const float FlowerScoreZMax = FlowerTopRingTop;

        /// <summary>
        /// FLOWER bore centres, F1…F4: left wall, rear wall, right wall, audience wall. F1/F2 are on
        /// red's half; F3/F4 are their point-symmetric blue twins.
        /// </summary>
        public static Vector2 FlowerCenter(int index)
        {
            float d = WallInner - FlowerBoreFromWall;
            switch (index)
            {
                case 0: return new Vector2(-d, -FlowerAlongWall);
                case 1: return new Vector2(-FlowerAlongWall, d);
                case 2: return new Vector2(d, FlowerAlongWall);
                default: return new Vector2(FlowerAlongWall, -d);
            }
        }

        /// <summary>Unit vector from the FLOWER's wall into the field.</summary>
        public static Vector2 FlowerInward(int index)
        {
            switch (index)
            {
                case 0: return Vector2.right;
                case 1: return Vector2.down;
                case 2: return Vector2.left;
                default: return Vector2.up;
            }
        }

        public const int FlowerCount = 4;

        // ── Scoring elements (§9.8; CAD outer radii) ────────────────────────────────────────
        public const float PollenDiameter = 2.8f;
        public const float NectarDiameter = 3.62f;
        public const int PollenTotal = 40;
        public const int NectarPerAlliance = 8;
        public const int NectarInCell = 3;
        public const int NectarInAllianceArea = 5;
        public const int PollenPerFlower = 4;
        public const int PollenPerGarden = 4;
        public const int PreloadPollen = 4;

        // ── AprilTags (§9.9) ────────────────────────────────────────────────────────────────
        public static readonly int[] TagsRedRear = { 30, 31, 32, 33 };
        public static readonly int[] TagsRedAudience = { 34, 35, 36, 37 };
        public static readonly int[] TagsBlueAudience = { 38, 39, 40, 41 };
        public static readonly int[] TagsBlueRear = { 42, 43, 44, 45 };

        // ── Robot envelope (R102, R105) ─────────────────────────────────────────────────────
        public const float StartCube = 18f;
        public const float ExpandedLong = 24f;
        public const float ExpandedHeight = 29f;

        public static Vector2 HivePivot(Alliance a) => new Vector2(a == Alliance.Red ? HivePivotXRed : -HivePivotXRed, 0f);

        /// <summary>
        /// Which end of an alliance's HIVE is up at the start of a match: red's audience-side (−y)
        /// cell, blue's rear-side (+y) cell (Fig 10-2; Event Field Setup Guide §11.1).
        /// Returned as the sign of v (along +y) of the staged up-cell.
        /// </summary>
        public static int StagedUpCellSign(Alliance a) => a == Alliance.Red ? -1 : 1;
    }
}
