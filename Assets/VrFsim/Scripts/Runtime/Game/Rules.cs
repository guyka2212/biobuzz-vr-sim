using System.Collections.Generic;
using UnityEngine;
using VrFsim.Settings;

namespace VrFsim.Game
{
    public enum ElementKind { Pollen, NectarRed, NectarBlue }

    public static class ElementKindExt
    {
        public static bool IsNectar(this ElementKind k) => k != ElementKind.Pollen;

        public static Alliance NectarAlliance(this ElementKind k) => k == ElementKind.NectarBlue ? Alliance.Blue : Alliance.Red;

        public static ElementKind NectarOf(Alliance a) => a == Alliance.Red ? ElementKind.NectarRed : ElementKind.NectarBlue;

        public static float DiameterIn(this ElementKind k) => k == ElementKind.Pollen ? FieldSpec.PollenDiameter : FieldSpec.NectarDiameter;
    }

    /// <summary>Point values, Table 10-2 (Competition Manual TU03).</summary>
    public static class Points
    {
        public const int Leave = 3;
        public const int Park = 5;
        public const int HiveTip = 20;
        public const int CellElement = 2;
        public const int BottomNectarBonus = 5;
        public const int OwnedFlowerElement = 2;
        public const int GardenElement = 1;
        public const int MinorFoul = 5;
        public const int MajorFoul = 20;

        // Table 10-3, "All Other Events".
        public const int SwarmThreshold = 16;
        public const int Pollinator1Tips = 4;
        public const int Pollinator2Tips = 7;
        public const int WinRp = 3;
        public const int TieRp = 1;
    }

    /// <summary>
    /// When does an up-CELL tip? FIRST calibrates every HIVE to tip at 8 POLLEN, and at 3 POLLEN +
    /// 3 NECTAR (Event Field Setup Guide §12). A linear load model through those two points gives
    /// NECTAR = 5/3 POLLEN. Both parameters are settings, because only those two points are official.
    /// </summary>
    public static class TipModel
    {
        public static float Load(int pollen, int nectar, RuleAssumptions r) => pollen + nectar * r.nectarWeight;

        public static bool Tips(int pollen, int nectar, RuleAssumptions r) => Load(pollen, nectar, r) >= r.tipLoadPollen - 1e-3f;

        /// <summary>POLLEN still needed to tip a cell that already holds this many NECTAR.</summary>
        public static int PollenNeeded(int nectar, RuleAssumptions r) =>
            Mathf.Max(0, Mathf.CeilToInt(r.tipLoadPollen - nectar * r.nectarWeight - 1e-3f));
    }

    /// <summary>One element inside a FLOWER scoring volume, for ownership evaluation.</summary>
    public struct FlowerEntry
    {
        public ElementKind kind;
        public float heightIn;

        public FlowerEntry(ElementKind kind, float heightIn) { this.kind = kind; this.heightIn = heightIn; }
    }

    public struct FlowerResult
    {
        public Alliance? owner;
        public Alliance? bottomBonus;
        public int scoringElements;
    }

    public static class FlowerRules
    {
        /// <summary>
        /// §10.5.2: the alliance with the top-most NECTAR of its colour owns the FLOWER and earns 2
        /// for every scoring element in it; the alliance with the bottom-most NECTAR earns the
        /// 5-point bottom bonus. No NECTAR means no owner and no bonus.
        /// </summary>
        public static FlowerResult Evaluate(IReadOnlyList<FlowerEntry> entries)
        {
            var res = new FlowerResult { scoringElements = entries.Count };
            float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (var e in entries)
            {
                if (!e.kind.IsNectar()) continue;
                if (e.heightIn > top) { top = e.heightIn; res.owner = e.kind.NectarAlliance(); }
                if (e.heightIn < bottom) { bottom = e.heightIn; res.bottomBonus = e.kind.NectarAlliance(); }
            }
            return res;
        }
    }

    /// <summary>Running and final score for one alliance.</summary>
    [System.Serializable]
    public class AllianceScore
    {
        public int leaveCount;        // robots credited with LEAVE
        public int autoParkCount;
        public int teleopParkCount;
        public int autoTips;
        public int teleopTips;
        public int cellElements;      // elements left in this alliance's up-CELL
        public int ownedFlowerElements;
        public int bottomBonuses;
        public int gardenElements;
        public int minorFoulsAgainstOpponent; // fouls the OPPONENT committed (points to us)
        public int majorFoulsAgainstOpponent;

        public int Tips => autoTips + teleopTips;
        public int LeavePoints => leaveCount * Points.Leave;
        public int ParkPoints => (autoParkCount + teleopParkCount) * Points.Park;
        public int AutoPoints => LeavePoints + autoParkCount * Points.Park + autoTips * Points.HiveTip;
        public int FoulPoints => minorFoulsAgainstOpponent * Points.MinorFoul + majorFoulsAgainstOpponent * Points.MajorFoul;

        public int TeleopPoints =>
            teleopParkCount * Points.Park + teleopTips * Points.HiveTip + cellElements * Points.CellElement +
            ownedFlowerElements * Points.OwnedFlowerElement + bottomBonuses * Points.BottomNectarBonus +
            gardenElements * Points.GardenElement;

        public int Total => AutoPoints + TeleopPoints + FoulPoints;

        public bool SwarmRp => LeavePoints + ParkPoints >= Points.SwarmThreshold;
        public bool Pollinator1Rp => Tips >= Points.Pollinator1Tips;
        public bool Pollinator2Rp => Tips >= Points.Pollinator2Tips;

        public int RankingPoints(AllianceScore opponent)
        {
            int rp = (SwarmRp ? 1 : 0) + (Pollinator1Rp ? 1 : 0) + (Pollinator2Rp ? 1 : 0);
            if (Total > opponent.Total) rp += Points.WinRp;
            else if (Total == opponent.Total) rp += Points.TieRp;
            return rp;
        }

        public void ResetLive()
        {
            cellElements = ownedFlowerElements = bottomBonuses = gardenElements = 0;
        }
    }
}
